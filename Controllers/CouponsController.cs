using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.Models;

namespace Website_API.Controllers;

/// <summary>Rewards kept in a client's account, and the agent side that applies them to a deal.</summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class CouponsController : ControllerBase
{
    /// <summary>Floor the player must reach in the Velven game to win the cashback (default).</summary>
    public const int GameRewardFloor = 30;
    public const string GameRewardKind = "game-cashback-50";
    private const string ManagerRoles = "Admin,Manager";
    // No 0/O or 1/I so codes read clearly over the phone.
    private const string CodeAlphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ITimeLimitedDataProtector _gameTickets;

    public CouponsController(AppDbContext context, IConfiguration configuration, IDataProtectionProvider dataProtection)
    {
        _context = context;
        _configuration = configuration;
        _gameTickets = dataProtection.CreateProtector("Velven.GameTicket").ToTimeLimitedDataProtector();
    }

    /// <summary>
    /// Shortest real play time before a win counts: about two seconds per floor (60 s for floor 30).
    /// The browser cannot fake this, because the start time sits inside a ticket only the server can read.
    /// </summary>
    private TimeSpan MinPlayTime => TimeSpan.FromSeconds(_configuration.GetValue("Game:MinSecondsPerFloor", 2) * RewardFloor);

    /// <summary>
    /// The game page asks for a signed ticket when a game starts. The win must bring it back.
    /// A ticket lasts 12 hours, so a win made before signing in can still be saved afterwards.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("game-start")]
    public IActionResult StartGame() =>
        Ok(new { ticket = _gameTickets.Protect(DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), TimeSpan.FromHours(12)) });

    /// <summary>
    /// Reward floor in use. Set Game__RewardFloor (e.g. 3) on the server to test the reward
    /// quickly; remove it to go back to 30.
    /// </summary>
    private int RewardFloor => Math.Max(1, _configuration.GetValue("Game:RewardFloor", GameRewardFloor));

    /// <summary>The game page asks which floor wins the cashback.</summary>
    [AllowAnonymous]
    [HttpGet("game-rules")]
    public IActionResult GetGameRules() => Ok(new { rewardFloor = RewardFloor, amountUsd = 50 });

    public record GameRewardRequest(int Floor, string? Ticket = null);

    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var coupons = await _context.ClientCoupons
            .AsNoTracking()
            .Where(coupon => coupon.UserId == userId)
            .OrderByDescending(coupon => coupon.CreatedAt)
            .ToListAsync(cancellationToken);
        return Ok(coupons.Select(ToDto));
    }

    /// <summary>
    /// Called by the game page when the player reaches the reward floor. One cashback per account:
    /// a second win returns the coupon the client already has.
    /// </summary>
    [HttpPost("game-reward")]
    public async Task<IActionResult> ClaimGameReward(GameRewardRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        // Staff accounts cannot win client cashback.
        if (User.IsInRole("Admin") || User.IsInRole("Manager") || User.IsInRole("Agent") || User.IsInRole("Uploader"))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Staff accounts cannot win the cashback." });
        if (request.Floor < RewardFloor || request.Floor > 10_000)
            return BadRequest(new { message = $"Reach floor {RewardFloor} to win the cashback." });
        // The floor number comes from the browser; the play time does not.
        long startedAt;
        try
        {
            if (string.IsNullOrWhiteSpace(request.Ticket) || !long.TryParse(_gameTickets.Unprotect(request.Ticket), out startedAt))
                return BadRequest(new { message = "Play the game to win the cashback." });
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return BadRequest(new { message = "Play the game to win the cashback." });
        }
        if (DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(startedAt) < MinPlayTime)
            return BadRequest(new { message = "Play the game to win the cashback." });

        var existing = await _context.ClientCoupons
            .FirstOrDefaultAsync(coupon => coupon.UserId == userId && coupon.Kind == GameRewardKind, cancellationToken);
        if (existing is not null) return Ok(new { created = false, coupon = ToDto(existing) });

        var coupon = new ClientCoupon
        {
            UserId = userId,
            Kind = GameRewardKind,
            Code = await NewCodeAsync(cancellationToken),
            AmountUsd = 50,
            Title = "$50 cashback · Velven game",
        };
        _context.ClientCoupons.Add(coupon);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two wins sent at the same moment: the unique index keeps one, return that one.
            _context.ChangeTracker.Clear();
            existing = await _context.ClientCoupons.AsNoTracking()
                .FirstAsync(c => c.UserId == userId && c.Kind == GameRewardKind, cancellationToken);
            return Ok(new { created = false, coupon = ToDto(existing) });
        }
        return Ok(new { created = true, coupon = ToDto(coupon) });
    }

    public record RedeemRequest(string? Note);
    public record VoidRequest(string? Reason);

    /// <summary>Managers: every coupon, newest first, searchable by code, client name or email.</summary>
    [Authorize(Roles = ManagerRoles)]
    [HttpGet("all")]
    public async Task<IActionResult> All(string? search = null, string? status = null, int page = 1,
        CancellationToken cancellationToken = default)
    {
        page = Math.Clamp(page, 1, 10000);
        const int pageSize = 30;
        var query = _context.ClientCoupons.AsNoTracking().Include(c => c.User).AsQueryable();
        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var upper = term.ToUpperInvariant();
            var lower = term.ToLowerInvariant();
            query = query.Where(c => c.Code.Contains(upper)
                || (c.User.FullName != null && c.User.FullName.ToLower().Contains(lower))
                || (c.User.Email != null && c.User.Email.ToLower().Contains(lower)));
        }
        query = status switch
        {
            "active" => query.Where(c => c.RedeemedAt == null && c.VoidedAt == null),
            "redeemed" => query.Where(c => c.RedeemedAt != null),
            "void" => query.Where(c => c.VoidedAt != null),
            _ => query,
        };
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var staffNames = await StaffNamesAsync(items, cancellationToken);
        var all = _context.ClientCoupons.AsNoTracking();
        var counts = new
        {
            Active = await all.CountAsync(c => c.RedeemedAt == null && c.VoidedAt == null, cancellationToken),
            Redeemed = await all.CountAsync(c => c.RedeemedAt != null, cancellationToken),
            Void = await all.CountAsync(c => c.VoidedAt != null, cancellationToken),
        };
        return Ok(new { total, page, pageSize, counts, items = items.Select(c => StaffDto(c, staffNames)) });
    }

    /// <summary>Managers check the code a client shows them.</summary>
    [Authorize(Roles = ManagerRoles)]
    [HttpGet("lookup/{code}")]
    public async Task<IActionResult> Lookup(string code, CancellationToken cancellationToken)
    {
        var normalized = NormalizeCode(code);
        var coupon = await _context.ClientCoupons.AsNoTracking()
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Code == normalized, cancellationToken);
        if (coupon is null) return NotFound(new { message = "No coupon with this code." });
        return Ok(StaffDto(coupon, await StaffNamesAsync([coupon], cancellationToken)));
    }

    /// <summary>
    /// Marks a coupon as used on a deal. The update only succeeds while the coupon is still
    /// unused and not cancelled, so two managers pressing at once cannot use it twice.
    /// </summary>
    [Authorize(Roles = ManagerRoles)]
    [HttpPost("{id:int}/redeem")]
    public async Task<IActionResult> Redeem(int id, RedeemRequest? request, CancellationToken cancellationToken)
    {
        var note = Clip(request?.Note, 200);
        var now = DateTime.UtcNow;
        var userId = GetUserId();
        // Nobody applies their own coupon.
        var updated = await _context.ClientCoupons
            .Where(c => c.Id == id && c.RedeemedAt == null && c.VoidedAt == null && c.UserId != userId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(c => c.RedeemedAt, now)
                .SetProperty(c => c.RedeemedById, userId)
                .SetProperty(c => c.RedeemNote, note), cancellationToken);
        var coupon = await _context.ClientCoupons.AsNoTracking().Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (coupon is null) return NotFound();
        if (updated == 0 && coupon.UserId == userId)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You cannot redeem your own coupon." });
        if (updated == 0)
            return Conflict(new { message = coupon.VoidedAt is not null ? "This coupon was cancelled." : "This coupon was already used." });
        return Ok(StaffDto(coupon, await StaffNamesAsync([coupon], cancellationToken)));
    }

    /// <summary>Cancels an unused coupon (for example a faked win). It can no longer be used.</summary>
    [Authorize(Roles = ManagerRoles)]
    [HttpPost("{id:int}/void")]
    public async Task<IActionResult> Void(int id, VoidRequest? request, CancellationToken cancellationToken)
    {
        var reason = Clip(request?.Reason, 200);
        var now = DateTime.UtcNow;
        var userId = GetUserId();
        var updated = await _context.ClientCoupons
            .Where(c => c.Id == id && c.RedeemedAt == null && c.VoidedAt == null)
            .ExecuteUpdateAsync(set => set
                .SetProperty(c => c.VoidedAt, now)
                .SetProperty(c => c.VoidedById, userId)
                .SetProperty(c => c.VoidReason, reason), cancellationToken);
        var coupon = await _context.ClientCoupons.AsNoTracking().Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (coupon is null) return NotFound();
        if (updated == 0)
            return Conflict(new { message = coupon.VoidedAt is not null ? "This coupon was already cancelled." : "A used coupon cannot be cancelled." });
        return Ok(StaffDto(coupon, await StaffNamesAsync([coupon], cancellationToken)));
    }

    /// <summary>Accepts "vlv 7k2m 9qxd", "VLV7K2M9QXD" and the printed form.</summary>
    private static string NormalizeCode(string code)
    {
        var compact = new string(code.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        return compact.Length == 11 && compact.StartsWith("VLV")
            ? $"VLV-{compact[3..7]}-{compact[7..]}"
            : code.Trim().ToUpperInvariant();
    }

    private static string? Clip(string? text, int max)
    {
        var value = text?.Trim();
        return string.IsNullOrEmpty(value) ? null : value.Length > max ? value[..max] : value;
    }

    private async Task<Dictionary<string, string>> StaffNamesAsync(IEnumerable<ClientCoupon> coupons, CancellationToken cancellationToken)
    {
        var ids = coupons.SelectMany(c => new[] { c.RedeemedById, c.VoidedById })
            .Where(id => id is not null).Select(id => id!).Distinct().ToList();
        if (ids.Count == 0) return new();
        return await _context.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName ?? u.Email ?? u.UserName ?? "Staff", cancellationToken);
    }

    private static object StaffDto(ClientCoupon coupon, IReadOnlyDictionary<string, string> staff) => new
    {
        coupon = ToDto(coupon),
        clientName = coupon.User?.FullName,
        clientEmail = coupon.User?.Email,
        clientPhone = coupon.User?.PhoneNumber,
        redeemNote = coupon.RedeemNote,
        redeemedBy = coupon.RedeemedById is not null && staff.TryGetValue(coupon.RedeemedById, out var r) ? r : null,
        voidedAt = coupon.VoidedAt,
        voidReason = coupon.VoidReason,
        voidedBy = coupon.VoidedById is not null && staff.TryGetValue(coupon.VoidedById, out var v) ? v : null,
    };

    private async Task<string> NewCodeAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var chars = RandomNumberGenerator.GetItems<char>(CodeAlphabet, 8);
            var code = $"VLV-{new string(chars, 0, 4)}-{new string(chars, 4, 4)}";
            if (!await _context.ClientCoupons.AnyAsync(c => c.Code == code, cancellationToken)) return code;
        }
    }

    private static object ToDto(ClientCoupon coupon) => new
    {
        coupon.Id,
        coupon.Kind,
        coupon.Code,
        coupon.AmountUsd,
        coupon.Title,
        coupon.CreatedAt,
        coupon.RedeemedAt,
        Status = coupon.VoidedAt is not null ? "void" : coupon.RedeemedAt is not null ? "redeemed" : "active",
    };

    private string? GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);
}
