using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
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
    private const string StaffRoles = "Admin,Manager,Agent";
    // No 0/O or 1/I so codes read clearly over the phone.
    private const string CodeAlphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public CouponsController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    /// <summary>
    /// Reward floor in use. Set Game__RewardFloor (e.g. 3) on the server to test the reward
    /// quickly; remove it to go back to 30.
    /// </summary>
    private int RewardFloor => Math.Max(1, _configuration.GetValue("Game:RewardFloor", GameRewardFloor));

    /// <summary>The game page asks which floor wins the cashback.</summary>
    [AllowAnonymous]
    [HttpGet("game-rules")]
    public IActionResult GetGameRules() => Ok(new { rewardFloor = RewardFloor, amountUsd = 50 });

    public record GameRewardRequest(int Floor);

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
        if (request.Floor < RewardFloor)
            return BadRequest(new { message = $"Reach floor {RewardFloor} to win the cashback." });

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

    /// <summary>Agents look up the code a client shows them.</summary>
    [Authorize(Roles = StaffRoles)]
    [HttpGet("lookup/{code}")]
    public async Task<IActionResult> Lookup(string code, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var coupon = await _context.ClientCoupons.AsNoTracking()
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Code == normalized, cancellationToken);
        if (coupon is null) return NotFound(new { message = "No coupon with this code." });
        return Ok(new { coupon = ToDto(coupon), clientName = coupon.User.FullName, clientEmail = coupon.User.Email });
    }

    /// <summary>Marks a coupon as used on a deal, so it cannot be applied twice.</summary>
    [Authorize(Roles = StaffRoles)]
    [HttpPost("{id:int}/redeem")]
    public async Task<IActionResult> Redeem(int id, CancellationToken cancellationToken)
    {
        var coupon = await _context.ClientCoupons.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (coupon is null) return NotFound();
        if (coupon.RedeemedAt is not null) return Conflict(new { message = "This coupon was already used." });
        coupon.RedeemedAt = DateTime.UtcNow;
        coupon.RedeemedById = GetUserId();
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(coupon));
    }

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
        Status = coupon.RedeemedAt is null ? "active" : "redeemed",
    };

    private string? GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);
}
