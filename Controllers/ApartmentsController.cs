using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.DTO;
using Website_API.Models;

namespace Website_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApartmentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public ApartmentsController(
        AppDbContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> GetApartments()
    {
        var apartments = await _context.Apartments
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return Ok(apartments);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetApartment(int id)
    {
        var apartment = await _context.Apartments.FindAsync(id);

        if (apartment == null)
            return NotFound(new { message = "Apartment not found" });

        return Ok(apartment);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> CreateApartment([FromForm] CreateApartmentDto dto)
    {
        string? imageUrl = await SaveImage(dto.Image);

        var apartment = new Apartment
        {
            Title = dto.Title,
            Description = dto.Description,
            Price = dto.Price,
            Address = dto.Address,
            ImageUrl = imageUrl
        };

        _context.Apartments.Add(apartment);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Apartment created successfully",
            apartment
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateApartment(int id, [FromForm] UpdateApartmentDto dto)
    {
        var apartment = await _context.Apartments.FindAsync(id);

        if (apartment == null)
            return NotFound(new { message = "Apartment not found" });

        apartment.Title = dto.Title ?? apartment.Title;
        apartment.Description = dto.Description ?? apartment.Description;
        apartment.Price = dto.Price ?? apartment.Price;
        apartment.Address = dto.Address ?? apartment.Address;

        if (dto.Image != null)
        {
            DeleteOldImage(apartment.ImageUrl);
            apartment.ImageUrl = await SaveImage(dto.Image);
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Apartment updated successfully",
            apartment
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteApartment(int id)
    {
        var apartment = await _context.Apartments.FindAsync(id);

        if (apartment == null)
            return NotFound(new { message = "Apartment not found" });

        DeleteOldImage(apartment.ImageUrl);

        _context.Apartments.Remove(apartment);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Apartment deleted successfully"
        });
    }

    private async Task<string?> SaveImage(IFormFile? image)
    {
        if (image == null)
            return null;

        var webRootPath = _environment.WebRootPath;

        if (string.IsNullOrWhiteSpace(webRootPath))
            webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        var uploadsFolder = Path.Combine(webRootPath, "uploads", "apartments");

        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await image.CopyToAsync(stream);

        return $"uploads/apartments/{fileName}";
    }

    private void DeleteOldImage(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return;

        var webRootPath = _environment.WebRootPath;

        if (string.IsNullOrWhiteSpace(webRootPath))
            webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        var cleanPath = imageUrl.Replace("/", Path.DirectorySeparatorChar.ToString());
        var filePath = Path.Combine(webRootPath, cleanPath);

        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);
    }
}