using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.Models;
using Website_API.Services;

namespace Website_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _context;
    private readonly SupabaseStorageService _storageService;

    public AdminController(
        UserManager<AppUser> userManager,
        AppDbContext context,
        SupabaseStorageService storageService)
    {
        _userManager = userManager;
        _context = context;
        _storageService = storageService;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _context.Users
            .AsNoTracking()
            .ToListAsync();

        var roleRows = await (
            from userRole in _context.UserRoles.AsNoTracking()
            join role in _context.Roles.AsNoTracking()
                on userRole.RoleId equals role.Id
            select new
            {
                userRole.UserId,
                RoleName = role.Name!
            })
            .ToListAsync();

        var rolesByUser = roleRows
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(row => row.RoleName)
                    .ToList());

        var result = await Task.WhenAll(users.Select(async user =>
        {
            rolesByUser.TryGetValue(user.Id, out var roles);
            var profilePicture =
                await _storageService.CreateSignedUrlAsync(
                    user.ProfilePicture,
                    604800,
                    HttpContext.RequestAborted);

            return new
            {
                user.Id,
                user.UserName,
                user.Email,
                user.FullName,
                ProfilePicture = profilePicture,
                ProfilePictureUrl = profilePicture,
                ProfilePicturePath = user.ProfilePicture,
                user.Bio,
                user.IsAgent,
                Roles = roles ?? []
            };
        }));

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
        var profilePicture =
            await _storageService.CreateSignedUrlAsync(
                user.ProfilePicture,
                604800,
                HttpContext.RequestAborted);

        return Ok(new
        {
            user.Id,
            user.UserName,
            user.Email,
            user.FullName,
            ProfilePicture = profilePicture,
            ProfilePictureUrl = profilePicture,
            ProfilePicturePath = user.ProfilePicture,
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
