using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.DTO;
using Website_API.Models;
using Website_API.Services;

namespace Website_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BlogController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly SupabaseStorageService _storageService;

    public BlogController(
        AppDbContext context,
        SupabaseStorageService storageService)
    {
        _context = context;
        _storageService = storageService;
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

        return Ok(await Task.WhenAll(
            posts.Select(post => ToResponseAsync(post))));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var post = await _context.BlogPosts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (post == null)
            return NotFound();

        return Ok(await ToResponseAsync(post));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromForm] CreateBlogPostDto dto,
        CancellationToken cancellationToken)
    {
        var imageUrl = await _storageService.UploadImageAsync(
            dto.Image,
            "blogs",
            cancellationToken);

        var post = new BlogPost
        {
            Title = dto.Title,
            Summary = dto.Summary,
            Content = dto.Content,
            ImageUrl = imageUrl,
            CreatedAt = DateTime.UtcNow
        };

        _context.BlogPosts.Add(post);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = post.Id },
            await ToResponseAsync(post, cancellationToken));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromForm] UpdateBlogPostDto dto,
        CancellationToken cancellationToken)
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
            var oldImageUrl = post.ImageUrl;
            post.ImageUrl = await _storageService.UploadImageAsync(
                dto.Image,
                "blogs",
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            await _storageService.DeleteImageAsync(
                oldImageUrl,
                cancellationToken);

            return Ok(await ToResponseAsync(post, cancellationToken));
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(await ToResponseAsync(post, cancellationToken));
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        var post = await _context.BlogPosts.FindAsync(id);

        if (post == null)
            return NotFound();

        _context.BlogPosts.Remove(post);
        await _context.SaveChangesAsync(cancellationToken);

        await _storageService.DeleteImageAsync(
            post.ImageUrl,
            cancellationToken);

        return Ok(new { message = "Blog post deleted" });
    }

    private async Task<BlogPostResponseDto> ToResponseAsync(
        BlogPost post,
        CancellationToken cancellationToken = default)
    {
        var imageUrl = await _storageService.CreateSignedUrlAsync(
            post.ImageUrl,
            cancellationToken: cancellationToken);

        return new BlogPostResponseDto
        {
            Id = post.Id,
            Title = post.Title,
            Summary = post.Summary,
            Content = post.Content,
            ImageUrl = imageUrl,
            CreatedAt = post.CreatedAt
        };
    }
}
