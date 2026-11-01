using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.Models;

namespace Website_API.Services;

public static class CrmLeadNotifications
{
    /// <summary>Enqueue in the lead's transaction; never notify the person making the change.</summary>
    public static async Task EnqueueStatusChangeAsync(AppDbContext db, CrmLead lead,
        string actorId, CrmLeadStatus oldStatus, CrmLeadStatus newStatus, DateTime now,
        CancellationToken cancellationToken)
    {
        if (oldStatus == newStatus) return;
        var recipients = await (from userRole in db.UserRoles
            join role in db.Roles on userRole.RoleId equals role.Id
            where role.NormalizedName == "ADMIN" || role.NormalizedName == "MANAGER"
            select userRole.UserId).Distinct().ToListAsync(cancellationToken);
        if (lead.AssignedAgentId is not null) recipients.Add(lead.AssignedAgentId);
        var actorName = await db.Users.Where(user => user.Id == actorId)
            .Select(user => user.FullName ?? user.UserName ?? "A team member")
            .FirstOrDefaultAsync(cancellationToken) ?? "A team member";
        foreach (var recipient in recipients.Where(id => id != actorId).Distinct())
            db.CrmLeadNotifications.Add(new CrmLeadNotification
            {
                UserId = recipient, Lead = lead, ActorName = actorName,
                FromStatus = oldStatus.ToApiValue(), ToStatus = newStatus.ToApiValue(), CreatedAt = now,
            });
    }
}
