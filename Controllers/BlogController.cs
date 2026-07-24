using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.DTO;
using Website_API.Models;

namespace Website_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BlogController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public BlogController(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var posts = await _context.BlogPosts
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(posts);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var post = await _context.BlogPosts.FindAsync(id);

        if (post == null)
            return NotFound();

        return Ok(post);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromForm] CreateBlogPostDto dto)
    {
        var imageUrl = await SaveImage(dto.Image);

        var post = new BlogPost
        {
            Title = dto.Title,
            Summary = dto.Summary,
            Content = dto.Content,
            ImageUrl = imageUrl,
            CreatedAt = DateTime.UtcNow
        };

        _context.BlogPosts.Add(post);
        await _context.SaveChangesAsync();

        return Ok(post);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromForm] UpdateBlogPostDto dto)
    {
        var post = await _context.BlogPosts.FindAsync(id);

        if (post == null)
            return NotFound();

        if (!string.IsNullOrWhiteSpace(dto.Title))
            post.Title = dto.Title;

        if (!string.IsNullOrWhiteSpace(dto.Summary))
            post.Summary = dto.Summary;

        if (!string.IsNullOrWhiteSpace(dto.Content))
            post.Content = dto.Content;

        if (dto.Image != null)
        {
            DeleteOldImage(post.ImageUrl);
            post.ImageUrl = await SaveImage(dto.Image);
        }

        await _context.SaveChangesAsync();

        return Ok(post);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var post = await _context.BlogPosts.FindAsync(id);

        if (post == null)
            return NotFound();

        DeleteOldImage(post.ImageUrl);

        _context.BlogPosts.Remove(post);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Blog post deleted" });
    }

    private async Task<string?> SaveImage(IFormFile? image)
    {
        if (image == null)
            return null;

        var webRootPath = _environment.WebRootPath;

        if (string.IsNullOrWhiteSpace(webRootPath))
            webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        var uploadsFolder = Path.Combine(webRootPath, "uploads", "blogs");

        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await image.CopyToAsync(stream);

        return $"uploads/blogs/{fileName}";
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
