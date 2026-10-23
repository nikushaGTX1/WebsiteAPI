using System.Net;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Website_API.Data;
using Website_API.Models;

namespace Website_API.Services;

/// <summary>
/// Listings waiting to be checked against saved Velven Match searches. Controllers add a listing
/// when it is published, approved or edited; <see cref="SavedSearchMatchWorker"/> does the work
/// in the background so publishing stays fast.
/// </summary>
public class ApartmentMatchQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
    {
        SingleReader = true,
    });

    public void Enqueue(int apartmentId) => _channel.Writer.TryWrite(apartmentId);

    public ChannelReader<int> Reader => _channel.Reader;
}

/// <summary>Checks one listing against every saved search and records new matches and alerts.</summary>
public class SavedSearchMatcher
{
    private readonly AppDbContext _context;
    private readonly ILogger<SavedSearchMatcher> _logger;

    public SavedSearchMatcher(AppDbContext context, ILogger<SavedSearchMatcher> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Idempotent: running it again for the same listing never adds a second match or alert
    /// (unique indexes back this up if two runs overlap).
    /// </summary>
    public async Task<int> MatchApartmentAsync(int apartmentId, CancellationToken cancellationToken)
    {
        var apartment = await ApartmentCatalogue.Published(_context.Apartments.AsNoTracking())
            .FirstOrDefaultAsync(item => item.Id == apartmentId, cancellationToken);

        if (apartment is null)
        {
            // No longer public: drop it from every saved search and from unread alerts.
            await RemoveMatchesAsync(apartmentId, null, cancellationToken);
            return 0;
        }

        var publishedAt = apartment.PublishedAt ?? apartment.LastConfirmedAt ?? apartment.CreatedAt;
        var searches = await _context.SavedSearches
            .AsNoTracking()
            .Include(search => search.User)
            .Where(search => search.CriteriaSince <= publishedAt)
            .Where(search => _context.UserRoles.Any(ur => ur.UserId == search.UserId &&
                _context.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == "CLIENT")) &&
                !_context.UserRoles.Any(ur => ur.UserId == search.UserId && _context.Roles.Any(r => r.Id == ur.RoleId &&
                    (r.NormalizedName == "ADMIN" || r.NormalizedName == "MANAGER" || r.NormalizedName == "AGENT" || r.NormalizedName == "UPLOADER" || r.NormalizedName == "CRM-MANAGER" || r.NormalizedName == "CRM_MANAGER"))))
            .ToListAsync(cancellationToken);
        var existing = await _context.SavedSearchMatches
            .Where(match => match.ApartmentId == apartmentId)
            .Select(match => match.SavedSearchId)
            .ToListAsync(cancellationToken);
        var alreadyMatched = existing.ToHashSet();

        var today = DateTime.UtcNow.Date;
        var created = 0;
        foreach (var search in searches)
        {
            if (!SavedSearchProfile.IsValid(search.ProfileJson))
            {
                await RemoveMatchesAsync(apartmentId, search.Id, cancellationToken);
                continue;
            }
            var profile = SavedSearchProfile.Parse(search.ProfileJson);
            var exact = HomeMatchRequirements.Evaluate(apartment, profile, today) == RequirementStatus.Exact;

            if (!exact)
            {
                if (alreadyMatched.Contains(search.Id))
                    await RemoveMatchesAsync(apartmentId, search.Id, cancellationToken);
                continue;
            }
            if (alreadyMatched.Contains(search.Id)) continue;

            _context.SavedSearchMatches.Add(new SavedSearchMatch
            {
                SavedSearchId = search.Id,
                ApartmentId = apartmentId,
            });

            var user = search.User;
            if (search.NotificationsEnabled && (user.MatchAlertsInApp || user.MatchAlertsEmail))
            {
                var previousAlert = await _context.ClientNotifications.AnyAsync(n =>
                    n.SavedSearchId == search.Id && n.ApartmentId == apartmentId, cancellationToken);
                if (!previousAlert) _context.ClientNotifications.Add(new ClientNotification
                {
                    UserId = search.UserId,
                    SavedSearchId = search.Id,
                    ApartmentId = apartmentId,
                    // Email-only clients keep a history entry but no unread bell badge.
                    ReadAt = user.MatchAlertsInApp ? null : DateTime.UtcNow,
                    EmailPending = user.MatchAlertsEmail && !string.IsNullOrWhiteSpace(user.Email),
                });
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                created++;
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Another run recorded it first; keep theirs.
                _context.ChangeTracker.Clear();
            }
        }

        if (created > 0)
            _logger.LogInformation("Listing {ApartmentId} matched {Count} saved search(es).", apartmentId, created);
        return created;
    }

    private async Task RemoveMatchesAsync(int apartmentId, int? searchId, CancellationToken cancellationToken)
    {
        await _context.ClientNotifications
            .Where(n => n.ApartmentId == apartmentId && (searchId == null || n.SavedSearchId == searchId))
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.EmailPending, false), cancellationToken);
        await _context.SavedSearchMatches
            .Where(match => match.ApartmentId == apartmentId && (searchId == null || match.SavedSearchId == searchId))
            .ExecuteDeleteAsync(cancellationToken);
        // Read alerts stay in the history; unread ones would point at a home that no longer matches.
        await _context.ClientNotifications
            .Where(notification => notification.ApartmentId == apartmentId &&
                                   notification.ReadAt == null &&
                                   (searchId == null || notification.SavedSearchId == searchId))
            .ExecuteDeleteAsync(cancellationToken);
    }
}

/// <summary>
/// Runs the matching queue, re-checks recently published listings every 10 minutes (so a
/// restart never loses a listing) and sends grouped alert emails.
/// </summary>
public class SavedSearchMatchWorker : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan SweepWindow = TimeSpan.FromDays(3);
    /// <summary>Alerts wait this long before emailing, so homes published together share one email.</summary>
    private static readonly TimeSpan EmailGrouping = TimeSpan.FromMinutes(5);

    private readonly ApartmentMatchQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<SavedSearchMatchWorker> _logger;
    private DateTime _nextSweep = DateTime.UtcNow.AddSeconds(30);

    public SavedSearchMatchWorker(ApartmentMatchQueue queue, IServiceScopeFactory scopes, ILogger<SavedSearchMatchWorker> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                wait.CancelAfter(TimeSpan.FromMinutes(1));
                try
                {
                    var apartmentId = await _queue.Reader.ReadAsync(wait.Token);
                    await RunScopedAsync((matcher, _) => matcher.MatchApartmentAsync(apartmentId, stoppingToken));
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // Nothing queued this minute.
                }

                if (DateTime.UtcNow >= _nextSweep)
                {
                    _nextSweep = DateTime.UtcNow + SweepInterval;
                    await SweepAsync(stoppingToken);
                }
                await SendEmailDigestsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Saved-search matching failed; retrying shortly.");
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }
    }

    private async Task RunScopedAsync(Func<SavedSearchMatcher, IServiceProvider, Task> work)
    {
        using var scope = _scopes.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<SavedSearchMatcher>(), scope.ServiceProvider);
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await context.SavedSearches.AnyAsync(cancellationToken)) return;
        var since = DateTime.UtcNow - SweepWindow;
        var recent = await ApartmentCatalogue.Published(context.Apartments.AsNoTracking())
            .Where(apartment => apartment.PublishedAt >= since)
            .Select(apartment => apartment.Id)
            .ToListAsync(cancellationToken);
        var matcher = scope.ServiceProvider.GetRequiredService<SavedSearchMatcher>();
        foreach (var id in recent) await matcher.MatchApartmentAsync(id, cancellationToken);
    }

    private async Task SendEmailDigestsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cutoff = DateTime.UtcNow - EmailGrouping;
        var userIds = await context.ClientNotifications
            .Where(notification => notification.EmailPending && notification.CreatedAt <= cutoff)
            .Select(notification => notification.UserId)
            .Distinct()
            .Take(20)
            .ToListAsync(cancellationToken);
        if (userIds.Count == 0) return;

        var email = scope.ServiceProvider.GetRequiredService<ResendEmailService>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var siteUrl = (config["Site:BaseUrl"] ?? "https://velven.ge").TrimEnd('/');

        foreach (var userId in userIds)
        {
            var pending = await context.ClientNotifications
                .Include(notification => notification.User)
                .Include(notification => notification.Apartment)
                .Include(notification => notification.SavedSearch)
                .Where(notification => notification.UserId == userId && notification.EmailPending)
                .OrderBy(notification => notification.CreatedAt)
                .ToListAsync(cancellationToken);
            if (pending.Count == 0) continue;
            var user = pending[0].User;
            var roles = await (from ur in context.UserRoles join r in context.Roles on ur.RoleId equals r.Id
                               where ur.UserId == userId select r.NormalizedName).ToListAsync(cancellationToken);
            var eligible = roles.Contains("CLIENT") && !roles.Any(r => r is "ADMIN" or "MANAGER" or "AGENT" or "UPLOADER" or "CRM-MANAGER" or "CRM_MANAGER");
            var sendable = pending.Where(item => eligible && item.Apartment is not null && item.Apartment.IsApproved &&
                item.SavedSearch is { NotificationsEnabled: true } &&
                SavedSearchProfile.IsValid(item.SavedSearch.ProfileJson) &&
                HomeMatchRequirements.Evaluate(item.Apartment, SavedSearchProfile.Parse(item.SavedSearch.ProfileJson), DateTime.UtcNow.Date) == RequirementStatus.Exact).ToList();

            var sent = !user.MatchAlertsEmail || sendable.Count == 0 || string.IsNullOrWhiteSpace(user.Email)
                ? true // Turned off meanwhile, or nothing left to tell: just clear the queue.
                : await email.SendAsync(
                    user.Email!,
                    MatchEmail.Subject(user.PreferredLanguage, sendable.Count),
                    MatchEmail.Html(user, sendable, siteUrl),
                    cancellationToken);

            var giveUp = pending.All(item => item.CreatedAt < DateTime.UtcNow.AddDays(-1));
            if (!sent && !giveUp) continue; // Retried next minute; after a day the alert stays on the website only.
            foreach (var item in pending)
            {
                item.EmailPending = false;
                item.EmailedAt = sent ? DateTime.UtcNow : null;
            }
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>The new-match email, in the client's language.</summary>
public static class MatchEmail
{
    private static string Lang(string? language) => language is "ka" or "ru" ? language : "en";

    public static string Subject(string? language, int count) => Lang(language) switch
    {
        "ka" => count == 1 ? "ახალი ბინა თქვენს ძიებას ემთხვევა" : $"{count} ახალი ბინა თქვენს ძიებას ემთხვევა",
        "ru" => count == 1 ? "Новая квартира по вашему поиску" : $"Новые квартиры по вашему поиску: {count}",
        _ => count == 1 ? "A new home matches your search" : $"{count} new homes match your search",
    };

    public static string Html(AppUser user, IReadOnlyList<ClientNotification> items, string siteUrl)
    {
        var lang = Lang(user.PreferredLanguage);
        var (heading, intro, button, footer, perMonth) = lang switch
        {
            "ka" => ("ახალი შესატყვისი ბინები", "Velven Discover-მა იპოვა ბინები, რომლებიც თქვენს შენახულ ძიებას ემთხვევა.", "ბინის ნახვა", "ამ წერილებს იღებთ, რადგან ჩართული გაქვთ ელფოსტით შეტყობინებები. გამორთვა შეგიძლიათ პროფილის პარამეტრებში.", " / თვეში"),
            "ru" => ("Новые совпадения", "Velven Discover нашёл квартиры, подходящие под ваш сохранённый поиск.", "Посмотреть квартиру", "Вы получаете это письмо, потому что включили уведомления по email. Отключить их можно в настройках профиля.", " / мес."),
            _ => ("New homes for you", "Velven Discover found homes that match your saved search.", "View Match", "You get these emails because you turned on email alerts. You can turn them off in your profile settings.", " / month"),
        };
        var name = WebUtility.HtmlEncode(user.FullName ?? "");
        var rows = string.Join("", items.Take(10).Select(item =>
        {
            var apartment = item.Apartment!;
            var title = WebUtility.HtmlEncode(apartment.Title);
            var place = WebUtility.HtmlEncode(string.Join(", ", new[] { apartment.District, apartment.City }.Where(part => !string.IsNullOrWhiteSpace(part))));
            var rent = HomeMatchRequirements.MatchesPropertyGoal(apartment, new SavedSearchProfile { PropertyGoal = "Rent" });
            var price = $"${apartment.Price:N0}{(rent ? perMonth : "")}";
            var link = $"{siteUrl}/discover?search={item.SavedSearchId}&apartment={apartment.Id}";
            return $"""
                <tr><td style="padding:14px 0;border-bottom:1px solid #eee9f6">
                  <div style="font-size:15px;font-weight:700;color:#160f2b">{title}</div>
                  <div style="margin:4px 0 10px;font-size:13px;color:#6b6480">{place} · <b style="color:#451a8f">{price}</b></div>
                  <a href="{link}" style="display:inline-block;padding:9px 16px;border-radius:10px;background:#451a8f;color:#fff;font-size:13px;font-weight:700;text-decoration:none">{button}</a>
                </td></tr>
                """;
        }));
        return $"""
            <!doctype html>
            <html><body style="margin:0;padding:0;background:#f4f1fb">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f4f1fb;padding:32px 12px;font-family:Arial,Helvetica,sans-serif">
              <tr><td align="center">
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;background:#ffffff;border-radius:20px;overflow:hidden;box-shadow:0 12px 40px rgba(37,20,74,0.12)">
                  <tr><td style="background:#451a8f;padding:24px;color:#fff;font-size:22px;font-weight:700;letter-spacing:1px;text-align:center">VELVEN</td></tr>
                  <tr><td style="padding:28px 28px 8px;color:#1b1626">
                    <h1 style="margin:0 0 8px;font-size:21px;color:#160f2b">{heading}{(name != "" ? $", {name}" : "")}</h1>
                    <p style="margin:0 0 8px;font-size:14px;line-height:1.6;color:#4b4560">{intro}</p>
                    <table role="presentation" width="100%" cellpadding="0" cellspacing="0">{rows}</table>
                  </td></tr>
                  <tr><td style="padding:18px 28px 26px;font-size:12px;line-height:1.6;color:#9a93ab;text-align:center">{footer}<br /><a href="{siteUrl}" style="color:#451a8f;font-weight:700;text-decoration:none">velven.ge</a></td></tr>
                </table>
              </td></tr>
            </table>
            </body></html>
            """;
    }
}
