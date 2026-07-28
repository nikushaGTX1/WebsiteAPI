using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Website_API.DTO;
using Website_API.Models;
using Website_API.Services;

namespace Website_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SupabaseStorageService _storageService;

    public ProfileController(
        UserManager<AppUser> userManager,
        SupabaseStorageService storageService)
    {
        _userManager = userManager;
        _storageService = storageService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(
        CancellationToken cancellationToken)
    {
        var user = await GetCurrentUser();

        if (user == null)
            return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        var profilePicture =
            await _storageService.CreateSignedUrlAsync(
                user.ProfilePicture,
                3600,
                cancellationToken);

        return Ok(new
        {
            user.Id,
            user.UserName,
            user.Email,
            user.FullName,
            user.Bio,
            user.PhoneNumber,
            ProfilePicture = profilePicture,
            user.IsAgent,
            roles
        });
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(
        [FromForm] UpdateProfileDto dto,
        CancellationToken cancellationToken)
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

        var oldProfilePicture = user.ProfilePicture;
        string? uploadedProfilePicture = null;
        var profileSaved = false;

        try
        {
            if (dto.ProfilePicture is not null &&
                dto.ProfilePicture.Length > 0)
            {
                uploadedProfilePicture =
                    await _storageService.UploadImageAsync(
                        dto.ProfilePicture,
                        $"profiles/{user.Id}",
                        cancellationToken);

                user.ProfilePicture = uploadedProfilePicture;
            }

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                user.ProfilePicture = oldProfilePicture;

                if (uploadedProfilePicture is not null)
                {
                    await _storageService.DeleteImageAsync(
                        uploadedProfilePicture,
                        CancellationToken.None);
                }

                return BadRequest(result.Errors);
            }

            profileSaved = true;

            if (uploadedProfilePicture is not null &&
                !string.IsNullOrWhiteSpace(oldProfilePicture) &&
                oldProfilePicture.Contains('/'))
            {
                await _storageService.DeleteImageAsync(
                    oldProfilePicture,
                    cancellationToken);
            }

            var profilePicture =
                await _storageService.CreateSignedUrlAsync(
                    user.ProfilePicture,
                    3600,
                    cancellationToken);

            return Ok(new
            {
                message = "Profile updated successfully",
                profilePicture,
                phoneNumber = user.PhoneNumber
            });
        }
        catch
        {
            if (uploadedProfilePicture is not null &&
                !profileSaved)
            {
                await _storageService.DeleteImageAsync(
                    uploadedProfilePicture,
                    CancellationToken.None);
            }

            throw;
        }
    }

    private async Task<AppUser?> GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId == null)
            return null;

        return await _userManager.FindByIdAsync(userId);
    }
}
