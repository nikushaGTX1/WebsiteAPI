namespace Website_API.DTO;

public class HomeMatchProfileRequest
{
    public string PropertyGoal { get; set; } = string.Empty;

    public List<string> Districts { get; set; } = [];
    public bool LocationFlexible { get; set; }

    public string? ProximityTarget { get; set; }
    public string? ProximityAddress { get; set; }
    public double? ProximityLatitude { get; set; }
    public double? ProximityLongitude { get; set; }

    public decimal BudgetMin { get; set; }
    public decimal BudgetMax { get; set; }
    public string Currency { get; set; } = "USD";
    public bool? IncludesUtilities { get; set; }

    public string HouseholdType { get; set; } = string.Empty;
    public int Adults { get; set; } = 1;
    public int Children { get; set; }
    public List<string> ChildrenAgeGroups { get; set; } = [];

    public int? Bedrooms { get; set; }
    public string? AdditionalRoom { get; set; }

    public string? RentalDuration { get; set; }
    public string? MoveInTiming { get; set; }
    public DateTime? MoveInDate { get; set; }
    public string? PurchaseTiming { get; set; }

    public List<string> Transportation { get; set; } = [];
    public int? MetroDistanceMinutes { get; set; }
    public bool ParkingAutomaticallyPrioritized { get; set; }

    public List<string> Lifestyles { get; set; } = [];

    public bool HasPet { get; set; }

    public List<string> MainPreferences { get; set; } = [];
    public List<string> AdditionalRequirements { get; set; } = [];

    public string? AdditionalNotes { get; set; }
}