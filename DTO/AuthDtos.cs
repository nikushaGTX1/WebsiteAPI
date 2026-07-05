using Microsoft.AspNetCore.Http;

namespace Website_API.DTO;

public class RegisterDto
{
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string? FullName { get; set; }
}

public class LoginDto
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class UpdateProfileDto
{
    public string? FullName { get; set; }

    public string? Bio { get; set; }

    // Uploaded image
    public IFormFile? ProfilePicture { get; set; }
}

public class RatingDto
{
    public int Stars { get; set; }

    public string? Comment { get; set; }
}