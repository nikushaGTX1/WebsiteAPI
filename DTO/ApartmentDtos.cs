using Microsoft.AspNetCore.Http;

namespace Website_API.DTO;

public class CreateApartmentDto
{
    public string Title { get; set; } = "";

    public string Description { get; set; } = "";

    public decimal Price { get; set; }

    public string? Address { get; set; }

    public IFormFile? Image { get; set; }
}

public class UpdateApartmentDto
{
    public string? Title { get; set; }

    public string? Description { get; set; }

    public decimal? Price { get; set; }

    public string? Address { get; set; }

    public IFormFile? Image { get; set; }
}