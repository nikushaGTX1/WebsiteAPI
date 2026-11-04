using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
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
        if (apartment.Price <= 0 && (client.BudgetMin > 0 || client.BudgetMax > 0)) return false;
        if (!MatchesCondition(apartment, client)) return false;
        // Requirements the shared Velven Match check does not cover. An unknown size (0) cannot prove a minimum.
        if (client.MinSize is > 0 && apartment.SizeSquareMeters < (double)client.MinSize.Value) return false;
        return apartment.MaxOccupants is not int occupants || occupants >= client.Adults + client.Children;
    }

    // The upload form stores condition in its existing description tag; there is no separate
    // property condition column. Unknown conditions cannot satisfy a buyer's explicit restriction.
    private static bool MatchesCondition(Apartment apartment, ClientRequirements client)
    {
        if (client.PropertyGoal != "Buy" || client.PropertyConditions.Count == 0 || client.PropertyConditions.Contains("Any")) return true;
        var condition = Regex.Match(apartment.Description, @"(?:^|[|\r\n])\s*Condition:\s*([^|\r\n]+)", RegexOptions.IgnoreCase)
            .Groups[1].Value.Trim().TrimEnd('.').ToLowerInvariant();
        return client.PropertyConditions.Any(code => code switch
        {
            "MoveInReady" => condition is "newly renovated" or "old renovated",
            "Renovated" => condition is "newly renovated" or "old renovated",
            "WhiteFrame" => condition is "white frame" or "white plus",
            "BlackFrame" => condition == "black frame",
            "GreenFrame" => condition == "green frame",
            "NeedsRenovation" => condition is "current renovation" or "repairing",
            _ => false,
        });
    }
}
