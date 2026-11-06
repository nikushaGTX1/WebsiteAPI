using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Xunit;

namespace Website_API.Tests;

/// <summary>
/// Production runs Database.MigrateAsync on start, and EF refuses to start when the model has
/// changes no migration describes (PendingModelChangesWarning). This catches that before deploy.
/// </summary>
public class MigrationModelTests
{
    [Fact]
    public void ModelHasNoChangesMissingFromMigrations()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model-check;Username=x;Password=x")
            .Options;
        using var context = new AppDbContext(options);

        Assert.False(context.Database.HasPendingModelChanges(),
            "The model changed without a migration: run `dotnet ef migrations add <Name>`.");
    }
}
