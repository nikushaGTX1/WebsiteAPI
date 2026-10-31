using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IO.Compression;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Website_API.Data;
using Website_API.Models;
using Website_API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient<MyHomeVerifier>(client => { client.Timeout = TimeSpan.FromSeconds(20); client.DefaultRequestHeaders.UserAgent.ParseAdd("VelvenListingVerification/1.0"); }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHostedService<MyHomeWorker>();
builder.Services.AddMemoryCache();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("CrmInquiries", context =>
    {
        // The public website proxies API calls; see ClientIpResolver.
        var partitionKey = ClientIpResolver.Resolve(context);

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: partitionKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(10)
            });
    });
    // Public review links (no sign-in): a few submissions per client IP per 10 minutes.
    options.AddPolicy("PublicReviews", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: Website_API.Controllers.ReviewsController.ClientIp(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(10)
            }));
    // Upload-form photo checks run on the paid GPU: cap each signed-in user.
    options.AddPolicy("ImageChecks", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? ClientIpResolver.Resolve(context),
        factory: _ => new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = 120,
            QueueLimit = 0,
            Window = TimeSpan.FromHours(1)
        }));
});
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy(
        "StaticLocations",
        policy => policy
            .Expire(TimeSpan.FromHours(24))
            .SetVaryByHeader("Origin")
            .SetVaryByQuery("*"));
    options.AddPolicy(
        "Apartments",
        policy => policy
            .Expire(TimeSpan.FromMinutes(1))
            .SetVaryByHeader("Origin")
            .SetVaryByQuery("*")
            .Tag("apartments"));
});
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes =
        ResponseCompressionDefaults.MimeTypes.Concat(
            ["application/json"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

builder.Services.AddHttpClient<GoogleNearbyPlacesService>();
// Fills pet shop / vet walking times once for listings uploaded before they were measured.
builder.Services.AddHostedService<PetServicesBackfillService>();
builder.Services.AddHttpClient<SupabaseStorageService>();
builder.Services.AddHttpClient<ResendEmailService>();
builder.Services.AddHttpClient("OpenStreetMap", client =>
{
    client.Timeout = TimeSpan.FromMinutes(4);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "VelvenRealEstate/1.0 (street geometry import)");
});
// Canonical street imports are admin-triggered review jobs. Never start an
// external map-data import from application startup or a public user request.
builder.Services.AddScoped<CanonicalStreetImportService>();
builder.Services.AddScoped<HomeMatchScorer>();
// Velven Discover: published listings are checked against saved Velven Match searches in the background.
builder.Services.AddSingleton<ApartmentMatchQueue>();
builder.Services.AddScoped<SavedSearchMatcher>();
builder.Services.AddScoped<ListingInterestService>();
builder.Services.AddHostedService<SavedSearchMatchWorker>();

// AI image processing (Modal GPU). Off unless ImageAi__Enabled=true.
builder.Services.Configure<ImageAiOptions>(
    builder.Configuration.GetSection("ImageAi"));
builder.Services.AddHttpClient<ModalImageAiClient>();
builder.Services.AddScoped<ImageProcessingQueue>();
builder.Services.AddHostedService<ImageProcessingWorker>();

// =========================
// CORS
// =========================

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularPolicy", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:4200",
                "https://website-lff1.onrender.com",
                "https://website-production-ab09.up.railway.app",
                "https://velven.ge",
                "https://www.velven.ge",
                "http://192.168.56.1:3000",
                "http://192.168.56.1",
                "http://localhost:3000"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// =========================
// POSTGRESQL DATABASE
// =========================

var connectionString = DatabaseConnection.Resolve(builder.Configuration);

builder.Services.AddDbContextPool<AppDbContext>(
    options => options.UseNpgsql(connectionString),
    poolSize: 128);

// =========================
// IDENTITY
// =========================

builder.Services
    .AddIdentity<AppUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// =========================
// JWT AUTHENTICATION
// =========================

var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "JWT configuration value 'Jwt:Key' is missing.");
}

if (string.IsNullOrWhiteSpace(jwtIssuer))
{
    throw new InvalidOperationException(
        "JWT configuration value 'Jwt:Issuer' is missing.");
}

if (string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException(
        "JWT configuration value 'Jwt:Audience' is missing.");
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,

                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey)
                ),

                ClockSkew = TimeSpan.Zero
            };

        // A signed JWT can otherwise remain usable until it expires even after
        // its owner has deleted their account. Verify that the user still
        // exists so account deletion revokes API access immediately.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userId = context.Principal?
                    .FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrWhiteSpace(userId))
                {
                    context.Fail("The access token has no user identifier.");
                    return;
                }

                var dbContext = context.HttpContext.RequestServices
                    .GetRequiredService<AppDbContext>();
                var userExists = await dbContext.Users
                    .AsNoTracking()
                    .AnyAsync(
                        user => user.Id == userId,
                        context.HttpContext.RequestAborted);

                if (!userExists)
                {
                    context.Fail("The user account no longer exists.");
                }
            }
        };
    });

builder.Services.AddAuthorization();

// =========================
// SWAGGER
// =========================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Website API",
        Version = "v1"
    });

    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT token."
        });

    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
});

var app = builder.Build();

// =========================
// SWAGGER
// =========================

app.UseSwagger();
app.UseSwaggerUI();

// =========================
// DATABASE MIGRATION + SEED
// =========================

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    try
    {
        var context =
            services.GetRequiredService<AppDbContext>();

        await context.Database.MigrateAsync();

        var importedOfficialStreets =
            await OfficialStreetCatalogSeeder.SeedAsync(context);
        if (importedOfficialStreets > 0)
        {
            app.Logger.LogInformation(
                "Imported {StreetCount} official Tbilisi street catalog records.",
                importedOfficialStreets);
        }

        var repairedLocations =
            await ApartmentLocationBackfill.RepairAsync(context);
        if (repairedLocations > 0)
        {
            app.Logger.LogInformation(
                "Automatically repaired the locations of {ApartmentCount} apartments.",
                repairedLocations);
        }

        var roleManager =
            services.GetRequiredService<
                RoleManager<IdentityRole>>();

        var userManager =
            services.GetRequiredService<
                UserManager<AppUser>>();

        string[] roles =
        [
            "Admin",
            "Manager",
            "Agent",
            "Uploader",
            "User",
            "Client",
            "Owner"
        ];

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var roleResult =
                    await roleManager.CreateAsync(
                        new IdentityRole(role));

                if (!roleResult.Succeeded)
                {
                    var errors = string.Join(
                        ", ",
                        roleResult.Errors.Select(
                            error => error.Description));

                    throw new InvalidOperationException(
                        $"Could not create role '{role}': {errors}");
                }
            }
        }

        // Client accounts (Velven Discover): every existing account that is not staff and has no
        // role yet besides "User". Staff (Admin/Manager/Agent/Uploader, or IsAgent) is never touched.
        var newClients = await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
            SELECT u."Id", r."Id"
            FROM "AspNetUsers" u CROSS JOIN "AspNetRoles" r
            WHERE r."NormalizedName" = 'CLIENT'
              AND NOT u."IsAgent"
              AND NOT EXISTS (
                SELECT 1 FROM "AspNetUserRoles" ur
                JOIN "AspNetRoles" sr ON sr."Id" = ur."RoleId"
                WHERE ur."UserId" = u."Id"
                  AND sr."NormalizedName" IN ('ADMIN', 'MANAGER', 'AGENT', 'UPLOADER', 'CLIENT', 'OWNER'))
            """);
        if (newClients > 0)
            app.Logger.LogInformation("Gave the Client role to {Count} existing account(s).", newClients);

        var adminEmail =
            builder.Configuration["Admin:Email"]
            ?? "admin@whitetower.com";

        var adminPassword =
            builder.Configuration["Admin:Password"];

        var admin =
            await userManager.FindByEmailAsync(adminEmail);

        if (admin == null)
        {
            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                throw new InvalidOperationException(
                    "Admin password is missing. Add Admin__Password in Railway.");
            }

            admin = new AppUser
            {
                UserName = "admin",
                Email = adminEmail,
                FullName = "Administrator",
                EmailConfirmed = true
            };

            var createAdminResult =
                await userManager.CreateAsync(
                    admin,
                    adminPassword);

            if (!createAdminResult.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    createAdminResult.Errors.Select(
                        error => error.Description));

                throw new InvalidOperationException(
                    $"Could not create admin: {errors}");
            }

            await userManager.AddToRoleAsync(
                admin,
                "Admin");
        }
        else if (!await userManager.IsInRoleAsync(
                     admin,
                     "Admin"))
        {
            await userManager.AddToRoleAsync(
                admin,
                "Admin");
        }
    }
    catch (Exception exception)
    {
        app.Logger.LogCritical(
            exception,
            "Database migration or initial data creation failed.");

        throw;
    }
}

// =========================
// HTTP PIPELINE
// =========================

app.UseResponseCompression();

// Security headers. The API returns JSON, so its CSP (report-only for now) allows nothing; the website's own
// headers are set in site/server.mjs. HSTS also needs to be on in Cloudflare (SSL/TLS > Edge Certificates).
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    headers["X-Frame-Options"] = "DENY";
    headers["X-Content-Type-Options"] = "nosniff";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    // Swagger UI needs scripts and styles; skip the strict policy there.
    if (!context.Request.Path.StartsWithSegments("/swagger"))
        headers["Content-Security-Policy-Report-Only"] = "default-src 'none'; frame-ancestors 'none'";
    await next();
});
app.UseStaticFiles();

app.UseRouting();
app.UseCors("AngularPolicy");
app.UseRateLimiter();
app.UseOutputCache();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
