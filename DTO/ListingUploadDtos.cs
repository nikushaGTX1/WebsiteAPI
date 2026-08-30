using System.ComponentModel.DataAnnotations;

namespace Website_API.DTO;

public class CreateListingUploadDto
{
    [Required]
    [MaxLength(20)]
    public string Platform { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [RegularExpression("^[0-9]+$", ErrorMessage = "PublishedListingId must contain digits only.")]
    public string PublishedListingId { get; set; } = string.Empty;

    [MaxLength(2000)]
    [Url]
    public string? PublishedUrl { get; set; }
}

public class CreateSourceListingUploadDto : CreateListingUploadDto
{
    [Required, MaxLength(20)] public string SourcePlatform { get; set; } = string.Empty;
    [Required, MaxLength(100), RegularExpression("^[0-9]+$")] public string SourceListingId { get; set; } = string.Empty;
    [MaxLength(2000), Url] public string? SourceUrl { get; set; }
}
