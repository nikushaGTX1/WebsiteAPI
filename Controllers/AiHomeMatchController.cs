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
        [FromQuery] int page = 1,
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

        // Only public listings: one still waiting for approval is never a match.
        var apartments = await ApartmentCatalogue.Published(_context.Apartments.AsNoTracking())
            .ToListAsync(cancellationToken);

        // Hard filters shared with the website: the budget ceiling (with the rental tolerance)
        // and listings that explicitly refuse the user's pets.
        var pets = request.PetTypes.Count > 0
            ? request.PetTypes
            : request.HasPet == true && !string.IsNullOrWhiteSpace(request.PetType) && request.PetType != "None"
                ? [request.PetType]
                : request.HasPet == true ? ["Other"] : [];
        var rent = string.Equals(request.PropertyGoal, "Rent", StringComparison.OrdinalIgnoreCase);
        var allMatches = apartments
            .Where(apartment =>
                RentalBudgetPolicy.IsWithinCeiling(apartment.Price, request.BudgetMax, request.PropertyGoal))
            .Where(apartment => !rent || !PetPolicy.IsProhibited(apartment, pets, request.DogSize))
            .Select(apartment => _scorer.Score(apartment, request))
            .OrderByDescending(match => match.MatchScore)
            .ToList();

        page = Math.Clamp(page, 1, 100000);
        var matches = allMatches.Skip((page - 1) * maxResults).Take(maxResults).ToList();

        return Ok(new HomeMatchResponseDto
        {
            TotalMatches = allMatches.Count,
            Matches = matches
        });
    }
}
