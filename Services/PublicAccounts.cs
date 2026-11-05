using Website_API.Data;
using Website_API.Models;

namespace Website_API.Services;

internal static class PublicAccounts
{
    private static readonly string[] StaffRoles =
        ["ADMIN", "MANAGER", "AGENT", "UPLOADER", "CRM-MANAGER", "CRM_MANAGER"];

    internal static IQueryable<AppUser> Users(AppDbContext db) => db.Users.Where(user =>
        !user.IsAgent && !db.UserRoles.Any(membership => membership.UserId == user.Id &&
            db.Roles.Any(role => role.Id == membership.RoleId && StaffRoles.Contains(role.NormalizedName!))));
}
