using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Website_API.DTO;
using Website_API.Models;
using Website_API.Services;

namespace Website_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private const int ProfilePictureUrlLifetimeSeconds = 604800;

    private readonly UserManager<AppUser> _userManager;
    private readonly IConfiguration _config;
    private readonly SupabaseStorageService _storageService;
    private readonly ResendEmailService _email;

    // Marks accounts created after email verification was introduced; older accounts have no
    // such claim, so they keep signing in without being locked out.
    private const string VerificationRequiredClaim = "email_verification_required";
    private const string VerifyPurpose = "VerifyEmail";
    private const string ResetPurpose = "ResetPassword";
    private const string ChangePurpose = "ChangePassword";

    public AuthController(
        UserManager<AppUser> userManager,
        IConfiguration config,
        SupabaseStorageService storageService,
        ResendEmailService email)
    {
        _userManager = userManager;
        _config = config;
        _storageService = storageService;
        _email = email;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        var existingEmail = await _userManager.FindByEmailAsync(dto.Email);

        if (existingEmail != null)
            return BadRequest(new { message = "Email already exists" });

        var user = new AppUser
        {
            UserName = dto.UserName,
            Email = dto.Email,
            FullName = dto.FullName,
            PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber)
                ? null
                : dto.PhoneNumber.Trim()
        };

        var result = await _userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
            return BadRequest(result.Errors);

        await _userManager.AddToRoleAsync(user, "User");
        await _userManager.AddClaimAsync(user, new Claim(VerificationRequiredClaim, "true"));
        var sent = await SendCodeAsync(user, VerifyPurpose, HttpContext.RequestAborted);

        return Ok(new { message = "Registered successfully", needsVerification = true, emailSent = sent, email = user.Email });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginDto dto,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);

        if (user == null)
            return Unauthorized(new { message = "Invalid email or password" });

        var passwordOk = await _userManager.CheckPasswordAsync(user, dto.Password);

        if (!passwordOk)
            return Unauthorized(new { message = "Invalid email or password" });

        if (!user.EmailConfirmed && await RequiresVerificationAsync(user))
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Please verify your email address first.",
                needsVerification = true,
                email = user.Email,
            });

        var roles = await _userManager.GetRolesAsync(user);
        var token = GenerateToken(user, roles);
        var profilePicture =
            await _storageService.CreateSignedUrlAsync(
                user.ProfilePicture,
                ProfilePictureUrlLifetimeSeconds,
                cancellationToken);

        return Ok(new
        {
            token,
            user = new
            {
                user.Id,
                user.UserName,
                user.Email,
                user.FullName,
                user.PhoneNumber,
                ProfilePicture = profilePicture,
                ProfilePicturePath = user.ProfilePicture,
                user.Bio,
                user.IsAgent,
                roles
            }
        });
    }

    // ---------- Email verification ----------

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email.Trim());
        if (user == null || !await IsCodeValidAsync(user, VerifyPurpose, dto.Code))
            return BadRequest(new { message = "The code is incorrect or has expired." });

        user.EmailConfirmed = true;
        await _userManager.UpdateAsync(user);
        await RemoveVerificationClaimAsync(user);
        return Ok(new { message = "Email verified." });
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification(EmailOnlyDto dto, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email.Trim());
        // Same response whether or not the account exists, so emails can't be probed.
        if (user != null && !user.EmailConfirmed)
            await SendCodeAsync(user, VerifyPurpose, cancellationToken);
        return Ok(new { message = "If the account exists, a new code has been sent." });
    }

    // ---------- Forgot password (signed out) ----------

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(EmailOnlyDto dto, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email.Trim());
        if (user != null)
            await SendCodeAsync(user, ResetPurpose, cancellationToken);
        return Ok(new { message = "If the account exists, a reset code has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordWithCodeDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email.Trim());
        if (user == null || !await IsCodeValidAsync(user, ResetPurpose, dto.Code))
            return BadRequest(new { message = "The code is incorrect or has expired." });

        var result = await SetNewPasswordAsync(user, dto.NewPassword);
        if (!result.Succeeded) return BadRequest(result.Errors);

        // Receiving the code proves the inbox belongs to the user.
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);
            await RemoveVerificationClaimAsync(user);
        }
        return Ok(new { message = "Password changed. You can sign in now." });
    }

    // ---------- Change password (signed in) ----------

    [Authorize]
    [HttpPost("change-password/request-code")]
    public async Task<IActionResult> RequestChangePasswordCode(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync();
        if (user == null) return Unauthorized();
        var sent = await SendCodeAsync(user, ChangePurpose, cancellationToken);
        return sent
            ? Ok(new { message = "A code has been sent to your email." })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Could not send the email right now." });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordWithCodeDto dto)
    {
        var user = await CurrentUserAsync();
        if (user == null) return Unauthorized();
        if (!await IsCodeValidAsync(user, ChangePurpose, dto.Code))
            return BadRequest(new { message = "The code is incorrect or has expired." });

        var result = await SetNewPasswordAsync(user, dto.NewPassword);
        return result.Succeeded ? Ok(new { message = "Password changed." }) : BadRequest(result.Errors);
    }

    // ---------- Helpers ----------

    private async Task<AppUser?> CurrentUserAsync()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return id == null ? null : await _userManager.FindByIdAsync(id);
    }

    private async Task<bool> RequiresVerificationAsync(AppUser user) =>
        (await _userManager.GetClaimsAsync(user)).Any((claim) => claim.Type == VerificationRequiredClaim);

    private async Task RemoveVerificationClaimAsync(AppUser user)
    {
        var claims = (await _userManager.GetClaimsAsync(user)).Where((claim) => claim.Type == VerificationRequiredClaim).ToList();
        if (claims.Count > 0) await _userManager.RemoveClaimsAsync(user, claims);
    }

    /// <summary>6-digit time-based code from Identity's email token provider (valid for a few minutes).</summary>
    private async Task<bool> SendCodeAsync(AppUser user, string purpose, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(user.Email)) return false;
        var code = await _userManager.GenerateUserTokenAsync(user, TokenOptions.DefaultEmailProvider, purpose);
        var (subject, heading, intro) = purpose switch
        {
            VerifyPurpose => ("Your Velven verification code", "Verify your email", "Enter this code on Velven to finish creating your account."),
            ResetPurpose => ("Your Velven password reset code", "Reset your password", "Enter this code on Velven to choose a new password."),
            _ => ("Your Velven security code", "Confirm password change", "Enter this code on Velven to change your password."),
        };
        return await _email.SendCodeAsync(user.Email, subject, heading, intro, code, cancellationToken);
    }

    private Task<bool> IsCodeValidAsync(AppUser user, string purpose, string code) =>
        _userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultEmailProvider, purpose, (code ?? "").Trim());

    private async Task<IdentityResult> SetNewPasswordAsync(AppUser user, string newPassword)
    {
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        return await _userManager.ResetPasswordAsync(user, resetToken, newPassword);
    }

    private string GenerateToken(AppUser user, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.UserName ?? ""),
            new Claim(ClaimTypes.Email, user.Email ?? "")
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!)
        );

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
