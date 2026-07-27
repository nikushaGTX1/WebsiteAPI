using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Website_API.Data;
using Website_API.Models;
using Website_API.Services;

namespace Website_API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class FavoritesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly SupabaseStorageService _storageService;

    public FavoritesController(
        AppDbContext context,
        SupabaseStorageService storageService)
    {
        _context = context;
        _storageService = storageService;
    }

    [HttpGet]
    public async Task<IActionResult> GetFavorites(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var favorites = await _context.FavoriteApartments
            .AsNoTracking()
            .Where(favorite => favorite.UserId == userId)
            .OrderByDescending(favorite => favorite.CreatedAt)
            .Select(favorite => new
            {
                favorite.CreatedAt,
                Apartment = favorite.Apartment
            })
            .ToListAsync(cancellationToken);

        var response = new List<object>(favorites.Count);
        foreach (var favorite in favorites)
        {
            favorite.Apartment.ImageUrl =
                await _storageService.CreateSignedUrlAsync(
                    favorite.Apartment.ImageUrl,
                    3600,
                    cancellationToken);

            response.Add(new
            {
                favorite.CreatedAt,
                favorite.Apartment
            });
        }

        return Ok(response);
    }

    [HttpGet("{apartmentId:int}")]
    public async Task<IActionResult> IsFavorite(
        int apartmentId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var isFavorite = await _context.FavoriteApartments
            .AsNoTracking()
            .AnyAsync(
                favorite =>
                    favorite.UserId == userId &&
                    favorite.ApartmentId == apartmentId,
                cancellationToken);

        return Ok(new
        {
            apartmentId,
            isFavorite
        });
    }

    [HttpPost("{apartmentId:int}")]
    public async Task<IActionResult> AddFavorite(
        int apartmentId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var apartmentExists = await _context.Apartments
            .AsNoTracking()
            .AnyAsync(
                apartment => apartment.Id == apartmentId,
                cancellationToken);

        if (!apartmentExists)
        {
            return NotFound(new { message = "Apartment not found" });
        }

        var alreadySaved = await _context.FavoriteApartments
            .AnyAsync(
                favorite =>
                    favorite.UserId == userId &&
                    favorite.ApartmentId == apartmentId,
                cancellationToken);

        if (!alreadySaved)
        {
            _context.FavoriteApartments.Add(new FavoriteApartment
            {
                UserId = userId,
                ApartmentId = apartmentId
            });

            await _context.SaveChangesAsync(cancellationToken);
        }

        return Ok(new
        {
            message = "Apartment saved",
            apartmentId,
            isFavorite = true
        });
    }

    [HttpDelete("{apartmentId:int}")]
    public async Task<IActionResult> RemoveFavorite(
        int apartmentId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var favorite = await _context.FavoriteApartments
            .FirstOrDefaultAsync(
                item =>
                    item.UserId == userId &&
                    item.ApartmentId == apartmentId,
                cancellationToken);

        if (favorite is not null)
        {
            _context.FavoriteApartments.Remove(favorite);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Ok(new
        {
            message = "Apartment removed from saved listings",
            apartmentId,
            isFavorite = false
        });
    }

    private string? GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);
}
