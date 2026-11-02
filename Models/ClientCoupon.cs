namespace Website_API.Models;

/// <summary>
/// A reward kept in a client's account (their "inventory"), e.g. the $50 cashback won in the
/// Velven game. An agent marks it redeemed when it is applied to a deal.
/// </summary>
public class ClientCoupon
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public AppUser User { get; set; } = null!;

    /// <summary>What the coupon is for; one coupon of each kind per account.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Code the client shows the agent, e.g. VLV-7K2M-9QXD.</summary>
    public string Code { get; set; } = string.Empty;

    public decimal AmountUsd { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RedeemedAt { get; set; }
    public string? RedeemedById { get; set; }
}
