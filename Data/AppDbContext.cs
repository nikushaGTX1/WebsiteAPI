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
    public DbSet<ApartmentImage> ApartmentImages => Set<ApartmentImage>();
    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
    public DbSet<FavoriteApartment> FavoriteApartments =>
        Set<FavoriteApartment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Apartment>()
            .HasIndex(apartment => apartment.CreatedAt);

        builder.Entity<ApartmentImage>()
            .ToTable("ApartmentImage");

        builder.Entity<BlogPost>()
            .HasIndex(post => post.CreatedAt);

        builder.Entity<AgentRating>()
            .HasIndex(rating => new { rating.AgentId, rating.CreatedAt });

        builder.Entity<FavoriteApartment>(favorite =>
        {
            favorite.HasKey(item => new
            {
                item.UserId,
                item.ApartmentId
            });

            favorite.HasOne(item => item.User)
                .WithMany(user => user.FavoriteApartments)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            favorite.HasOne(item => item.Apartment)
                .WithMany(apartment => apartment.FavoritedBy)
                .HasForeignKey(item => item.ApartmentId)
                .OnDelete(DeleteBehavior.Cascade);

            favorite.HasIndex(item => new
            {
                item.UserId,
                item.CreatedAt
            });
        });
    }
}
