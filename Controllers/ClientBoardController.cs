using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.DTO;
using Website_API.Models;
using Website_API.Services;

namespace Website_API.Controllers;

[ApiController]
[Route("api")]
public class ClientBoardController(AppDbContext db, SupabaseStorageService storage) : ControllerBase
{
    public const string StaffRoles = "Admin,Manager,Agent";
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private bool Elevated => User.IsInRole("Admin") || User.IsInRole("Manager");
    private IQueryable<ClientIntakeLink> OwnLinks => db.ClientIntakeLinks.Where(x => x.AgentUserId == UserId);
    private IQueryable<ClientProfile> AccessibleProfiles => db.ClientProfiles.Where(x => Elevated || x.AgentUserId == UserId);
    // Recheck current Identity membership for the linked agent: stale links cannot keep
    // publishing profiles for a deleted/demoted staff account.
    private IQueryable<string> StaffIds => from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id
        where role.Name == "Admin" || role.Name == "Manager" || role.Name == "Agent" select ur.UserId;

    [Authorize(Roles = StaffRoles)]
    [HttpGet("client-intake-links")]
    public async Task<IActionResult> Links(CancellationToken ct) => Ok(await OwnLinks.AsNoTracking()
        .OrderByDescending(x => x.CreatedAt).Select(x => new { x.Id, x.Token, x.Slug, x.IsActive, x.CreatedAt }).ToListAsync(ct));

    [Authorize(Roles = StaffRoles)]
    [HttpPost("client-intake-links")]
    public async Task<IActionResult> CreateLink(CancellationToken ct)
    {
        if (UserId is null || !await StaffIds.ContainsAsync(UserId, ct)) return Forbid();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var link = new ClientIntakeLink { Token = token, AgentUserId = UserId };
        db.ClientIntakeLinks.Add(link);
        await db.SaveChangesAsync(ct);
        return Ok(new { link.Id, link.Token, link.Slug, link.IsActive, link.CreatedAt });
    }

    [Authorize(Roles = StaffRoles)]
    [HttpPatch("client-intake-links/{id:long}")]
    public async Task<IActionResult> UpdateLink(long id, IntakeLinkUpdate request, CancellationToken ct)
    {
        var link = await db.ClientIntakeLinks.FirstOrDefaultAsync(x => x.Id == id && (Elevated || x.AgentUserId == UserId), ct);
        if (link is null) return NotFound();
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? null : request.Slug.Trim().ToLowerInvariant();
        if (slug is not null && !Regex.IsMatch(slug, "^[a-z][a-z0-9-]{2,63}$")) return BadRequest();
        if (slug is not null && await db.ClientIntakeLinks.AnyAsync(x => x.Id != id && (x.Slug == slug || x.Token == slug), ct)) return Conflict();
        link.Slug = slug;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" }) { return Conflict(); }
        return Ok(new { link.Id, link.Token, link.Slug, link.IsActive, link.CreatedAt });
    }
    public record IntakeLinkUpdate(string? Slug);

    [Authorize(Roles = StaffRoles)]
    [HttpDelete("client-intake-links/{id:long}")]
    public async Task<IActionResult> Revoke(long id, CancellationToken ct)
    {
        var link = await db.ClientIntakeLinks.FirstOrDefaultAsync(x => x.Id == id && (Elevated || x.AgentUserId == UserId), ct);
        if (link is null) return NotFound();
        link.IsActive = false;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [Authorize(Roles = StaffRoles)]
    [HttpGet("client-profiles")]
    public async Task<IActionResult> Submissions([FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (page < 1 || page > 100000) return BadRequest();
        // The intake card always shows the signed-in agent's submissions; managers can
        // operate on another agent's profile by ID through the secured status endpoint.
        var q = db.ClientProfiles.AsNoTracking().Where(x => x.AgentUserId == UserId);
        return Ok(new { total = await q.CountAsync(ct), page, pageSize = 20,
            items = await q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip((page - 1) * 20).Take(20)
                .Select(x => new { x.Id, x.FirstName, x.Phone, x.Status, x.CreatedAt, x.LeadId, published = x.PublishConsentAt != null }).ToListAsync(ct) });
    }

    public record StatusRequest(string Status);
    [Authorize(Roles = StaffRoles)]
    [HttpPatch("client-profiles/{id:long}/status")]
    public async Task<IActionResult> Status(long id, StatusRequest request, CancellationToken ct)
    {
        if (request.Status is not ("Active" or "Paused" or "Closed")) return BadRequest();
        var profile = await AccessibleProfiles.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (profile is null) return NotFound();
        profile.Status = request.Status;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [AllowAnonymous]
    [EnableRateLimiting("CrmInquiries")]
    [RequestSizeLimit(110 * 1024)]
    [HttpPost("client-intake/{token}")]
    public async Task<IActionResult> Intake(string token, ClientIntakeRequest request, CancellationToken ct)
    {
        var error = ClientRequirements.Validate(request);
        if (error is not null) return BadRequest(new { code = error });
        // Share a transaction with revocation: an intake cannot pass a revocation that
        // has already committed. The row lock also serializes simultaneous submissions.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var link = await db.ClientIntakeLinks.FromSqlInterpolated($"SELECT * FROM \"ClientIntakeLinks\" WHERE (\"Token\" = {token} OR \"Slug\" = {token}) FOR UPDATE").FirstOrDefaultAsync(ct);
        if (link is null || !link.IsActive || !await StaffIds.ContainsAsync(link.AgentUserId, ct)) return NotFound(new { code = "invalid_intake" });
        var p = ClientRequirements.Read(request.Profile.GetRawText());
        var profileJson = JsonSerializer.Serialize(p, ClientRequirements.Json);
        var now = DateTime.UtcNow;
        var lead = new CrmLead { Name = request.FirstName.Trim(), Phone = request.Phone.Trim(),
            AssignedAgentId = link.AgentUserId, Source = CrmLeadSource.AiMatch,
            Goal = p.PropertyGoal, PreferredDistricts = p.Districts.ToArray(), Bedrooms = p.Bedrooms,
            BudgetMin = p.BudgetMin, BudgetMax = p.BudgetMax, Currency = p.Currency,
            ConsentGiven = true, ConsentGivenAt = now, CreatedAt = now, UpdatedAt = now,
            Preferences = profileJson };
        var (min, max) = HomeMatchRequirements.BudgetRangeUsd(p);
        var profile = new ClientProfile { IntakeLinkId = link.Id, AgentUserId = link.AgentUserId, Lead = lead,
            // Preserve the questionnaire's codes privately; publish only the explicit DTO below.
            ProfileJson = profileJson, FirstName = lead.Name, Phone = lead.Phone!,
            Nationality = string.IsNullOrWhiteSpace(request.Nationality) ? null : request.Nationality.Trim(),
            ConsentAt = now, PublishConsentAt = request.PublishConsent ? now : null, CreatedAt = now,
            Districts = p.Districts.ToArray(), BudgetMinUsd = min ?? 0, BudgetMaxUsd = max ?? 0,
            MoveInDate = p.MoveInTiming == "SpecificDate" && DateOnly.TryParse(p.MoveInDate, out var date) ? date :
                p.MoveInTiming is "Immediately" or "WithinOneWeek" ? DateOnly.FromDateTime(now.AddDays(p.MoveInTiming == "Immediately" ? 0 : 7)) :
                p.MoveInTiming == "WithinOneMonth" ? DateOnly.FromDateTime(now.AddDays(30)) : null,
            MoveInTiming = p.MoveInTiming ?? "", Residents = p.Adults + p.Children,
            HasPets = p.HasPet == true || p.PetTypes.Count > 0 || p.PetType is "Dog" or "Cat" or "Other" };
        db.ClientProfiles.Add(profile);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(new { id = profile.Id });
    }

    [AllowAnonymous]
    [HttpGet("client-board")]
    public async Task<ActionResult<ClientBoardPage>> Board(string? district = null, decimal? budgetMin = null,
        decimal? budgetMax = null, DateOnly? moveInBefore = null, int? residents = null, bool? pets = null,
        int page = 1, int pageSize = 12, CancellationToken ct = default)
    {
        if (page < 1 || page > 100000 || pageSize < 1 || pageSize > 48 || budgetMin < 0 || budgetMax < 0 ||
            budgetMin > budgetMax || residents is < 1 or > 7 || district?.Length > 80) return BadRequest();
        // Renters only: purchase budgets must never be presented as monthly rent.
        var q = db.ClientProfiles.AsNoTracking().Where(x => x.Status == "Active" && x.PublishConsentAt != null && StaffIds.Contains(x.AgentUserId)
            && EF.Functions.JsonContains(x.ProfileJson, "{\"propertyGoal\":\"Rent\"}"));
        if (!string.IsNullOrEmpty(district)) q = q.Where(x => x.Districts.Contains(district) || EF.Functions.JsonContains(x.ProfileJson, "{\"locationFlexible\":true}"));
        // Budget filters are USD/month and select overlapping requirement ranges.
        if (budgetMin.HasValue) q = q.Where(x => x.BudgetMaxUsd >= budgetMin);
        if (budgetMax.HasValue) q = q.Where(x => x.BudgetMinUsd <= budgetMax);
        if (moveInBefore.HasValue) q = q.Where(x => x.MoveInDate <= moveInBefore || x.MoveInTiming == "Flexible");
        if (residents.HasValue) q = q.Where(x => x.Residents == residents);
        if (pets.HasValue) q = q.Where(x => x.HasPets == pets);
        var total = await q.CountAsync(ct);
        var rows = await q.Include(x => x.Agent).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var items = new List<PublicClientCard>();
        foreach (var row in rows)
        {
            var photo = await storage.CreateSignedUrlAsync(row.Agent.ProfilePicture, 3600, ct);
            items.Add(ToPublic(row, photo));
        }
        return new ClientBoardPage(items, total, page, pageSize);
    }

    public static PublicClientCard ToPublic(ClientProfile row, string? agentPhoto)
    {
        var p = ClientRequirements.Read(row.ProfileJson);
        return new(row.Id, row.IdentityVerified, row.Nationality,
            new(p.BudgetMin, p.BudgetMax, p.Currency, p.Districts.Where(x => x != "SelectOnMap").ToArray(), p.LocationFlexible,
                p.Gender, p.HouseholdType, p.Adults, p.Children, p.ChildrenAgeGroups.ToArray(), p.Bedrooms,
                p.BedroomChoices.ToArray(), p.MinSize, p.RentalDuration, p.MoveInTiming, p.MoveInDate,
                row.HasPets, p.PetType, p.PetTypes.ToArray(), p.DogSize, p.PetCount, p.TopPriorities.Take(3).ToArray()),
            new(row.Agent.FullName ?? "Velven agent", row.Agent.PhoneNumber, agentPhoto));
    }
}
