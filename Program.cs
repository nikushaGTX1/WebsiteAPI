using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using Website_API.Data;
using Website_API.Models;
using Website_API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("CrmInquiries", context =>
    {
        // The public website proxies API calls, so the socket address belongs
        // to that proxy. Use its forwarded client IP when it is a valid IP;
        // direct callers fall back to their actual remote address.
        var forwardedValue = context.Request.Headers["X-Forwarded-For"]
            .ToString()
            .Split(',', 2)[0]
            .Trim();
        var partitionKey = IPAddress.TryParse(forwardedValue, out var clientIp)
            ? clientIp.ToString()
            : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

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
builder.Services.AddHttpClient<SupabaseStorageService>();
builder.Services.AddHttpClient("OpenStreetMap", client =>
{
    client.Timeout = TimeSpan.FromMinutes(4);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "VelvenRealEstate/1.0 (street geometry import)");
});
builder.Services.AddSingleton<StreetGeometryImportService>();
builder.Services.AddHostedService(serviceProvider =>
    serviceProvider.GetRequiredService<StreetGeometryImportService>());
builder.Services.AddScoped<HomeMatchScorer>();

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
                "http://192.168.56.1:3000/",
                "http://192.168.56.1",
                "http://localhost:4200/"
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
            "User"
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
app.UseStaticFiles();

app.UseRouting();
app.UseCors("AngularPolicy");
app.UseRateLimiter();
app.UseOutputCache();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
