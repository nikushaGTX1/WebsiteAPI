using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Website_API.Data;
using Website_API.DTO;
using Website_API.Models;
using Website_API.Services;

namespace Website_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly SupabaseStorageService _storageService;

    public AgentsController(
        AppDbContext context,
        SupabaseStorageService storageService)
    {
        _context = context;
        _storageService = storageService;
    }

    /// <summary>Profile phone, else the public contact phone on the agent's latest listing.</summary>
    public static string? ContactPhone(string? profilePhone, string? listingPhone) =>
        string.IsNullOrWhiteSpace(profilePhone) ? listingPhone?.Trim() : profilePhone.Trim();

    [HttpGet]
    public async Task<IActionResult> GetAgents()
    {
        var agents = await _context.Users
            .Where(u => u.IsAgent)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.FullName,
                u.Bio,
                u.SpokenLanguages,
                u.PhoneNumber,
                ProfilePicturePath = u.ProfilePicture,
                // Signed-in ratings plus reviews left through the agent's public review link.
                RatingSum = (_context.AgentRatings
                    .Where(r => r.AgentId == u.Id)
                    .Sum(r => (int?)r.Stars) ?? 0) + (_context.AgentReviews
                    .Where(r => r.AgentId == u.Id)
                    .Sum(r => (int?)r.Rating) ?? 0),
                RatingCount = _context.AgentRatings
                    .Count(r => r.AgentId == u.Id) + _context.AgentReviews
                    .Count(r => r.AgentId == u.Id),
                ClosedDeals = _context.CrmLeads
                    .Count(l => l.AssignedAgentId == u.Id && l.Status == CrmLeadStatus.Won),
                ListingCount = _context.Apartments
                    .Where(a => a.IsApproved && a.UploadedByUserId == u.Id)
                    .Where(a => !EF.Functions.ILike(a.Description, "%Source: https://www.myhome.ge/%")
                        && !EF.Functions.ILike(a.Description, "%Source: https://home.ss.ge/%")).Count(),
                ListingPhone = _context.Apartments
                    .Where(a => a.IsApproved && a.UploadedByUserId == u.Id)
                    .Where(a => !EF.Functions.ILike(a.Description, "%Source: https://www.myhome.ge/%")
                        && !EF.Functions.ILike(a.Description, "%Source: https://home.ss.ge/%"))
                    .Where(a => a.PhoneNumber != null && a.PhoneNumber != "")
                    .OrderByDescending(a => a.Id)
                    .Select(a => a.PhoneNumber)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var response = await Task.WhenAll(agents.Select(async agent =>
        {
            var profilePicture = await _storageService.CreateSignedUrlAsync(
                agent.ProfilePicturePath,
                604800,
                HttpContext.RequestAborted);

            return new
            {
                agent.Id,
                agent.UserName,
                agent.FullName,
                agent.Bio,
                Languages = ParseLanguages(agent.SpokenLanguages),
                PhoneNumber = ContactPhone(agent.PhoneNumber, agent.ListingPhone),
                ProfilePicture = profilePicture,
                ProfilePictureUrl = profilePicture,
                agent.ProfilePicturePath,
                AverageRating = agent.RatingCount == 0 ? 0 : agent.RatingSum / (double)agent.RatingCount,
                agent.RatingCount,
                agent.ClosedDeals,
                agent.ListingCount
            };
        }));

        return Ok(response);
    }

    [HttpGet("{agentId}")]
    public async Task<IActionResult> GetAgent(string agentId)
    {
        var agent = await _context.Users
            .Where(u => u.Id == agentId && u.IsAgent)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.FullName,
                u.Bio,
                u.SpokenLanguages,
                u.PhoneNumber,
                ProfilePicturePath = u.ProfilePicture,
                // Signed-in ratings plus reviews left through the agent's public review link.
                RatingSum = (_context.AgentRatings
                    .Where(r => r.AgentId == u.Id)
                    .Sum(r => (int?)r.Stars) ?? 0) + (_context.AgentReviews
                    .Where(r => r.AgentId == u.Id)
                    .Sum(r => (int?)r.Rating) ?? 0),
                RatingCount = _context.AgentRatings
                    .Count(r => r.AgentId == u.Id) + _context.AgentReviews
                    .Count(r => r.AgentId == u.Id),
                ClosedDeals = _context.CrmLeads
                    .Count(l => l.AssignedAgentId == u.Id && l.Status == CrmLeadStatus.Won),
                ListingCount = _context.Apartments
                    .Where(a => a.IsApproved && a.UploadedByUserId == u.Id)
                    .Where(a => !EF.Functions.ILike(a.Description, "%Source: https://www.myhome.ge/%")
                        && !EF.Functions.ILike(a.Description, "%Source: https://home.ss.ge/%")).Count(),
                ListingPhone = _context.Apartments
                    .Where(a => a.IsApproved && a.UploadedByUserId == u.Id)
                    .Where(a => !EF.Functions.ILike(a.Description, "%Source: https://www.myhome.ge/%")
                        && !EF.Functions.ILike(a.Description, "%Source: https://home.ss.ge/%"))
                    .Where(a => a.PhoneNumber != null && a.PhoneNumber != "")
                    .OrderByDescending(a => a.Id)
                    .Select(a => a.PhoneNumber)
                    .FirstOrDefault(),
                Ratings = _context.AgentRatings
                    .Where(r => r.AgentId == u.Id)
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => new
                    {
                        r.Id,
                        r.Stars,
                        r.Comment,
                        r.CreatedAt
                    })
                    .ToList(),
                LinkReviews = _context.AgentReviews
                    .Where(r => r.AgentId == u.Id)
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(50)
                    .Select(r => new
                    {
                        r.Id,
                        r.Rating,
                        r.Comment,
                        r.CustomerName,
                        r.CreatedAt
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (agent == null)
            return NotFound(new { message = "Agent not found" });

        var profilePicture = await _storageService.CreateSignedUrlAsync(
            agent.ProfilePicturePath,
            604800,
            HttpContext.RequestAborted);

        return Ok(new
        {
            agent.Id,
            agent.UserName,
            agent.FullName,
            agent.Bio,
            Languages = ParseLanguages(agent.SpokenLanguages),
            PhoneNumber = ContactPhone(agent.PhoneNumber, agent.ListingPhone),
            ProfilePicture = profilePicture,
            ProfilePictureUrl = profilePicture,
            agent.ProfilePicturePath,
            AverageRating = agent.RatingCount == 0 ? 0 : agent.RatingSum / (double)agent.RatingCount,
            agent.RatingCount,
            agent.ClosedDeals,
            agent.ListingCount,
            // Public list: only the reviewer's first name is shown, never contact details.
            Ratings = agent.Ratings
                .Select(r => new
                {
                    Id = r.Id.ToString(),
                    r.Stars,
                    r.Comment,
                    r.CreatedAt,
                    ReviewerName = (string?)null
                })
                .Concat(agent.LinkReviews.Select(r => new
                {
                    Id = r.Id.ToString(),
                    Stars = r.Rating,
                    r.Comment,
                    r.CreatedAt,
                    ReviewerName = FirstName(r.CustomerName)
                }))
                .OrderByDescending(r => r.CreatedAt)
                .ToList()
        });
    }

    private static string? FirstName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];

    [Authorize]
    [HttpPost("{agentId}/ratings")]
    public async Task<IActionResult> RateAgent(string agentId, RatingDto dto)
    {
        if (dto.Stars < 1 || dto.Stars > 5)
            return BadRequest(new { message = "Stars must be between 1 and 5" });

        var agent = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == agentId && u.IsAgent);

        if (agent == null)
            return NotFound(new { message = "Agent not found" });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId == null)
            return Unauthorized();

        var rating = new AgentRating
        {
            AgentId = agentId,
            UserId = userId,
            Stars = dto.Stars,
            Comment = dto.Comment
        };

        _context.AgentRatings.Add(rating);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Rating added successfully" });
    }

    /// <summary>Language codes an agent may list (shown as Ge, En, Ru, …).</summary>
    public static readonly string[] SupportedLanguages =
        ["ka", "en", "ru", "tr", "hy", "az", "uk", "de", "fr", "es", "it", "ar", "he", "zh", "fa"];

    /// <summary>Agents who never had languages set keep showing Georgian, English and Russian.</summary>
    private static readonly string[] DefaultLanguages = ["ka", "en", "ru"];

    public static string[] ParseLanguages(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return DefaultLanguages;
        var codes = stored
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(code => SupportedLanguages.Contains(code))
            .Distinct()
            .ToArray();
        return codes.Length == 0 ? DefaultLanguages : codes;
    }

    /// <summary>Admins and managers set which languages an agent speaks.</summary>
    [Authorize(Roles = "Admin,Manager")]
    [HttpPut("{agentId}/languages")]
    public async Task<IActionResult> SetLanguages(
        string agentId,
        AgentLanguagesDto dto,
        CancellationToken cancellationToken)
    {
        var codes = (dto.Languages ?? [])
            .Select(code => (code ?? "").Trim().ToLowerInvariant())
            .Where(code => code.Length > 0)
            .Distinct()
            .ToArray();
        if (codes.Length == 0)
            return BadRequest(new { message = "Choose at least one language." });
        var unknown = codes.Where(code => !SupportedLanguages.Contains(code)).ToArray();
        if (unknown.Length > 0)
            return BadRequest(new { message = $"Unsupported language: {string.Join(", ", unknown)}." });

        var agent = await _context.Users.FirstOrDefaultAsync(
            user => user.Id == agentId && user.IsAgent,
            cancellationToken);
        if (agent is null)
            return NotFound(new { message = "Agent not found" });

        // Keep the order of the supported list so profiles read consistently (Ge, En, Ru, …).
        var ordered = SupportedLanguages.Where(codes.Contains).ToArray();
        agent.SpokenLanguages = string.Join(",", ordered);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { languages = ordered });
    }
}

public class AgentLanguagesDto
{
    public List<string?>? Languages { get; set; }
}
