using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.DTO;
using Website_API.Models;

namespace Website_API.Services;

// Uses the field names of the existing owner form. Only these fields can become listing data.
public sealed class SubmissionProperty
{
    public string DealType { get; set; } = "For Rent";
    public string City { get; set; } = "Tbilisi";
    public string Location { get; set; } = "";
    public string Street { get; set; } = "";
    public long? StreetId { get; set; }
    public string StreetNumber { get; set; } = "";
    public decimal TotalPrice { get; set; }
    public string Currency { get; set; } = "$";
    public double Area { get; set; }
    public int Rooms { get; set; }
    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }
    public int? Floor { get; set; }
    public int? TotalFloors { get; set; }
    public string RealEstateType { get; set; } = "Apartment";
    public string BuildingType { get; set; } = "";
    public string Condition { get; set; } = "";
    public bool IsFurnished { get; set; }
    public bool IsPetFriendly { get; set; }
    public bool HasParking { get; set; }
    public bool HasElevator { get; set; }
    public bool HasBalcony { get; set; }
    public bool HasAirConditioning { get; set; }
    public bool HasDishwasher { get; set; }
    public bool HasBathtub { get; set; }
    public bool HasView { get; set; }
    public bool IsQuietStreet { get; set; }
    public string AvailableFrom { get; set; } = "";
    public string MinimumRentalPeriod { get; set; } = "";
    public int? MaxOccupants { get; set; }
    public string Description { get; set; } = "";
    public double? PropertyLatitude { get; set; }
    public double? PropertyLongitude { get; set; }

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static SubmissionProperty? Read(string json) { try { return JsonSerializer.Deserialize<SubmissionProperty>(json, Json); } catch (JsonException) { return null; } }
    public string? Validate(string ownerName, string ownerPhone)
    {
        if (new[] { DealType, City, Location, Street, StreetNumber, Currency, RealEstateType, BuildingType, Condition, AvailableFrom, MinimumRentalPeriod, Description, ownerName, ownerPhone }.Any(x => x is null)) return "შეავსეთ სავალდებულო ველები.";
        if (ownerName.Trim().Length is < 2 or > 120 || !ClientRequirements.ValidPhone(ownerPhone)) return "შეიყვანეთ მესაკუთრის სახელი და სწორი ტელეფონის ნომერი.";
        if (DealType is not ("For Rent" or "For Sale") || Currency is not ("$" or "USD")) return "აირჩიეთ ქირა ან გაყიდვა და მიუთითეთ ფასი აშშ დოლარში.";
        if (City.Trim().Length is < 2 or > 100 || Location.Trim().Length is < 2 or > 100 || Street.Trim().Length is < 2 or > 200 || StreetNumber.Length > 30) return "შეავსეთ ქალაქი, უბანი და მისამართი.";
        if (TotalPrice is <= 0 or > 1_000_000_000 || !double.IsFinite(Area) || Area is < 5 or > 10000 || Rooms is < 1 or > 50 || Bedrooms < 0 || Bedrooms > Rooms || Bathrooms is < 0 or > 20) return "შეამოწმეთ ფასი, ფართობი და ოთახების რაოდენობა.";
        if (Floor is null or < 0 or > 200 || TotalFloors is null or < 1 or > 200 || Floor > TotalFloors) return "მიუთითეთ სწორი სართული და სართულების რაოდენობა.";
        if (RealEstateType is not ("Apartment" or "Private house" or "Country house") || BuildingType is not ("Old building" or "New building" or "Under construction")) return "აირჩიეთ საცხოვრებელი ქონებისა და შენობის ტიპი.";
        if (Condition is not ("Newly Renovated" or "Old renovated" or "Current renovation" or "Repairing" or "White frame" or "Black frame" or "Green frame" or "White Plus")) return "აირჩიეთ ბინის მდგომარეობა.";
        if (!DateOnly.TryParseExact(AvailableFrom, "yyyy-MM-dd", out _) || Description.Trim().Length is < 5 or > 5000 || MaxOccupants is < 1 or > 50 || MinimumRentalPeriod.Length > 80) return "შეავსეთ ხელმისაწვდომობის თარიღი და აღწერა.";
        if (PropertyLatitude is < -90 or > 90 || PropertyLongitude is < -180 or > 180 || PropertyLatitude is double lat && !double.IsFinite(lat) || PropertyLongitude is double lng && !double.IsFinite(lng)) return "მდებარეობა არასწორია.";
        return null;
    }

    public void Apply(Apartment apartment, string ownerName, string ownerPhone)
    {
        apartment.Title = $"{(DealType == "For Rent" ? "ქირავდება" : "იყიდება")} {Bedrooms}-საძინებლიანი {(RealEstateType == "Apartment" ? "ბინა" : "სახლი")} — {Location.Trim()}";
        // Canonical tags are consumed by the existing matching engine. Owner free text is
        // kept after the tags so it cannot override deal/condition/pet restrictions.
        apartment.Description = $"Deal: {DealType} | Condition: {Condition} | Pet-friendly: {(IsPetFriendly ? "yes" : "no")} | Building: {BuildingType}\n{Description.Trim()}";
        apartment.Price = TotalPrice; apartment.City = City.Trim(); apartment.District = Location.Trim();
        apartment.Street = Street.Trim(); apartment.BuildingNumber = StreetNumber.Trim(); apartment.StreetId = StreetId;
        apartment.Address = $"{Street.Trim()} {StreetNumber.Trim()}, {Location.Trim()}, {City.Trim()}";
        apartment.SizeSquareMeters = Area; apartment.Rooms = Rooms; apartment.Bedrooms = Bedrooms; apartment.Bathrooms = Bathrooms;
        apartment.Floor = Floor ?? 0; apartment.TotalFloors = TotalFloors ?? 1;
        apartment.OwnerName = ownerName.Trim(); apartment.OwnerPhoneNumber = ownerPhone.Trim();
        apartment.IsFurnished = IsFurnished; apartment.IsPetFriendly = IsPetFriendly; apartment.HasParking = HasParking;
        apartment.HasElevator = HasElevator; apartment.HasBalcony = HasBalcony; apartment.HasAirConditioning = HasAirConditioning;
        apartment.HasDishwasher = HasDishwasher; apartment.HasBathtub = HasBathtub; apartment.HasView = HasView; apartment.NoiseLevel = IsQuietStreet ? "Quiet" : "";
        apartment.AvailableFrom = DateOnly.ParseExact(AvailableFrom, "yyyy-MM-dd").ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        apartment.MinimumRentalPeriod = MinimumRentalPeriod; apartment.MaxOccupants = MaxOccupants;
        apartment.PropertyLatitude = PropertyLatitude; apartment.PropertyLongitude = PropertyLongitude;
    }
}

public sealed class ApartmentSubmissionService(AppDbContext db, ListingInterestService interests)
{
    public static readonly string[] VerificationKeys = ["reviewAvailability", "reviewDetails", "reviewTerms", "reviewOwnerApproval"];
    public static bool Verified(string json)
    {
        try { var checks = JsonSerializer.Deserialize<Dictionary<string, bool>>(json); return checks is not null && VerificationKeys.All(key => checks.GetValueOrDefault(key)); }
        catch (JsonException) { return false; }
    }
    public void History(OwnerSubmission row, string action, string? actor = null, int matched = 0) => db.SubmissionEvents.Add(new SubmissionEvent
        { OwnerSubmissionId = row.Id, Submission = row, Action = action, ActorUserId = actor, MatchedClients = matched });

    public async Task<int> DistributeAsync(OwnerSubmission row, string actor, CancellationToken ct)
    {
        var apartment = await db.Apartments.SingleAsync(x => x.Id == row.PublishedApartmentId, ct);
        var boardClients = await db.ClientProfiles.AsNoTracking().Where(x => x.Status == "Active" &&
            db.UserRoles.Any(ur => ur.UserId == x.AgentUserId && db.Roles.Any(r => r.Id == ur.RoleId && (r.NormalizedName == "AGENT" || r.NormalizedName == "ADMIN" || r.NormalizedName == "MANAGER"))))
            .ToListAsync(ct);
        var count = 0;
        foreach (var candidate in boardClients)
        {
            if (!ApartmentSubmissionMatching.IsCompatible(apartment, ClientRequirements.Read(candidate.ProfileJson), DateTime.UtcNow)) continue;
            db.SubmissionClientMatches.Add(new SubmissionClientMatch { OwnerSubmissionId = row.Id, ClientProfileId = candidate.Id });
            count++;
        }
        // Include old saved searches as well as new ones. SEND is an explicit staff action,
        // so the background worker's "published since criteria changed" rule does not apply.
        var searches = await db.SavedSearches.Include(x => x.User).Where(search =>
            db.UserRoles.Any(ur => ur.UserId == search.UserId && db.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == "CLIENT")) &&
            !db.UserRoles.Any(ur => ur.UserId == search.UserId && db.Roles.Any(r => r.Id == ur.RoleId &&
                (r.NormalizedName == "ADMIN" || r.NormalizedName == "MANAGER" || r.NormalizedName == "AGENT" || r.NormalizedName == "UPLOADER" || r.NormalizedName == "OWNER" || r.NormalizedName == "CRM-MANAGER" || r.NormalizedName == "CRM_MANAGER"))))
            .OrderByDescending(search => search.NotificationsEnabled).ThenBy(search => search.Id).ToListAsync(ct);
        var users = new HashSet<string>();
        foreach (var search in searches)
        {
            if (!SavedSearchProfile.IsValid(search.ProfileJson) || !ApartmentSubmissionMatching.IsCompatible(apartment, ClientRequirements.Read(search.ProfileJson), DateTime.UtcNow)) continue;
            var firstForClient = users.Add(search.UserId);
            if (!await db.SavedSearchMatches.AnyAsync(x => x.ApartmentId == apartment.Id && x.SavedSearchId == search.Id, ct))
                db.SavedSearchMatches.Add(new SavedSearchMatch { ApartmentId = apartment.Id, SavedSearchId = search.Id });
            if (firstForClient && search.NotificationsEnabled && (search.User.MatchAlertsInApp || search.User.MatchAlertsEmail) &&
                !await db.ClientNotifications.AnyAsync(x => x.ApartmentId == apartment.Id && x.SavedSearchId == search.Id, ct))
                db.ClientNotifications.Add(new ClientNotification { ApartmentId = apartment.Id, SavedSearchId = search.Id, UserId = search.UserId,
                    ReadAt = search.User.MatchAlertsInApp ? null : DateTime.UtcNow, EmailPending = search.User.MatchAlertsEmail && !string.IsNullOrWhiteSpace(search.User.Email) });
            if (firstForClient) await interests.AddAlertAsync(apartment, search, ct);
        }
        count += users.Count;
        apartment.IsApproved = true; apartment.PublishedAt = DateTime.UtcNow; apartment.LastConfirmedAt = DateTime.UtcNow;
        row.Status = "sent"; row.UpdatedAt = DateTime.UtcNow;
        History(row, "sent", actor, count);
        await db.SaveChangesAsync(ct);
        return count;
    }
}
