using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.Models;
using Website_API.Services;

namespace Website_API.Controllers;

/// <summary>
/// Client reviews for agents. Each agent has one permanent public link
/// (velven.ge/review/{slug}) that anyone can open and rate without signing in;
/// the agent sees the reviews in their own dashboard.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public partial class ReviewsController : ControllerBase
{
    private const int MaxNameLength = 60;
    private const int MaxCommentLength = 1000;
    /// <summary>Faster than this from page load to submit is treated as a bot.</summary>
    private const int MinFillMilliseconds = 1200;
    /// <summary>Reviews one network may leave for the same agent per day.</summary>
    private const int MaxReviewsPerIpPerAgentPerDay = 3;

    private readonly AppDbContext _context;
    private readonly SupabaseStorageService _storageService;
    private readonly IConfiguration _configuration;

    public ReviewsController(
        AppDbContext context,
        SupabaseStorageService storageService,
        IConfiguration configuration)
    {
        _context = context;
        _storageService = storageService;
        _configuration = configuration;
    }

    /// <summary>
    /// The site proxies API calls, so the socket address is the proxy's. Use the
    /// forwarded client IP when it is valid; direct callers use their own address.
    /// </summary>
    public static string ClientIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString().Split(',', 2)[0].Trim();
        return IPAddress.TryParse(forwarded, out var ip)
            ? ip.ToString()
            : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>Public: who the review link belongs to (name and photo only).</summary>
    [AllowAnonymous]
    [HttpGet("link/{slug}")]
    public async Task<IActionResult> GetLink(string slug, CancellationToken cancellationToken)
    {
        var agent = await FindAgentBySlugAsync(slug, cancellationToken);
        if (agent is null) return NotFound(new { message = "This review link is not valid." });

        var photo = await _storageService.CreateSignedUrlAsync(agent.ProfilePicture, 604800, cancellationToken);
        return Ok(new
        {
            slug = agent.ReviewSlug,
            agentName = DisplayName(agent),
            profilePicture = photo
        });
    }

    /// <summary>Public: leave a review through an agent's link. The agent comes from the link only.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("PublicReviews")]
    [HttpPost("link/{slug}")]
    public async Task<IActionResult> Submit(
        string slug,
        [FromBody] SubmitReviewDto dto,
        CancellationToken cancellationToken)
    {
        var agent = await FindAgentBySlugAsync(slug, cancellationToken);
        if (agent is null) return NotFound(new { message = "This review link is not valid." });

        // Honeypot: people never see this field, simple bots fill it. Pretend it worked.
        if (!string.IsNullOrWhiteSpace(dto.Website)) return Ok(new { message = "Thank you for your review." });

        if (dto.Rating is < 1 or > 5)
            return BadRequest(new { message = "Choose a rating from 1 to 5 stars." });
        if (dto.SubmissionId == Guid.Empty)
            return BadRequest(new { message = "Reload the page and try again." });
        if (dto.ElapsedMs is null or < MinFillMilliseconds)
            return BadRequest(new { message = "Please take a moment to review before submitting." });

        var name = Clean(dto.CustomerName, singleLine: true);
        var comment = Clean(dto.Comment, singleLine: false);
        if (name?.Length > MaxNameLength)
            return BadRequest(new { message = $"Name can be at most {MaxNameLength} characters." });
        if (comment?.Length > MaxCommentLength)
            return BadRequest(new { message = $"Comment can be at most {MaxCommentLength} characters." });
        if ((comment is not null && LinkPattern().IsMatch(comment)) || (name is not null && LinkPattern().IsMatch(name)))
            return BadRequest(new { message = "Please remove links from your review." });

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == agent.Id)
            return BadRequest(new { message = "You cannot review yourself." });

        // A repeated submit (double click, retry after a slow network) returns the first result.
        var alreadySaved = await _context.AgentReviews.AnyAsync(
            review => review.AgentId == agent.Id && review.SubmissionId == dto.SubmissionId,
            cancellationToken);
        if (alreadySaved) return Ok(new { message = "Thank you for your review." });

        var ipHash = HashIp(ClientIp(HttpContext));
        var since = DateTime.UtcNow.AddDays(-1);
        var recentFromIp = await _context.AgentReviews.CountAsync(
            review => review.AgentId == agent.Id && review.IpHash == ipHash && review.CreatedAt >= since,
            cancellationToken);
        if (recentFromIp >= MaxReviewsPerIpPerAgentPerDay)
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { message = "You have already reviewed this agent today. Thank you!" });

        _context.AgentReviews.Add(new AgentReview
        {
            AgentId = agent.Id,
            CustomerName = name,
            Rating = dto.Rating,
            Comment = comment,
            SubmissionId = dto.SubmissionId,
            IpHash = ipHash,
            CreatedAt = DateTime.UtcNow
        });
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two identical submits raced and the first one was stored: that is a success.
            var stored = await _context.AgentReviews.AsNoTracking().AnyAsync(
                review => review.AgentId == agent.Id && review.SubmissionId == dto.SubmissionId,
                cancellationToken);
            if (!stored) throw;
        }

        return Ok(new { message = "Thank you for your review." });
    }

    /// <summary>
    /// Signed-in agent's own reviews, link and statistics. Managers and admins may
    /// pass agentId to open another agent's reviews; everyone else only sees their own.
    /// </summary>
    [Authorize]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(
        [FromQuery] int? rating,
        [FromQuery] string? sort,
        [FromQuery] string? agentId,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        var isStaff = User.IsInRole("Admin") || User.IsInRole("Manager");
        var targetId = isStaff && !string.IsNullOrWhiteSpace(agentId) ? agentId.Trim() : currentUserId;
        var agent = await _context.Users.FirstOrDefaultAsync(user => user.Id == targetId, cancellationToken);
        if (agent is null) return NotFound(new { message = "Agent not found." });

        var canOwnReviews = agent.IsAgent || isStaff || User.IsInRole("Agent");
        if (targetId == currentUserId && !canOwnReviews) return Forbid();
        if (targetId != currentUserId && !agent.IsAgent) return NotFound(new { message = "Agent not found." });

        var slug = await EnsureSlugAsync(agent, cancellationToken);

        var all = _context.AgentReviews.AsNoTracking().Where(review => review.AgentId == agent.Id);
        var counts = await all
            .GroupBy(review => review.Rating)
            .Select(group => new { Rating = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var total = counts.Sum(item => item.Count);
        var average = total == 0 ? 0 : counts.Sum(item => item.Rating * item.Count) / (double)total;

        var filtered = rating is >= 1 and <= 5 ? all.Where(review => review.Rating == rating) : all;
        filtered = string.Equals(sort, "oldest", StringComparison.OrdinalIgnoreCase)
            ? filtered.OrderBy(review => review.CreatedAt)
            : filtered.OrderByDescending(review => review.CreatedAt);
        var reviews = await filtered
            .Take(500)
            .Select(review => new
            {
                review.Id,
                review.CustomerName,
                review.Rating,
                review.Comment,
                review.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            agentId = agent.Id,
            agentName = DisplayName(agent),
            slug,
            summary = new
            {
                average = Math.Round(average, 2),
                total,
                // Index 0 = 1 star ... index 4 = 5 stars.
                distribution = Enumerable.Range(1, 5)
                    .Select(stars => counts.FirstOrDefault(item => item.Rating == stars)?.Count ?? 0)
                    .ToArray()
            },
            reviews
        });
    }

    private async Task<AppUser?> FindAgentBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var value = (slug ?? "").Trim().ToLowerInvariant();
        if (value.Length is 0 or > 80 || !SlugPattern().IsMatch(value)) return null;
        return await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(user => user.ReviewSlug == value, cancellationToken);
    }

    /// <summary>Creates the agent's permanent slug the first time it is needed; it never changes afterwards.</summary>
    private async Task<string> EnsureSlugAsync(AppUser agent, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(agent.ReviewSlug)) return agent.ReviewSlug;

        var source = !string.IsNullOrWhiteSpace(agent.FullName)
            ? agent.FullName
            : (agent.UserName ?? agent.Email ?? "agent").Split('@')[0];
        var baseSlug = CreateSlug(source);
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var candidate = attempt switch
            {
                0 => baseSlug,
                < 10 => $"{baseSlug}-{attempt + 1}",
                _ => $"{baseSlug}-{RandomNumberGenerator.GetHexString(6, lowercase: true)}"
            };
            if (await _context.Users.AnyAsync(user => user.ReviewSlug == candidate, cancellationToken)) continue;
            agent.ReviewSlug = candidate;
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return candidate;
            }
            catch (DbUpdateException)
            {
                // Another agent took the same slug at the same moment; try the next one.
                _context.Entry(agent).Property(user => user.ReviewSlug).CurrentValue = null;
                _context.Entry(agent).Property(user => user.ReviewSlug).IsModified = false;
            }
        }
        throw new InvalidOperationException("Could not create a review link.");
    }

    /// <summary>Latin slug from a name; Georgian letters are transliterated (გიორგი → giorgi).</summary>
    public static string CreateSlug(string value)
    {
        var text = new StringBuilder();
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            text.Append(GeorgianLatin.TryGetValue(character, out var latin) ? latin : character.ToString());
        }
        var normalized = text.ToString().Normalize(NormalizationForm.FormD);
        var slug = new StringBuilder(normalized.Length);
        var previousWasDash = false;
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (character <= 127 && char.IsLetterOrDigit(character))
            {
                slug.Append(char.ToLowerInvariant(character));
                previousWasDash = false;
            }
            else if (!previousWasDash && slug.Length > 0)
            {
                slug.Append('-');
                previousWasDash = true;
            }
        }
        var result = slug.ToString().Trim('-');
        if (result.Length == 0) result = "agent";
        return result.Length <= 60 ? result : result[..60].TrimEnd('-');
    }

    private static readonly Dictionary<char, string> GeorgianLatin = new()
    {
        ['ა'] = "a", ['ბ'] = "b", ['გ'] = "g", ['დ'] = "d", ['ე'] = "e", ['ვ'] = "v", ['ზ'] = "z",
        ['თ'] = "t", ['ი'] = "i", ['კ'] = "k", ['ლ'] = "l", ['მ'] = "m", ['ნ'] = "n", ['ო'] = "o",
        ['პ'] = "p", ['ჟ'] = "zh", ['რ'] = "r", ['ს'] = "s", ['ტ'] = "t", ['უ'] = "u", ['ფ'] = "p",
        ['ქ'] = "k", ['ღ'] = "gh", ['ყ'] = "q", ['შ'] = "sh", ['ჩ'] = "ch", ['ც'] = "ts", ['ძ'] = "dz",
        ['წ'] = "ts", ['ჭ'] = "ch", ['ხ'] = "kh", ['ჯ'] = "j", ['ჰ'] = "h"
    };

    private static string DisplayName(AppUser agent) =>
        !string.IsNullOrWhiteSpace(agent.FullName) ? agent.FullName.Trim() : "Velven agent";

    /// <summary>Trims, removes control characters and (for names) line breaks; empty becomes null.</summary>
    private static string? Clean(string? value, bool singleLine)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            if (character is '\n' && !singleLine) builder.Append(character);
            else if (char.IsControl(character)) builder.Append(' ');
            else builder.Append(character);
        }
        var cleaned = Regex.Replace(builder.ToString(), "\n{3,}", "\n\n").Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }

    /// <summary>Salted hash so the raw IP is never stored.</summary>
    private string HashIp(string ip)
    {
        var salt = _configuration["Jwt:Key"] ?? "velven-reviews";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}|reviews|{ip}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();

    [GeneratedRegex(@"(https?://|www\.|\b[a-z0-9-]+\.(com|ge|net|org|ru|io|xyz|info|biz|link)\b)", RegexOptions.IgnoreCase)]
    private static partial Regex LinkPattern();
}

public class SubmitReviewDto
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? CustomerName { get; set; }
    /// <summary>Random id created once per form; repeated submits with it are stored once.</summary>
    public Guid SubmissionId { get; set; }
    /// <summary>Milliseconds from page load to submit (basic bot check).</summary>
    public int? ElapsedMs { get; set; }
    /// <summary>Hidden honeypot field; must stay empty.</summary>
    public string? Website { get; set; }
}
