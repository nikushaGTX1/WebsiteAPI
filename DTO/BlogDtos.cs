using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Website_API.DTO;

public class CreateBlogPostDto
{
    [Required, StringLength(200)]
    public string Title { get; set; } = "";

    [Required, StringLength(500)]
    public string Summary { get; set; } = "";

    [Required]
    public string Content { get; set; } = "";

    public IFormFile? Image { get; set; }
}

public class UpdateBlogPostDto
{
    [StringLength(200)]
    public string? Title { get; set; }

    [StringLength(500)]
    public string? Summary { get; set; }

    public string? Content { get; set; }

    public IFormFile? Image { get; set; }
}

public class BlogPostResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Content { get; set; } = "";
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}
