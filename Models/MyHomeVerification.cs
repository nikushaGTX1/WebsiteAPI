namespace Website_API.Models;
public class MyHomeJob {
 public Guid Id { get; set; } = Guid.NewGuid();
 public string State { get; set; } = "RUNNING";
 public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
 public DateTime? CompletedAt { get; set; }
 public string? RequestedBy { get; set; }
 public int TotalApartments { get; set; }
 public int MissingIds { get; set; }
 public string? Error { get; set; }
}
public class MyHomeCheck {
 public long Id { get; set; }
 public Guid JobId { get; set; }
 public int ApartmentId { get; set; }
 public string MyHomeId { get; set; } = "";
 public string? Address { get; set; }
 public string? ImageUrl { get; set; }
 public string Status { get; set; } = "PENDING";
 public DateTime? CheckedAt { get; set; }
 public string? Detail { get; set; }
 public int? HttpStatus { get; set; }
}