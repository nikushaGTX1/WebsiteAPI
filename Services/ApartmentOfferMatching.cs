using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.DTO;
using Website_API.Models;

namespace Website_API.Services;

public static class ClientBoardEligibility
{
    /// <summary>
    /// The only clients the public may see or offer to: agent intake-link profiles that are Active,
    /// consented to publication and still assigned to staff. Self-serve Velven Match searches live
    /// in SavedSearches and are not ClientProfiles, so they can never pass this query.
    /// </summary>
    public static IQueryable<ClientProfile> PublicProfiles(AppDbContext db) => db.ClientProfiles.Where(x =>
        x.Status == "Active" && x.PublishConsentAt != null &&
        db.UserRoles.Any(ur => ur.UserId == x.AgentUserId && db.Roles.Any(role => role.Id == ur.RoleId &&
            (role.Name == "Admin" || role.Name == "Manager" || role.Name == "Agent"))));
}

public static class ApartmentOfferMatching
{
    /// <summary>
    /// Mandatory filters only: anything short of an exact match, including details the listing leaves
    /// unconfirmed, is excluded. Top priorities and lifestyle answers are optional and never decide this.
    /// </summary>
    public static bool IsCompatible(Apartment apartment, ClientRequirements client, DateTime today)
    {
        if (HomeMatchRequirements.Evaluate(apartment, client, today) != RequirementStatus.Exact) return false;
        // Requirements the shared Velven Match check does not cover. An unknown size (0) cannot prove a minimum.
        if (client.MinSize is > 0 && apartment.SizeSquareMeters < (double)client.MinSize.Value) return false;
        return apartment.MaxOccupants is not int occupants || occupants >= client.Adults + client.Children;
    }
}
