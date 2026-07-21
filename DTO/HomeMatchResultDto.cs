using Website_API.Models;

namespace Website_API.DTO;

public class HomeMatchResponseDto
{
    public int TotalMatches { get; set; }
    public List<HomeMatchResultDto> Matches { get; set; } = [];
}

public class HomeMatchResultDto
{
    public Apartment Apartment { get; set; } = null!;

    public int MatchScore { get; set; }
    public string MatchLabel { get; set; } = string.Empty;
    public string RecommendationCategory { get; set; } = string.Empty;

    public List<HomeMatchReasonDto> Reasons { get; set; } = [];
    public List<HomeMatchTradeOffDto> TradeOffs { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<HomeMatchScoreBreakdownDto> ScoreBreakdown { get; set; } = [];
}

public class HomeMatchReasonDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public int PointsEarned { get; set; }
    public int PointsAvailable { get; set; }
}

public class HomeMatchTradeOffDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public string Severity { get; set; } = "Low";
    public int PointsLost { get; set; }
}

public class HomeMatchScoreBreakdownDto
{
    public string Label { get; set; } = string.Empty;

    public int PointsEarned { get; set; }
    public int PointsAvailable { get; set; }
}