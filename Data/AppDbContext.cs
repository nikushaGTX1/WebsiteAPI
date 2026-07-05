using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Website_API.Models;

namespace Website_API.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<AgentRating> AgentRatings => Set<AgentRating>();
    public DbSet<Apartment> Apartments => Set<Apartment>();
    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
}