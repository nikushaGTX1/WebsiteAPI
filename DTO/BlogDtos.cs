using Microsoft.AspNetCore.Http;

namespace Website_API.DTO;

public class CreateBlogPostDto
{
    public string Title { get; set; } = "";

    public string Summary { get; set; } = "";

    public string Content { get; set; } = "";

    public IFormFile? Image { get; set; }
}

public class UpdateBlogPostDto
{
    public string? Title { get; set; }

    public string? Summary { get; set; }

    public string? Content { get; set; }

    public IFormFile? Image { get; set; }
}