using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.DTO;
using Website_API.Models;
using Website_API.Services;

namespace Website_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private static readonly string[] CrmRoles = ["Manager", "Agent", "Uploader"];
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _context;
    private readonly SupabaseStorageService _storageService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        UserManager<AppUser> userManager,
        AppDbContext context,
        SupabaseStorageService storageService,
        ILogger<AdminController> logger)
    {
        _userManager = userManager;
        _context = context;
        _storageService = storageService;
        _logger = logger;
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

    [HttpPut("users/{id}/settings")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateUserSettings(
        string id,
        [FromForm] AdminUpdateUserDto dto,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id);

        if (user == null)
            return NotFound(new { message = "User not found" });

        var oldProfilePicture = user.ProfilePicture;
        string? uploadedProfilePicture = null;
        var profileSaved = false;

        try
        {
            if (dto.ProfilePicture is { Length: > 0 })
            {
                uploadedProfilePicture = await _storageService.UploadImageAsync(
                    dto.ProfilePicture,
                    $"profiles/{user.Id}",
                    cancellationToken);
                user.ProfilePicture = uploadedProfilePicture;
            }

            user.FullName = dto.FullName.Trim();
            user.UserName = dto.UserName.Trim();
            user.Email = dto.Email.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber)
                ? null
                : dto.PhoneNumber.Trim();
            user.Bio = string.IsNullOrWhiteSpace(dto.Bio) ? null : dto.Bio.Trim();

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                user.ProfilePicture = oldProfilePicture;

                if (uploadedProfilePicture is not null)
                    await _storageService.DeleteImageAsync(uploadedProfilePicture, CancellationToken.None);

                return BadRequest(result.Errors);
            }

            profileSaved = true;

            if (uploadedProfilePicture is not null &&
                !string.IsNullOrWhiteSpace(oldProfilePicture) &&
                oldProfilePicture.Contains('/'))
            {
                await _storageService.DeleteImageAsync(oldProfilePicture, cancellationToken);
            }

            return Ok(new { message = "User settings updated" });
        }
        catch
        {
            if (uploadedProfilePicture is not null && !profileSaved)
                await _storageService.DeleteImageAsync(uploadedProfilePicture, CancellationToken.None);

            throw;
        }
    }

    [HttpPut("users/{id}/password")]
    public async Task<IActionResult> ResetUserPassword(
        string id,
        [FromBody] AdminResetPasswordDto dto)
    {
        var user = await _userManager.FindByIdAsync(id);

        if (user == null)
            return NotFound(new { message = "User not found" });

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, dto.NewPassword);

        if (!result.Succeeded)
            return BadRequest(result.Errors);

        await _userManager.UpdateSecurityStampAsync(user);
        return Ok(new { message = "Password reset successfully" });
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(
        string id,
        CancellationToken cancellationToken)
    {
        var currentUserId = _userManager.GetUserId(User);
        if (string.Equals(currentUserId, id, StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                message = "You cannot delete your own account from the admin panel."
            });
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound(new { message = "User not found" });

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
                    "Admin deleted account {UserId}, but its profile image could not be removed.",
                    id);
            }
        }

        return NoContent();
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

public sealed record TransferApartmentsDto(string? FromUserId, string ToUserId, int? ApartmentId);

    /// <summary>
    /// Gives apartments to another agent: every apartment of FromUserId (e.g. an agent who left),
    /// or a single ApartmentId. The listing's agent name and phone follow; for a whole-agent
    /// transfer their open CRM leads and owner submissions move too.
    /// </summary>
    [HttpPost("transfer-apartments")]
    public async Task<IActionResult> TransferApartments([FromBody] TransferApartmentsDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.ToUserId)) return BadRequest(new { message = "Choose the agent who receives the apartments." });
        if (dto.ApartmentId is null && string.IsNullOrWhiteSpace(dto.FromUserId)) return BadRequest(new { message = "Choose whose apartments to move." });
        if (dto.FromUserId == dto.ToUserId) return BadRequest(new { message = "Choose a different agent to receive the apartments." });
        var target = await _userManager.FindByIdAsync(dto.ToUserId);
        if (target is null) return BadRequest(new { message = "The agent who should receive the apartments was not found." });
        var targetIsStaff = target.IsAgent ||
            await _userManager.IsInRoleAsync(target, "Agent") ||
            await _userManager.IsInRoleAsync(target, "Manager") ||
            await _userManager.IsInRoleAsync(target, "Admin");
        if (!targetIsStaff) return BadRequest(new { message = "Apartments can only be given to an agent, manager or admin." });

        var query = _context.Apartments.AsQueryable();
        query = dto.ApartmentId is int id ? query.Where(x => x.Id == id) : query.Where(x => x.UploadedByUserId == dto.FromUserId);
        var apartments = await query.ToListAsync(cancellationToken);
        if (dto.ApartmentId is not null && apartments.Count == 0) return NotFound(new { message = "Apartment not found" });
        var targetName = target.FullName ?? target.UserName;
        foreach (var apartment in apartments)
        {
            apartment.UploadedByUserId = target.Id;
            apartment.AgentName = targetName;
            if (!string.IsNullOrWhiteSpace(target.PhoneNumber)) apartment.AgentPhoneNumber = target.PhoneNumber;
        }
        var leadsMoved = 0;
        if (dto.ApartmentId is null)
        {
            var leads = await _context.CrmLeads.Where(l => l.AssignedAgentId == dto.FromUserId && l.ClosedAt == null).ToListAsync(cancellationToken);
            foreach (var lead in leads) lead.AssignedAgentId = target.Id;
            leadsMoved = leads.Count;
            var submissions = await _context.OwnerSubmissions.Where(x => x.AgentUserId == dto.FromUserId && x.Status != "sent" && x.Status != "rejected").ToListAsync(cancellationToken);
            foreach (var submission in submissions) submission.AgentUserId = target.Id;
        }
        await _context.SaveChangesAsync(cancellationToken);
        await HttpContext.RequestServices.GetRequiredService<IOutputCacheStore>().EvictByTagAsync("apartments", cancellationToken);
        return Ok(new { movedApartments = apartments.Count, movedLeads = leadsMoved, toName = targetName });
    }

    /// <summary>How much work an agent leaves behind, shown before removing them.</summary>
    [HttpGet("agents/{userId}/holdings")]
    public async Task<IActionResult> AgentHoldings(string userId, CancellationToken cancellationToken)
    {
        var apartments = await _context.Apartments.CountAsync(a => a.UploadedByUserId == userId, cancellationToken);
        var openLeads = await _context.CrmLeads.CountAsync(l => l.AssignedAgentId == userId && l.ClosedAt == null, cancellationToken);
        return Ok(new { apartments, openLeads });
    }

    /// <summary>
    /// Removes the agent role. With ?transferTo=, their website apartments (and the agent
    /// name/phone shown on them), open CRM leads and owner submissions go to that person first.
    /// </summary>
    [HttpPost("remove-agent/{userId}")]
    public async Task<IActionResult> RemoveAgent(string userId, [FromQuery] string? transferTo, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
            return NotFound(new { message = "User not found" });

        var moved = 0;
        if (string.IsNullOrWhiteSpace(transferTo) &&
            (await _context.Apartments.AnyAsync(a => a.UploadedByUserId == userId, cancellationToken) ||
             await _context.CrmLeads.AnyAsync(l => l.AssignedAgentId == userId && l.ClosedAt == null, cancellationToken)))
        {
            return Conflict(new { message = "This agent still has apartments or open leads. Choose who receives them." });
        }
        if (!string.IsNullOrWhiteSpace(transferTo))
        {
            if (transferTo == userId) return BadRequest(new { message = "Choose a different agent to receive the apartments." });
            var target = await _userManager.FindByIdAsync(transferTo);
            if (target is null) return BadRequest(new { message = "The agent who should receive the apartments was not found." });
            var targetIsStaff = target.IsAgent ||
                await _userManager.IsInRoleAsync(target, "Agent") ||
                await _userManager.IsInRoleAsync(target, "Manager") ||
                await _userManager.IsInRoleAsync(target, "Admin");
            if (!targetIsStaff) return BadRequest(new { message = "Apartments can only be given to an agent, manager or admin." });

            var targetName = target.FullName ?? target.UserName;
            var apartments = await _context.Apartments.Where(a => a.UploadedByUserId == userId).ToListAsync(cancellationToken);
            foreach (var apartment in apartments)
            {
                apartment.UploadedByUserId = target.Id;
                apartment.AgentName = targetName;
                if (!string.IsNullOrWhiteSpace(target.PhoneNumber)) apartment.AgentPhoneNumber = target.PhoneNumber;
            }
            moved = apartments.Count;
            var leads = await _context.CrmLeads.Where(l => l.AssignedAgentId == userId && l.ClosedAt == null).ToListAsync(cancellationToken);
            foreach (var lead in leads) lead.AssignedAgentId = target.Id;
            var submissions = await _context.OwnerSubmissions.Where(x => x.AgentUserId == userId && x.Status != "sent" && x.Status != "rejected").ToListAsync(cancellationToken);
            foreach (var submission in submissions) submission.AgentUserId = target.Id;
            await _context.SaveChangesAsync(cancellationToken);
            await HttpContext.RequestServices.GetRequiredService<IOutputCacheStore>().EvictByTagAsync("apartments", cancellationToken);
        }

        user.IsAgent = false;

        var updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
            return BadRequest(updateResult.Errors);

        if (await _userManager.IsInRoleAsync(user, "Agent"))
        {
            await _userManager.RemoveFromRoleAsync(user, "Agent");
        }

        return Ok(new { message = "User is no longer an agent", movedApartments = moved });
    }

    [HttpPut("users/{id}/crm-role")]
    public async Task<IActionResult> SetCrmRole(
        string id,
        [FromBody] SetCrmRoleDto dto)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound(new { message = "User not found" });
        if (await _userManager.IsInRoleAsync(user, "Admin"))
        {
            return BadRequest(new
            {
                message = "The administrator's CRM access cannot be changed."
            });
        }

        var requestedRole = CrmRoles.FirstOrDefault(role =>
            string.Equals(role, dto.Role?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (requestedRole is null)
        {
            return BadRequest(new
            {
                message = "CRM role must be Manager, Agent, or Uploader."
            });
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles
            .Where(role => CrmRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (rolesToRemove.Length > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(
                user,
                rolesToRemove);
            if (!removeResult.Succeeded)
                return BadRequest(removeResult.Errors);
        }

        var addResult = await _userManager.AddToRoleAsync(user, requestedRole);
        if (!addResult.Succeeded)
            return BadRequest(addResult.Errors);

        user.IsAgent = requestedRole == "Agent";
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return BadRequest(updateResult.Errors);

        return Ok(new
        {
            message = $"CRM role changed to {requestedRole}.",
            role = requestedRole
        });
    }
}
