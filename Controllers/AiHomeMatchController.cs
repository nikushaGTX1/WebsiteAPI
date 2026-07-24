using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.DTO;
using Website_API.Services;

namespace Website_API.Controllers;

[ApiController]
[Route("api/ai-home-match")]
public class AiHomeMatchController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly HomeMatchScorer _scorer;

    public AiHomeMatchController(
        AppDbContext context,
        HomeMatchScorer scorer)
    {
        _context = context;
        _scorer = scorer;
    }

    [HttpPost("matches")]
    public async Task<ActionResult<HomeMatchResponseDto>> FindMatches(
        [FromBody] HomeMatchProfileRequest request,
        [FromQuery] int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        maxResults = Math.Clamp(maxResults, 1, 100);

        if (request.BudgetMin < 0)
        {
            return BadRequest(new
            {
                message = "Minimum budget cannot be negative."
            });
        }

        if (request.BudgetMax < request.BudgetMin)
        {
            return BadRequest(new
            {
                message = "Maximum budget cannot be lower than minimum budget."
            });
        }

        if (request.Adults < 1)
        {
            return BadRequest(new
            {
                message = "At least one adult is required."
            });
        }

        var apartments = await _context.Apartments
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var allMatches = apartments
            .Select(apartment => _scorer.Score(apartment, request))
            .OrderByDescending(match => match.MatchScore)
            .ToList();

        var matches = allMatches.Take(maxResults).ToList();

        return Ok(new HomeMatchResponseDto
        {
            TotalMatches = allMatches.Count,
            Matches = matches
        });
    }
}
