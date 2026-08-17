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
    private const int ProfilePictureUrlLifetimeSeconds = 604800;

    private readonly UserManager<AppUser> _userManager;
    private readonly SupabaseStorageService _storageService;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        UserManager<AppUser> userManager,
        SupabaseStorageService storageService,
        ILogger<ProfileController> logger)
    {
        _userManager = userManager;
        _storageService = storageService;
        _logger = logger;
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
                ProfilePictureUrlLifetimeSeconds,
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
            ProfilePicturePath = user.ProfilePicture,
            user.IsAgent,
            roles
        });
    }

    [HttpPut("settings")]
    [Consumes("multipart/form-data")]
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
                    ProfilePictureUrlLifetimeSeconds,
                    cancellationToken);

            return Ok(new
            {
                message = "Profile updated successfully",
                profilePicture,
                profilePicturePath = user.ProfilePicture,
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

    [HttpPut("picture")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdatePicture(
        [FromForm] ProfilePictureDto dto,
        CancellationToken cancellationToken)
    {
        var user = await GetCurrentUser();

        if (user == null)
            return Unauthorized();

        return await UpdateSettings(
            new UpdateProfileDto
            {
                Bio = user.Bio,
                ProfilePicture = dto.ProfilePicture
            },
            cancellationToken);
    }

    [HttpDelete("account")]
    public async Task<IActionResult> DeleteAccount(
        [FromBody] DeleteAccountDto dto,
        CancellationToken cancellationToken)
    {
        var user = await GetCurrentUser();
        if (user is null)
            return Unauthorized();

        if (!await _userManager.CheckPasswordAsync(user, dto.Password))
        {
            return BadRequest(new
            {
                message = "The password you entered is incorrect."
            });
        }

        var profilePicture = user.ProfilePicture;
        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        if (!string.IsNullOrWhiteSpace(profilePicture) &&
            profilePicture.Contains('/'))
        {
            try
            {
                await _storageService.DeleteImageAsync(
                    profilePicture,
                    cancellationToken);
            }
            catch (Exception error)
            {
                _logger.LogWarning(
                    error,
                    "Account {UserId} was deleted but its profile image could not be removed.",
                    user.Id);
            }
        }

        return NoContent();
    }

    private async Task<AppUser?> GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId == null)
            return null;

        return await _userManager.FindByIdAsync(userId);
    }
}
