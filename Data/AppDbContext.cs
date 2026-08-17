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
    public DbSet<CrmLead> CrmLeads => Set<CrmLead>();
    public DbSet<CrmActivity> CrmActivities => Set<CrmActivity>();
    public DbSet<CrmTask> CrmTasks => Set<CrmTask>();
    public DbSet<StreetGeometry> StreetGeometries => Set<StreetGeometry>();
    public DbSet<LocationArea> LocationAreas => Set<LocationArea>();
    public DbSet<CanonicalStreet> CanonicalStreets => Set<CanonicalStreet>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Apartment>()
            .HasIndex(apartment => apartment.CreatedAt);

        builder.Entity<Apartment>()
            .HasOne(apartment => apartment.UploadedByUser)
            .WithMany()
            .HasForeignKey(apartment => apartment.UploadedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Apartment>()
            .HasIndex(apartment => apartment.UploadedByUserId);

        builder.Entity<Apartment>()
            .HasOne(apartment => apartment.CanonicalStreet)
            .WithMany()
            .HasForeignKey(apartment => apartment.StreetId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Apartment>()
            .HasIndex(apartment => apartment.StreetId);

        builder.Entity<Apartment>()
            .HasIndex(apartment => new
            {
                apartment.City,
                apartment.Region,
                apartment.District,
                apartment.Street
            });

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

        builder.Entity<StreetGeometry>(street =>
        {
            street.Property(item => item.City)
                .HasMaxLength(100);
            street.Property(item => item.District)
                .HasMaxLength(120);
            street.Property(item => item.Names)
                .HasColumnType("text[]");
            street.Property(item => item.CoordinatesJson)
                .HasColumnType("jsonb");
            street.HasIndex(item => item.City);
            street.HasIndex(item => new { item.City, item.District });
            street.HasIndex(item => new { item.OsmWayId, item.District })
                .IsUnique();
            street.HasIndex(item => item.Names)
                .HasMethod("gin");
        });

        builder.Entity<LocationArea>(area =>
        {
            area.Property(item => item.Type).HasMaxLength(32);
            area.Property(item => item.NameKa).HasMaxLength(180);
            area.Property(item => item.NameEn).HasMaxLength(180);
            area.Property(item => item.Slug).HasMaxLength(200);
            area.Property(item => item.BoundaryGeoJson).HasColumnType("jsonb");
            area.Property(item => item.Source).HasMaxLength(80);
            area.Property(item => item.ExternalSourceId).HasMaxLength(160);
            area.Property(item => item.GeometryStatus).HasMaxLength(32);
            area.HasIndex(item => item.Slug).IsUnique();
            area.HasIndex(item => new { item.Type, item.NameEn });
            area.HasOne(item => item.Parent)
                .WithMany(item => item.Children)
                .HasForeignKey(item => item.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CanonicalStreet>(street =>
        {
            street.Property(item => item.NameKa).HasMaxLength(240);
            street.Property(item => item.NameEn).HasMaxLength(240);
            street.Property(item => item.Aliases).HasColumnType("text[]");
            street.Property(item => item.GeometryGeoJson).HasColumnType("jsonb");
            street.Property(item => item.BoundsGeoJson).HasColumnType("jsonb");
            street.Property(item => item.Source).HasMaxLength(80);
            // A canonical road can consist of hundreds of verified OSM ways.
            // Keep every source ID without truncation.
            street.Property(item => item.ExternalSourceId).HasColumnType("text");
            street.Property(item => item.GeometryStatus).HasMaxLength(32);
            street.Property(item => item.ApprovedByUserId).HasMaxLength(450);
            street.Property(item => item.ReviewNotes).HasMaxLength(2000);
            street.HasOne(item => item.City)
                .WithMany()
                .HasForeignKey(item => item.CityId)
                .OnDelete(DeleteBehavior.Restrict);
            street.HasOne(item => item.District)
                .WithMany()
                .HasForeignKey(item => item.DistrictId)
                .OnDelete(DeleteBehavior.Restrict);
            street.HasIndex(item => new { item.DistrictId, item.NameEn });
            street.HasIndex(item => new { item.DistrictId, item.NameKa });
            street.HasIndex(item => item.GeometryStatus);
            street.HasIndex(item => item.Aliases).HasMethod("gin");
        });

        builder.Entity<CrmLead>(lead =>
        {
            lead.Property(item => item.Name)
                .HasMaxLength(160);
            lead.Property(item => item.Email)
                .HasMaxLength(254);
            lead.Property(item => item.Phone)
                .HasMaxLength(50);
            lead.Property(item => item.Goal)
                .HasMaxLength(80);
            lead.Property(item => item.PreferredContactMethod)
                .HasMaxLength(30);
            lead.Property(item => item.PreferredDistricts)
                .HasColumnType("text[]");
            lead.Property(item => item.PreferredPropertyType)
                .HasMaxLength(80);
            lead.Property(item => item.BudgetMin)
                .HasPrecision(18, 2);
            lead.Property(item => item.BudgetMax)
                .HasPrecision(18, 2);
            lead.Property(item => item.Currency)
                .HasMaxLength(3);
            lead.Property(item => item.Preferences)
                .HasMaxLength(4000);
            lead.Property(item => item.Message)
                .HasMaxLength(4000);
            lead.Property(item => item.Status)
                .HasConversion(
                    value => value.ToApiValue(),
                    value => CrmEnumText.ParseLeadStatus(value))
                .HasMaxLength(16);
            lead.Property(item => item.Source)
                .HasConversion(
                    value => value.ToApiValue(),
                    value => CrmEnumText.ParseLeadSource(value))
                .HasMaxLength(16);

            lead.HasOne(item => item.Apartment)
                .WithMany()
                .HasForeignKey(item => item.ApartmentId)
                .OnDelete(DeleteBehavior.SetNull);
            lead.HasOne(item => item.CustomerUser)
                .WithMany()
                .HasForeignKey(item => item.CustomerUserId)
                .OnDelete(DeleteBehavior.SetNull);
            lead.HasOne(item => item.AssignedAgent)
                .WithMany()
                .HasForeignKey(item => item.AssignedAgentId)
                .OnDelete(DeleteBehavior.SetNull);
            lead.HasOne(item => item.CreatedByUser)
                .WithMany()
                .HasForeignKey(item => item.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            lead.HasIndex(item => new
            {
                item.AssignedAgentId,
                item.Status,
                item.UpdatedAt
            });
            lead.HasIndex(item => new
            {
                item.Status,
                item.CreatedAt
            });
            lead.HasIndex(item => item.ApartmentId);
            lead.HasIndex(item => item.CustomerUserId);
        });

        builder.Entity<CrmActivity>(activity =>
        {
            activity.Property(item => item.Type)
                .HasConversion(
                    value => value.ToApiValue(),
                    value => CrmEnumText.ParseActivityType(value))
                .HasMaxLength(16);
            activity.Property(item => item.Content)
                .HasMaxLength(4000);

            activity.HasOne(item => item.Lead)
                .WithMany(lead => lead.Activities)
                .HasForeignKey(item => item.LeadId)
                .OnDelete(DeleteBehavior.Cascade);
            activity.HasOne(item => item.CreatedByUser)
                .WithMany()
                .HasForeignKey(item => item.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            activity.HasIndex(item => new
            {
                item.LeadId,
                item.CreatedAt
            });
        });

        builder.Entity<CrmTask>(task =>
        {
            task.Property(item => item.Type)
                .HasConversion(
                    value => value.ToApiValue(),
                    value => CrmEnumText.ParseTaskType(value))
                .HasMaxLength(16);
            task.Property(item => item.Title)
                .HasMaxLength(200);
            task.Property(item => item.Details)
                .HasMaxLength(4000);

            task.HasOne(item => item.Lead)
                .WithMany(lead => lead.Tasks)
                .HasForeignKey(item => item.LeadId)
                .OnDelete(DeleteBehavior.Cascade);
            task.HasOne(item => item.AssignedAgent)
                .WithMany()
                .HasForeignKey(item => item.AssignedAgentId)
                .OnDelete(DeleteBehavior.SetNull);
            task.HasOne(item => item.CreatedByUser)
                .WithMany()
                .HasForeignKey(item => item.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            task.HasIndex(item => new
            {
                item.LeadId,
                item.DueAt
            });
            task.HasIndex(item => new
            {
                item.AssignedAgentId,
                item.CompletedAt,
                item.DueAt
            });
        });
    }
}
