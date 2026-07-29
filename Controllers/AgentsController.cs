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
                u.PhoneNumber,
                ProfilePicturePath = u.ProfilePicture,
                AverageRating = _context.AgentRatings
                    .Where(r => r.AgentId == u.Id)
                    .Average(r => (double?)r.Stars) ?? 0,
                RatingCount = _context.AgentRatings
                    .Count(r => r.AgentId == u.Id)
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
                agent.PhoneNumber,
                ProfilePicture = profilePicture,
                ProfilePictureUrl = profilePicture,
                agent.ProfilePicturePath,
                agent.AverageRating,
                agent.RatingCount
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
                u.PhoneNumber,
                ProfilePicturePath = u.ProfilePicture,
                AverageRating = _context.AgentRatings
                    .Where(r => r.AgentId == u.Id)
                    .Average(r => (double?)r.Stars) ?? 0,
                RatingCount = _context.AgentRatings
                    .Count(r => r.AgentId == u.Id),
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
            agent.PhoneNumber,
            ProfilePicture = profilePicture,
            ProfilePictureUrl = profilePicture,
            agent.ProfilePicturePath,
            agent.AverageRating,
            agent.RatingCount,
            agent.Ratings
        });
    }

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
}
