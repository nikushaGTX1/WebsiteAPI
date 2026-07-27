using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Website_API.DTO;
using Website_API.Models;

namespace Website_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IWebHostEnvironment _environment;

    public ProfileController(
        UserManager<AppUser> userManager,
        IWebHostEnvironment environment)
    {
        _userManager = userManager;
        _environment = environment;
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await GetCurrentUser();

        if (user == null)
            return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new
        {
            user.Id,
            user.UserName,
            user.Email,
            user.FullName,
            user.Bio,
            user.PhoneNumber,
            user.ProfilePicture,
            user.IsAgent,
            roles
        });
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromForm] UpdateProfileDto dto)
    {
        var user = await GetCurrentUser();

        if (user == null)
            return Unauthorized();

        if (!string.IsNullOrWhiteSpace(dto.FullName))
            user.FullName = dto.FullName;

        user.Bio = dto.Bio;

        if (dto.PhoneNumber is not null)
        {
            user.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber)
                ? null
                : dto.PhoneNumber.Trim();
        }

        if (dto.ProfilePicture != null)
        {
            var webRootPath = _environment.WebRootPath;

            if (string.IsNullOrWhiteSpace(webRootPath))
            {
                webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            }

            var uploadsFolder = Path.Combine(
                webRootPath,
                "uploads",
                "profiles"
            );

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            if (!string.IsNullOrEmpty(user.ProfilePicture))
            {
                var oldFile = Path.Combine(uploadsFolder, user.ProfilePicture);

                if (System.IO.File.Exists(oldFile))
                    System.IO.File.Delete(oldFile);
            }

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(dto.ProfilePicture.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await dto.ProfilePicture.CopyToAsync(stream);
            }

            user.ProfilePicture = fileName;
        }

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
            return BadRequest(result.Errors);

        return Ok(new
        {
            message = "Profile updated successfully",
            profilePicture = user.ProfilePicture,
            phoneNumber = user.PhoneNumber
        });
    }

    private async Task<AppUser?> GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId == null)
            return null;

        return await _userManager.FindByIdAsync(userId);
    }
}
