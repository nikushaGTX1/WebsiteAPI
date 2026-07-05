using Microsoft.AspNetCore.Identity;

namespace Website_API.Models;

public class AppUser : IdentityUser
{
    public string? FullName { get; set; }

    public string? ProfilePicture { get; set; }

    public bool IsAgent { get; set; }

    public string? Bio { get; set; }
}