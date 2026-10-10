using System.Text.RegularExpressions;
using Website_API.Models;

namespace Website_API.Services;

/// <summary>
/// Rental budget tolerance shared by filtering and scoring. The website applies the same rule
/// (mandatory-requirements.ts, maxRecommendedPriceUsd) for its badges.
/// Up to $1,000/month nothing above the maximum is recommended; above $1,000, homes up to 25%
/// over the maximum may be shown as over-budget options. Purchases never get a tolerance.
/// </summary>
public static class RentalBudgetPolicy
{
    public const decimal ToleranceThreshold = 1000m;
    public const decimal ToleranceRate = 0.25m;

    public static decimal MaxRecommendedPrice(decimal budgetMax, string? propertyGoal)
    {
        var max = Math.Round(budgetMax, 2);
        var rent = string.Equals(propertyGoal, "Rent", StringComparison.OrdinalIgnoreCase);
        return !rent || max <= ToleranceThreshold ? max : max * (1 + ToleranceRate);
    }

    /// <summary>A zero maximum means "no limit".</summary>
    public static bool IsWithinCeiling(decimal price, decimal budgetMax, string? propertyGoal) =>
        budgetMax <= 0 || price <= 0 || price <= MaxRecommendedPrice(budgetMax, propertyGoal);
}

/// <summary>
/// Whether a listing explicitly refuses the user's pets. An unticked "Pet friendly" box is not a
/// refusal; only the listing's own text ("No pets", "No dogs", "small dogs only"...) counts.
/// Mirrors petPermission() on the website.
/// </summary>
public static class PetPolicy
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static readonly Regex NoPets = new(
        @"pet[\s-]*friendly:\s*no\b|pets?(?:\s+allowed)?:\s*(?:no|not allowed)\b|\bno pets\b|pets? (?:are )?not (?:allowed|permitted)|without pets|შინაური ცხოველ\S*\s+(?:არ|აკრძალ)|ცხოველები\s+(?:არ|აკრძალ)|ცხოველების გარეშე|без (?:домашних )?животных|животны\S* (?:не допуска|запрещ)",
        Options);
    private static readonly Regex NoDogs = new(
        @"\bno dogs\b|dogs? (?:are )?not (?:allowed|permitted)|ძაღლ\S*\s+(?:არ|აკრძალ)|без собак|собаки? запрещ",
        Options);
    private static readonly Regex NoCats = new(
        @"\bno cats\b|cats? (?:are )?not (?:allowed|permitted)|კატ\S*\s+(?:არ|აკრძალ)|без кош",
        Options);
    private static readonly Regex SmallDogsOnly = new(
        @"small (?:dogs?|pets?) only|only small (?:dogs?|pets?)|მხოლოდ პატარა|только (?:маленьк|мелк)",
        Options);
    private static readonly Regex NoLargeDogs = new(
        @"no (?:large|big) dogs|large dogs? (?:are )?not (?:allowed|permitted)|без крупных собак",
        Options);

    public static bool IsProhibited(Apartment apartment, IReadOnlyCollection<string> pets, string? dogSize)
    {
        if (pets.Count == 0) return false;
        var text = $"{apartment.Title} {apartment.Description}";
        if (NoPets.IsMatch(text)) return true;
        var dog = pets.Contains("Dog", StringComparer.OrdinalIgnoreCase);
        if (dog && NoDogs.IsMatch(text)) return true;
        if (dog && !string.IsNullOrEmpty(dogSize) &&
            !dogSize.Equals("Small", StringComparison.OrdinalIgnoreCase) && SmallDogsOnly.IsMatch(text))
            return true;
        if (dog && string.Equals(dogSize, "Large", StringComparison.OrdinalIgnoreCase) && NoLargeDogs.IsMatch(text))
            return true;
        return pets.Contains("Cat", StringComparer.OrdinalIgnoreCase) && NoCats.IsMatch(text);
    }
}
