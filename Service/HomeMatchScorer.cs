using Website_API.DTO;
using Website_API.Models;

namespace Website_API.Services;

public class HomeMatchScorer
{
    public HomeMatchResultDto Score(
        Apartment apartment,
        HomeMatchProfileRequest profile)
    {
        var result = new HomeMatchResultDto
        {
            Apartment = apartment
        };

        var budgetScore = ScoreBudget(apartment, profile, result);
        var locationScore = ScoreLocation(apartment, profile, result);
        var householdScore = ScoreHousehold(apartment, profile, result);
        var lifestyleScore = ScoreLifestyle(apartment, profile, result);
        var transportScore = ScoreTransportation(apartment, profile, result);
        var preferenceScore = ScorePreferences(apartment, profile, result);
        var additionalScore = ScoreAdditionalRequirements(
            apartment,
            profile,
            result
        );

        var total =
            budgetScore +
            locationScore +
            householdScore +
            lifestyleScore +
            transportScore +
            preferenceScore +
            additionalScore;

        result.MatchScore = Math.Clamp(total, 0, 100);
        result.MatchLabel = GetMatchLabel(result.MatchScore);
        result.RecommendationCategory =
            GetRecommendationCategory(apartment, profile);

        if (profile.HasPet && !apartment.IsPetFriendly)
        {
            result.Warnings.Add(
                "Landlord confirmation may be required for pets."
            );
        }

        return result;
    }

    private static int ScoreBudget(
        Apartment apartment,
        HomeMatchProfileRequest profile,
        HomeMatchResultDto result)
    {
        const int available = 20;
        int earned;

        if (apartment.Price >= profile.BudgetMin &&
            apartment.Price <= profile.BudgetMax)
        {
            earned = available;

            AddReason(
                result,
                "Budget match",
                "The apartment is within your preferred budget.",
                earned,
                available
            );
        }
        else if (apartment.Price < profile.BudgetMin)
        {
            earned = 18;

            AddReason(
                result,
                "Below budget",
                "The apartment costs less than your preferred maximum.",
                earned,
                available
            );
        }
        else
        {
            var difference = apartment.Price - profile.BudgetMax;
            var tolerance = profile.BudgetMax * 0.10m;

            earned = difference <= tolerance ? 10 : 0;

            AddTradeOff(
                result,
                "Above budget",
                $"The apartment is {difference:C0} above your maximum budget.",
                earned == 0 ? "High" : "Medium",
                available - earned
            );
        }

        AddBreakdown(result, "Budget", earned, available);
        return earned;
    }

    private static int ScoreLocation(
        Apartment apartment,
        HomeMatchProfileRequest profile,
        HomeMatchResultDto result)
    {
        const int available = 15;

        if (profile.LocationFlexible)
        {
            AddReason(
                result,
                "Flexible location",
                "You indicated that you are flexible about location.",
                available,
                available
            );

            AddBreakdown(result, "Location", available, available);
            return available;
        }

        var districtMatches = profile.Districts.Any(district =>
            string.Equals(
                district,
                apartment.District,
                StringComparison.OrdinalIgnoreCase
            )
        );

        var earned = districtMatches ? available : 2;

        if (districtMatches)
        {
            AddReason(
                result,
                "Preferred district",
                $"{apartment.District} is one of your selected districts.",
                earned,
                available
            );
        }
        else
        {
            AddTradeOff(
                result,
                "Location",
                $"{apartment.District} is outside your selected districts.",
                "High",
                available - earned
            );
        }

        AddBreakdown(result, "Location", earned, available);
        return earned;
    }

    private static int ScoreHousehold(
        Apartment apartment,
        HomeMatchProfileRequest profile,
        HomeMatchResultDto result)
    {
        const int available = 15;
        var earned = 0;

        if (!profile.Bedrooms.HasValue)
        {
            earned += 8;
        }
        else if (apartment.Bedrooms >= profile.Bedrooms.Value)
        {
            earned += 10;

            AddReason(
                result,
                "Bedroom fit",
                $"The apartment has {apartment.Bedrooms} bedrooms.",
                10,
                10
            );
        }
        else
        {
            AddTradeOff(
                result,
                "Bedroom count",
                $"You requested {profile.Bedrooms.Value} bedrooms, but this apartment has {apartment.Bedrooms}.",
                "High",
                10
            );
        }

        if (profile.Children > 0)
        {
            if (apartment.Bathrooms >= 2)
            {
                earned += 3;

                AddReason(
                    result,
                    "Family-friendly bathrooms",
                    "The apartment has at least two bathrooms.",
                    3,
                    3
                );
            }

            if (apartment.HasElevator || apartment.Floor <= 2)
            {
                earned += 2;

                AddReason(
                    result,
                    "Convenient family access",
                    apartment.HasElevator
                        ? "The building has an elevator."
                        : "The apartment is on a relatively low floor.",
                    2,
                    2
                );
            }
        }
        else
        {
            earned += 5;
        }

        earned = Math.Min(earned, available);

        AddBreakdown(result, "Household and bedroom fit", earned, available);
        return earned;
    }

    private static int ScoreLifestyle(
        Apartment apartment,
        HomeMatchProfileRequest profile,
        HomeMatchResultDto result)
    {
        const int available = 20;
        var earned = 0;

        if (profile.Lifestyles.Contains("Athlete"))
        {
            var gymScore = DistanceScore(
                apartment.GymDistanceMinutes,
                5,
                10,
                20,
                8
            );

            earned += gymScore;

            if (gymScore > 0)
            {
                AddReason(
                    result,
                    "Gym nearby",
                    $"The nearest gym is approximately {apartment.GymDistanceMinutes} minutes away.",
                    gymScore,
                    8
                );
            }
            else
            {
                AddTradeOff(
                    result,
                    "Gym distance",
                    apartment.GymDistanceMinutes.HasValue
                        ? $"The nearest gym is approximately {apartment.GymDistanceMinutes} minutes away."
                        : "Gym-distance information is unavailable.",
                    "Medium",
                    8
                );
            }
        }

        if (profile.Lifestyles.Contains("RemoteWorker"))
        {
            if (apartment.HasHomeOfficeSpace)
            {
                earned += 6;

                AddReason(
                    result,
                    "Workspace",
                    "The apartment has dedicated home-office space.",
                    6,
                    6
                );
            }

            if (IsQuiet(apartment))
                earned += 3;

            if (HasGoodSunlight(apartment))
                earned += 3;
        }

        if (profile.Lifestyles.Contains("FamilyFocused"))
        {
            if (IsWithin(apartment.SchoolDistanceMinutes, 15))
                earned += 4;

            if (IsWithin(apartment.KindergartenDistanceMinutes, 15))
                earned += 4;

            if (IsWithin(apartment.ParkDistanceMinutes, 15))
                earned += 3;
        }

        if (profile.Lifestyles.Contains("SocialLifestyle") ||
            profile.Lifestyles.Contains("HostsGuests"))
        {
            if (apartment.HasLargeKitchen)
                earned += 4;

            if (apartment.SizeSquareMeters >= 90)
                earned += 4;
        }

        earned = Math.Min(earned, available);

        AddBreakdown(result, "Lifestyle", earned, available);
        return earned;
    }

    private static int ScoreTransportation(
        Apartment apartment,
        HomeMatchProfileRequest profile,
        HomeMatchResultDto result)
    {
        const int available = 10;
        var earned = 0;

        if (profile.Transportation.Contains("Car"))
        {
            if (apartment.HasParking)
            {
                earned += 5;

                AddReason(
                    result,
                    "Private parking",
                    "Parking was prioritized because you selected Car.",
                    5,
                    5
                );
            }
            else
            {
                AddTradeOff(
                    result,
                    "Parking",
                    "The apartment does not list private parking.",
                    "High",
                    5
                );
            }
        }

        if (profile.Transportation.Contains("Metro"))
        {
            if (!profile.MetroDistanceMinutes.HasValue)
            {
                earned += 5;
            }
            else if (
                apartment.MetroDistanceMinutes.HasValue &&
                apartment.MetroDistanceMinutes.Value <=
                profile.MetroDistanceMinutes.Value
            )
            {
                earned += 5;

                AddReason(
                    result,
                    "Metro access",
                    $"The metro is approximately {apartment.MetroDistanceMinutes.Value} minutes away.",
                    5,
                    5
                );
            }
            else
            {
                AddTradeOff(
                    result,
                    "Metro distance",
                    apartment.MetroDistanceMinutes.HasValue
                        ? $"The metro is approximately {apartment.MetroDistanceMinutes.Value} minutes away."
                        : "Metro-distance information is unavailable.",
                    "Medium",
                    5
                );
            }
        }

        if (!profile.Transportation.Contains("Car") &&
            !profile.Transportation.Contains("Metro"))
        {
            earned = available;
        }

        earned = Math.Min(earned, available);

        AddBreakdown(result, "Transportation", earned, available);
        return earned;
    }

    private static int ScorePreferences(
        Apartment apartment,
        HomeMatchProfileRequest profile,
        HomeMatchResultDto result)
    {
        const int available = 15;

        if (profile.MainPreferences.Count == 0)
        {
            AddBreakdown(result, "Main preferences", available, available);
            return available;
        }

        var matched = 0;

        foreach (var preference in profile.MainPreferences)
        {
            if (MatchesPreference(apartment, preference))
            {
                matched++;

                AddReason(
                    result,
                    Humanize(preference),
                    GetPreferenceDescription(apartment, preference),
                    0,
                    0
                );
            }
            else
            {
                AddTradeOff(
                    result,
                    Humanize(preference),
                    $"The apartment does not fully match your {Humanize(preference).ToLowerInvariant()} preference.",
                    "Medium",
                    0
                );
            }
        }

        var earned = (int)Math.Round(
            available * (matched / (double)profile.MainPreferences.Count)
        );

        AddBreakdown(result, "Main preferences", earned, available);
        return earned;
    }

    private static int ScoreAdditionalRequirements(
        Apartment apartment,
        HomeMatchProfileRequest profile,
        HomeMatchResultDto result)
    {
        const int available = 5;

        if (profile.AdditionalRequirements.Count == 0)
        {
            AddBreakdown(
                result,
                "Additional requirements",
                available,
                available
            );

            return available;
        }

        var matched = profile.AdditionalRequirements.Count(requirement =>
            MatchesAdditionalRequirement(apartment, requirement)
        );

        var earned = (int)Math.Round(
            available *
            (matched / (double)profile.AdditionalRequirements.Count)
        );

        AddBreakdown(
            result,
            "Additional requirements",
            earned,
            available
        );

        return earned;
    }

    private static bool MatchesPreference(
        Apartment apartment,
        string preference)
    {
        return preference switch
        {
            "SchoolNearby" =>
                IsWithin(apartment.SchoolDistanceMinutes, 15),

            "KindergartenNearby" =>
                IsWithin(apartment.KindergartenDistanceMinutes, 15),

            "ParkNearby" =>
                IsWithin(apartment.ParkDistanceMinutes, 15),

            "GymNearby" =>
                IsWithin(apartment.GymDistanceMinutes, 15),

            "MetroNearby" =>
                IsWithin(apartment.MetroDistanceMinutes, 15),

            "Balcony" => apartment.HasBalcony,

            "NaturalLight" => HasGoodSunlight(apartment),

            "QuietStreet" => IsQuiet(apartment),

            "LargeLivingRoom" =>
                apartment.SizeSquareMeters >= 90,

            "LargeKitchen" => apartment.HasLargeKitchen,

            "SeparateWorkspace" => apartment.HasHomeOfficeSpace,

            "GoodView" => apartment.HasView,

            "MultipleBathrooms" => apartment.Bathrooms >= 2,

            "BedroomAirConditioning" =>
                apartment.HasAirConditioning,

            "Elevator" => apartment.HasElevator,

            "AdditionalStorage" =>
                apartment.SizeSquareMeters >= 100,

            _ => false
        };
    }

    private static bool MatchesAdditionalRequirement(
        Apartment apartment,
        string requirement)
    {
        return requirement switch
        {
            "LowFloor" => apartment.Floor <= 3,
            "HighFloor" => apartment.Floor >= 7,
            "LargeElevator" => apartment.HasElevator,
            "MultipleBathrooms" => apartment.Bathrooms >= 2,
            "Security24Hours" => false,
            "Generator" => false,
            "WaterReservoir" => false,
            _ => false
        };
    }

    private static int DistanceScore(
        int? distance,
        int best,
        int good,
        int acceptable,
        int maximumPoints)
    {
        if (!distance.HasValue)
            return 0;

        if (distance.Value <= best)
            return maximumPoints;

        if (distance.Value <= good)
            return (int)Math.Round(maximumPoints * 0.75);

        if (distance.Value <= acceptable)
            return (int)Math.Round(maximumPoints * 0.4);

        return 0;
    }

    private static bool IsWithin(int? distance, int maximum) =>
        distance.HasValue && distance.Value <= maximum;

    private static bool IsQuiet(Apartment apartment) =>
        apartment.NoiseLevel.Equals(
            "Quiet",
            StringComparison.OrdinalIgnoreCase
        ) ||
        apartment.NoiseLevel.Equals(
            "VeryQuiet",
            StringComparison.OrdinalIgnoreCase
        );

    private static bool HasGoodSunlight(Apartment apartment) =>
        apartment.Sunlight.Equals(
            "Morning",
            StringComparison.OrdinalIgnoreCase
        ) ||
        apartment.Sunlight.Equals(
            "Afternoon",
            StringComparison.OrdinalIgnoreCase
        ) ||
        apartment.Sunlight.Equals(
            "AllDay",
            StringComparison.OrdinalIgnoreCase
        );

    private static string GetMatchLabel(int score) => score switch
    {
        >= 95 => "Perfect Match",
        >= 85 => "Excellent Match",
        >= 70 => "Good Match",
        _ => "Possible Match"
    };

    private static string GetRecommendationCategory(
        Apartment apartment,
        HomeMatchProfileRequest profile)
    {
        if (profile.Children > 0 &&
            apartment.Bedrooms >= 2 &&
            apartment.Bathrooms >= 2)
        {
            return "Best for Your Family";
        }

        if (profile.Lifestyles.Contains("Athlete") &&
            IsWithin(apartment.GymDistanceMinutes, 10))
        {
            return "Best for Your Lifestyle";
        }

        if (!profile.LocationFlexible &&
            profile.Districts.Contains(
                apartment.District,
                StringComparer.OrdinalIgnoreCase
            ))
        {
            return "Best Location Match";
        }

        if (apartment.Price <= profile.BudgetMax)
            return "Best Value";

        return "Best Balanced Match";
    }

    private static string GetPreferenceDescription(
        Apartment apartment,
        string preference)
    {
        return preference switch
        {
            "SchoolNearby" =>
                $"School is approximately {apartment.SchoolDistanceMinutes} minutes away.",

            "KindergartenNearby" =>
                $"Kindergarten is approximately {apartment.KindergartenDistanceMinutes} minutes away.",

            "ParkNearby" =>
                $"Park is approximately {apartment.ParkDistanceMinutes} minutes away.",

            "GymNearby" =>
                $"Gym is approximately {apartment.GymDistanceMinutes} minutes away.",

            "MetroNearby" =>
                $"Metro is approximately {apartment.MetroDistanceMinutes} minutes away.",

            _ => $"The apartment includes {Humanize(preference).ToLowerInvariant()}."
        };
    }

    private static string Humanize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return System.Text.RegularExpressions.Regex.Replace(
            value,
            "([a-z])([A-Z])",
            "$1 $2"
        );
    }

    private static void AddReason(
        HomeMatchResultDto result,
        string title,
        string description,
        int pointsEarned,
        int pointsAvailable)
    {
        result.Reasons.Add(new HomeMatchReasonDto
        {
            Title = title,
            Description = description,
            PointsEarned = pointsEarned,
            PointsAvailable = pointsAvailable
        });
    }

    private static void AddTradeOff(
        HomeMatchResultDto result,
        string title,
        string description,
        string severity,
        int pointsLost)
    {
        result.TradeOffs.Add(new HomeMatchTradeOffDto
        {
            Title = title,
            Description = description,
            Severity = severity,
            PointsLost = pointsLost
        });
    }

    private static void AddBreakdown(
        HomeMatchResultDto result,
        string label,
        int earned,
        int available)
    {
        result.ScoreBreakdown.Add(new HomeMatchScoreBreakdownDto
        {
            Label = label,
            PointsEarned = earned,
            PointsAvailable = available
        });
    }
}