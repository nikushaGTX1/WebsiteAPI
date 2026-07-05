using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.Models;

namespace Website_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _context;

    public AdminController(UserManager<AppUser> userManager, AppDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _context.Users.ToListAsync();

        var result = new List<object>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);

            result.Add(new
            {
                user.Id,
                user.UserName,
                user.Email,
                user.FullName,
                user.ProfilePicture,
                user.Bio,
                user.IsAgent,
                Roles = roles
            });
        }

        return Ok(result);
    }

    [HttpGet("user-ids")]
    public async Task<IActionResult> GetUserIds()
    {
        var ids = await _context.Users
            .Select(u => u.Id)
            .ToListAsync();

        return Ok(ids);
    }

    [HttpGet("users/{id}")]
    public async Task<IActionResult> GetUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);

        if (user == null)
            return NotFound(new { message = "User not found" });

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new
        {
            user.Id,
            user.UserName,
            user.Email,
            user.FullName,
            user.ProfilePicture,
            user.Bio,
            user.IsAgent,
            Roles = roles
        });
    }

    [HttpPost("make-agent/{userId}")]
    public async Task<IActionResult> MakeAgent(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
            return NotFound(new { message = "User not found" });

        user.IsAgent = true;

        var updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
            return BadRequest(updateResult.Errors);

        if (!await _userManager.IsInRoleAsync(user, "Agent"))
        {
            await _userManager.AddToRoleAsync(user, "Agent");
        }

        return Ok(new { message = "User is now an agent" });
    }

    [HttpPost("remove-agent/{userId}")]
    public async Task<IActionResult> RemoveAgent(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
            return NotFound(new { message = "User not found" });

        user.IsAgent = false;

        var updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
            return BadRequest(updateResult.Errors);

        if (await _userManager.IsInRoleAsync(user, "Agent"))
        {
            await _userManager.RemoveFromRoleAsync(user, "Agent");
        }

        return Ok(new { message = "User is no longer an agent" });
    }
}