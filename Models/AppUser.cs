using Microsoft.AspNetCore.Identity;

namespace Website_API.Models;

public class AppUser : IdentityUser
{
    public string? FullName { get; set; }

    public string? ProfilePicture { get; set; }

    public bool IsAgent { get; set; }

    public string? Bio { get; set; }

    /// <summary>Permanent public review link slug (velven.ge/review/{slug}); set once, never changed.</summary>
    public string? ReviewSlug { get; set; }

    /// <summary>Languages the agent speaks, as comma-separated codes ("ka,en,ru"). Set by admins/managers.</summary>
    public string? SpokenLanguages { get; set; }

    public List<FavoriteApartment> FavoriteApartments { get; set; } = [];
}
