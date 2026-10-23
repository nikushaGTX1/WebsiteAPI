using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Website_API.Services;

namespace Website_API.DTO;

public class ClientIntakeRequest
{
    public string FirstName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Nationality { get; set; }
    public bool Consent { get; set; }
    public bool PublishConsent { get; set; }
    public JsonElement Profile { get; set; }
}

// An explicit allowlist: arbitrary profile keys, contact details, addresses, map coordinates
// and free-form answers can never cross the public board's privacy boundary.
public class ClientRequirements : SavedSearchProfile
{
    public string Gender { get; set; } = "";
    public string HouseholdType { get; set; } = "";
    public int Adults { get; set; } = 1;
    public int Children { get; set; }
    public List<string> ChildrenAgeGroups { get; set; } = [];
    public List<string> Lifestyles { get; set; } = [];
    public List<string> TopPriorities { get; set; } = [];
    public decimal? MinSize { get; set; }
    public int? PetCount { get; set; }
    public List<string> Transportation { get; set; } = [];
    public int? MetroDistanceMinutes { get; set; }
    public bool ParkingAutomaticallyPrioritized { get; set; }
    public string? CarFuelType { get; set; }
    public string? PurchaseTiming { get; set; }
    public string? ApartmentStyle { get; set; }
    public string? PetOtherType { get; set; }
    public string? ProximityTarget { get; set; }
    public string? ProximityAddress { get; set; }
    public double? ProximityLatitude { get; set; }
    public double? ProximityLongitude { get; set; }

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static ClientRequirements Read(string json) => JsonSerializer.Deserialize<ClientRequirements>(json, Json)!;

    public static string? Validate(ClientIntakeRequest request)
    {
        if (!request.Consent) return "consent_required";
        if (string.IsNullOrWhiteSpace(request.FirstName) || request.FirstName.Length > 80 ||
            string.IsNullOrWhiteSpace(request.Phone) || request.Phone.Length > 40 || !ValidPhone(request.Phone) ||
            (request.Nationality?.Length ?? 0) > 80) return "invalid_contact";
        if (request.Profile.ValueKind != JsonValueKind.Object) return "invalid_profile";
        var raw = request.Profile.GetRawText();
        if (Encoding.UTF8.GetByteCount(raw) > 100 * 1024) return "profile_too_large";
        if (!IsValid(raw)) return "invalid_profile";
        try
        {
            var p = Read(raw);
            // Match-completed badges require a completed questionnaire, not merely
            // well-formed partial JSON. The public projection still shows only three priorities.
            if (p.Gender is not ("Male" or "Female") || p.ApartmentStyle is not ("Modern" or "Vintage" or "Other") ||
                !request.Profile.TryGetProperty("bedrooms", out _) || p.Transportation is not { Count: > 0 } ||
                p.Lifestyles is not { Count: > 0 } || p.TopPriorities is not { Count: 5 } ||
                (p.PropertyGoal == "Buy" && string.IsNullOrEmpty(p.PurchaseTiming)) ||
                (p.PropertyGoal == "Rent" && (string.IsNullOrEmpty(p.HouseholdType) || string.IsNullOrEmpty(p.RentalDuration) ||
                    string.IsNullOrEmpty(p.MoveInTiming) || p.HasPet is null)) ||
                (!p.LocationFlexible && p.Districts.Count == 0 && p.SelectedMapArea is null)) return "invalid_profile";
            if (p.Adults is < 1 or > 7 || p.Children is < 0 or > 6 || p.Adults + p.Children > 7 || p.BudgetMax > 1_000_000_000 ||
                (p.BudgetConversion is { } conversion && (conversion.UsdGel is < 0.01m or > 1000 || conversion.UsdEur is < 0.01m or > 1000)) ||
                p.Bedrooms is < 0 or > 4 || p.MinSize is < 0 or > 10000 || p.PetCount is < 1 or > 20 ||
                !Codes(p.Districts, "Vake Saburtalo Vera Mtatsminda Dighomi DidiDighomi Didube Avlabari Isani Gldani Chugureti Ortachala Nadzaladevi Krtsanisi Vashlijvari Sololaki Samgori SelectOnMap") ||
                !Codes(p.Lifestyles, "Athlete RemoteWorker BusinessProfessional Student QuietLifestyle SocialLifestyle HostsGuests FrequentTraveler") ||
                !Codes(p.Transportation, "Car Metro Walking MultipleMethods") ||
                !Codes(p.ChildrenAgeGroups, "Age0To3 Age4To6 Age7To12 Age13To17") ||
                !Codes(p.TopPriorities, "SelectedLocationNearby OfficeNearby MetroNearby SchoolNearby KindergartenNearby GymNearby ParkNearby UniversityNearby SupermarketNearby PharmacyNearby PlaygroundNearby PlaygroundOrSportsFieldNearby ClinicNearby PublicTransportNearby EverydayServicesNearby CafesNearby CafesOrCoworkingNearby CafesAndRestaurantsNearby MeetingPlacesNearby StudySpacesNearby CityCenterNearby EntertainmentNearby Workspace Parking QuietStreet AwayFromNightlife BalconyOrTerrace MultipleBathrooms SecurityOrConcierge ModernMaintainedBuilding IsolatedBedrooms CompanyLeaseAvailable") ||
                !Code(p.Gender, "Male Female") || !Code(p.HouseholdType, "JustMe Couple FamilyWithChildren Relatives Roommates CorporateHousing") ||
                !Code(p.RentalDuration, "ThreeToFiveMonths SixMonths TwelveMonths MoreThanTwelveMonths Unknown") ||
                !Code(p.MoveInTiming, "Immediately WithinOneWeek WithinOneMonth SpecificDate Flexible") ||
                !Code(p.PetType, "None Dog Cat Other") || !Code(p.DogSize, "Small Medium Large") ||
                !Code(p.CarFuelType, "Electric Fuel") || !Code(p.ApartmentStyle, "Modern Vintage Other") ||
                !Code(p.PurchaseTiming, "Immediately WithinOneMonth WithinOneToThreeMonths WithinThreeToSixMonths MoreThanSixMonths Exploring Flexible") ||
                (p.PetOtherType?.Length ?? 0) > 120 || (p.ProximityAddress?.Length ?? 0) > 500 ||
                (p.ProximityTarget?.Length ?? 0) > 80 || p.MetroDistanceMinutes is < 0 or > 120 ||
                p.ProximityLatitude is < -90 or > 90 || p.ProximityLongitude is < -180 or > 180 ||
                (p.MoveInTiming == "SpecificDate" && string.IsNullOrEmpty(p.MoveInDate)) ||
                p.TopPriorities.Count > 5 || p.Lifestyles.Count > 3 ||
                (!string.IsNullOrEmpty(p.MoveInDate) && !DateOnly.TryParseExact(p.MoveInDate, "yyyy-MM-dd", out _))) return "invalid_profile";
            if (p.Children > 0 && p.ChildrenAgeGroups.Count == 0 ||
                p.HasPet == true && p.PetTypes.Count == 0 && p.PetType is not ("Dog" or "Cat" or "Other") ||
                (p.PetTypes.Contains("Dog") || p.PetType == "Dog") && string.IsNullOrEmpty(p.DogSize) ||
                (p.PetTypes.Contains("Other") || p.PetType == "Other") && string.IsNullOrWhiteSpace(p.PetOtherType)) return "invalid_profile";
            if (Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(p, Json)) > 100 * 1024) return "profile_too_large";
        }
        catch (JsonException) { return "invalid_profile"; }
        return null;
    }

    private static bool Code(string? value, string allowed) => string.IsNullOrEmpty(value) || allowed.Split(' ').Contains(value);
    private static bool Codes(List<string>? values, string allowed) => values is not null && values.Count <= 25 && values.Distinct().Count() == values.Count && values.All(x => !string.IsNullOrEmpty(x) && Code(x, allowed));
    public static bool ValidPhone(string value)
    {
        if (!Regex.IsMatch(value, ContactPhone.Pattern)) return false;
        var digits = Regex.Replace(value, @"\D", "");
        if (!value.StartsWith('+')) return Regex.IsMatch(digits, @"^[235]\d{8}$");
        return digits.StartsWith("995") ? Regex.IsMatch(digits, @"^995[235]\d{8}$") : digits.Length is >= 8 and <= 15;
    }
}

public record PublicClientRequirements(
    decimal BudgetMin, decimal BudgetMax, string Currency, string[] Districts, bool LocationFlexible,
    string Gender, string HouseholdType, int Adults, int Children, string[] ChildrenAgeGroups,
    int? Bedrooms, int[] BedroomChoices, decimal? MinSize, string? RentalDuration,
    string? MoveInTiming, string? MoveInDate, bool HasPet, string? PetType, string[] PetTypes,
    string? DogSize, int? PetCount, string[] TopPriorities);

public record PublicClientAgent(string Name, string? Phone, string? Photo);
public record PublicClientCard(long Id, bool IdentityVerified, string? Nationality,
    PublicClientRequirements Profile, PublicClientAgent Agent);
public record ClientBoardPage(IReadOnlyList<PublicClientCard> Items, int Total, int Page, int PageSize);
