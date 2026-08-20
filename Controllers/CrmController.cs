using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using Website_API.Data;
using Website_API.DTO;
using Website_API.Models;

namespace Website_API.Controllers;

[ApiController]
[Route("api/Crm")]
public class CrmController : ControllerBase
{
    private const string CrmReadRoles = "Admin,Manager,Agent,Uploader";
    private const string CrmCreateRoles = "Admin,Manager,Agent,Uploader";
    private const string CrmWriteRoles = "Admin,Manager,Agent";
    private const string CrmManagerRoles = "Admin,Manager";

    private readonly AppDbContext _context;
    private readonly UserManager<AppUser> _userManager;

    public CrmController(
        AppDbContext context,
        UserManager<AppUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [Authorize(Roles = CrmReadRoles)]
    [HttpGet("leads")]
    public async Task<ActionResult<IReadOnlyList<CrmLeadListItemDto>>> GetLeads(
        [FromQuery] string? status = null,
        [FromQuery] string? source = null,
        [FromQuery] string? assignedAgentId = null,
        [FromQuery] int? apartmentId = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? createdFrom = null,
        [FromQuery] DateTime? createdTo = null,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId();
        if (userId is null)
            return Unauthorized();

        CrmLeadStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!CrmEnumText.TryParseLeadStatus(status, out var value))
            {
                return BadRequest(new
                {
                    message =
                        "Invalid status. Use new, contacted, qualified, viewing, " +
                        "negotiation, won, or lost."
                });
            }

            parsedStatus = value;
        }

        CrmLeadSource? parsedSource = null;
        if (!string.IsNullOrWhiteSpace(source))
        {
            if (!CrmEnumText.TryParseLeadSource(source, out var value))
            {
                return BadRequest(new
                {
                    message =
                        "Invalid source. Use website, manual, phone, referral, " +
                        "or ai-match."
                });
            }

            parsedSource = value;
        }

        var hasFullAccess = HasFullCrmAccess();
        var normalizedAssignedAgentId = NormalizeOptional(assignedAgentId);

        if (!hasFullAccess &&
            normalizedAssignedAgentId is not null &&
            normalizedAssignedAgentId != userId)
        {
            return Forbid();
        }

        if (hasFullAccess && normalizedAssignedAgentId is not null &&
            await FindAgentAsync(
                normalizedAssignedAgentId,
                cancellationToken) is null)
        {
            return BadRequest(new { message = "Assigned agent is not valid." });
        }

        DateTime? normalizedCreatedFrom = createdFrom.HasValue
            ? ToUtc(createdFrom.Value)
            : null;
        DateTime? normalizedCreatedTo = createdTo.HasValue
            ? ToUtc(createdTo.Value)
            : null;

        if (normalizedCreatedFrom > normalizedCreatedTo)
        {
            return BadRequest(new
            {
                message = "createdFrom cannot be later than createdTo."
            });
        }

        var query = AccessibleLeads(userId)
            .AsNoTracking();

        if (parsedStatus.HasValue)
            query = query.Where(lead => lead.Status == parsedStatus.Value);
        if (parsedSource.HasValue)
            query = query.Where(lead => lead.Source == parsedSource.Value);
        if (normalizedAssignedAgentId is not null)
        {
            query = query.Where(
                lead => lead.AssignedAgentId == normalizedAssignedAgentId);
        }
        if (apartmentId.HasValue)
            query = query.Where(lead => lead.ApartmentId == apartmentId.Value);
        if (normalizedCreatedFrom.HasValue)
        {
            query = query.Where(
                lead => lead.CreatedAt >= normalizedCreatedFrom.Value);
        }
        if (normalizedCreatedTo.HasValue)
        {
            query = query.Where(
                lead => lead.CreatedAt <= normalizedCreatedTo.Value);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(lead =>
                EF.Functions.ILike(lead.Name, pattern) ||
                (lead.Email != null && EF.Functions.ILike(lead.Email, pattern)) ||
                (lead.Phone != null && EF.Functions.ILike(lead.Phone, pattern)) ||
                (lead.Message != null && EF.Functions.ILike(lead.Message, pattern)));
        }

        var rows = await query
            .OrderByDescending(lead => lead.UpdatedAt)
            .Select(lead => new
            {
                lead.Id,
                lead.Name,
                lead.Email,
                lead.Phone,
                lead.Status,
                lead.Source,
                lead.Goal,
                lead.PreferredContactMethod,
                lead.PreferredDistricts,
                lead.PreferredPropertyType,
                lead.Bedrooms,
                lead.BudgetMin,
                lead.BudgetMax,
                lead.Currency,
                lead.ApartmentId,
                ApartmentTitle = lead.Apartment == null
                    ? null
                    : lead.Apartment.Title,
                lead.AssignedAgentId,
                AssignedAgentName = lead.AssignedAgent == null
                    ? null
                    : lead.AssignedAgent.FullName ?? lead.AssignedAgent.UserName,
                UploaderUserId = lead.Apartment == null
                    ? null
                    : lead.Apartment.UploadedByUserId,
                lead.CustomerUserId,
                lead.CreatedByUserId,
                NextFollowUpAt = lead.Tasks
                    .Where(task =>
                        task.CompletedAt == null &&
                        task.Type == CrmTaskType.FollowUp)
                    .OrderBy(task => task.DueAt)
                    .Select(task => (DateTime?)task.DueAt)
                    .FirstOrDefault(),
                NextTask = lead.Tasks
                    .Where(task => task.CompletedAt == null)
                    .OrderBy(task => task.DueAt)
                    .Select(task => new
                    {
                        task.Id,
                        task.LeadId,
                        task.Type,
                        task.Title,
                        task.Details,
                        task.DueAt,
                        task.CompletedAt,
                        task.AssignedAgentId,
                        AssignedAgentName = task.AssignedAgent == null
                            ? null
                            : task.AssignedAgent.FullName ??
                                task.AssignedAgent.UserName,
                        task.CreatedByUserId,
                        task.CreatedAt,
                        task.UpdatedAt
                    })
                    .FirstOrDefault(),
                LastActivityAt = lead.Activities
                    .OrderByDescending(activity => activity.CreatedAt)
                    .Select(activity => (DateTime?)activity.CreatedAt)
                    .FirstOrDefault(),
                lead.CreatedAt,
                lead.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var response = rows.Select(row => new CrmLeadListItemDto
        {
            Id = row.Id,
            FullName = row.Name,
            Email = row.Email,
            PhoneNumber = row.Phone,
            Status = row.Status.ToApiValue(),
            Source = row.Source.ToApiValue(),
            Goal = row.Goal,
            PreferredContactMethod = row.PreferredContactMethod,
            PreferredDistricts = [.. row.PreferredDistricts],
            PreferredPropertyType = row.PreferredPropertyType,
            Bedrooms = row.Bedrooms,
            BudgetMin = row.BudgetMin,
            BudgetMax = row.BudgetMax,
            Currency = row.Currency,
            ApartmentId = row.ApartmentId,
            ApartmentTitle = row.ApartmentTitle,
            AssignedAgentId = row.AssignedAgentId,
            AssignedAgentName = row.AssignedAgentName,
            UploaderUserId = row.UploaderUserId,
            CustomerUserId = row.CustomerUserId,
            CreatedByUserId = row.CreatedByUserId,
            NextFollowUpAt = row.NextFollowUpAt,
            NextTask = row.NextTask is null
                ? null
                : new CrmTaskResponseDto
                {
                    Id = row.NextTask.Id,
                    LeadId = row.NextTask.LeadId,
                    Type = row.NextTask.Type.ToApiValue(),
                    Title = row.NextTask.Title,
                    Description = row.NextTask.Details,
                    DueAt = row.NextTask.DueAt,
                    CompletedAt = row.NextTask.CompletedAt,
                    Status = "open",
                    AssignedAgentId = row.NextTask.AssignedAgentId,
                    AssignedAgentName = row.NextTask.AssignedAgentName,
                    CreatedByUserId = row.NextTask.CreatedByUserId,
                    CreatedAt = row.NextTask.CreatedAt,
                    UpdatedAt = row.NextTask.UpdatedAt
                },
            LastActivityAt = row.LastActivityAt,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt
        }).ToList();

        return Ok(response);
    }

    [Authorize(Roles = CrmReadRoles)]
    [HttpGet("leads/{id:int}")]
    public async Task<ActionResult<CrmLeadDetailDto>> GetLead(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null)
            return Unauthorized();

        var lead = await LoadLeadDetailsAsync(id, userId, cancellationToken);
        if (lead is null)
            return NotFound(new { message = "Lead not found." });

        return Ok(ToLeadDetail(lead));
    }

    [Authorize(Roles = CrmReadRoles)]
    [HttpGet("metrics")]
    public async Task<ActionResult<CrmMetricsDto>> GetMetrics(
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null)
            return Unauthorized();

        var leadQuery = AccessibleLeads(userId).AsNoTracking();
        var groupedStatuses = await leadQuery
            .GroupBy(lead => lead.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var counts = groupedStatuses.ToDictionary(
            item => item.Status,
            item => item.Count);
        var totalLeads = groupedStatuses.Sum(item => item.Count);
        var wonLeads = Count(CrmLeadStatus.Won);
        var lostLeads = Count(CrmLeadStatus.Lost);
        var closedLeads = wonLeads + lostLeads;

        var accessibleLeadIds = AccessibleLeads(userId)
            .Select(lead => lead.Id);
        var taskQuery = _context.CrmTasks
            .AsNoTracking()
            .Where(task => accessibleLeadIds.Contains(task.LeadId));
        var now = DateTime.UtcNow;
        var today = now.Date;
        var tomorrow = today.AddDays(1);

        var overdueTasks = await taskQuery.CountAsync(
            task => task.CompletedAt == null && task.DueAt < now,
            cancellationToken);
        var dueTodayTasks = await taskQuery.CountAsync(
            task =>
                task.CompletedAt == null &&
                task.DueAt >= today &&
                task.DueAt < tomorrow,
            cancellationToken);
        var upcomingViewings = await taskQuery.CountAsync(
            task =>
                task.CompletedAt == null &&
                task.Type == CrmTaskType.Viewing &&
                task.DueAt >= now,
            cancellationToken);
        var unassignedLeads = HasFullCrmAccess()
            ? await leadQuery.CountAsync(
                lead => lead.AssignedAgentId == null,
                cancellationToken)
            : 0;

        return Ok(new CrmMetricsDto
        {
            TotalLeads = totalLeads,
            NewLeads = Count(CrmLeadStatus.New),
            ActiveLeads = totalLeads - closedLeads,
            UnassignedLeads = unassignedLeads,
            OverdueTasks = overdueTasks,
            DueTodayTasks = dueTodayTasks,
            UpcomingViewings = upcomingViewings,
            WonLeads = wonLeads,
            ConversionRate = closedLeads == 0
                ? 0
                : Math.Round(wonLeads * 100d / closedLeads, 1),
            StatusCounts = new CrmLeadStatusCountsDto
            {
                New = Count(CrmLeadStatus.New),
                Contacted = Count(CrmLeadStatus.Contacted),
                Qualified = Count(CrmLeadStatus.Qualified),
                Viewing = Count(CrmLeadStatus.Viewing),
                Negotiation = Count(CrmLeadStatus.Negotiation),
                Won = wonLeads,
                Lost = lostLeads
            }
        });

        int Count(CrmLeadStatus statusValue) =>
            counts.GetValueOrDefault(statusValue);
    }

    // =========================================================
    // QUESTIONNAIRE REFERRAL LINKS
    // =========================================================

    [Authorize(Roles = CrmCreateRoles)]
    [HttpPost("questionnaire-links")]
    public async Task<IActionResult> GenerateQuestionnaireLink(
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null)
            return Unauthorized();

        var ownerExists = await UserExistsAsync(userId, cancellationToken);
        if (!ownerExists)
        {
            return BadRequest(new
            {
                message = "The CRM account connected to this link is not available."
            });
        }

        string token;

        do
        {
            token = GenerateQuestionnaireToken();
        }
        while (await _context.CrmQuestionnaireLinks
            .AnyAsync(item => item.Token == token, cancellationToken));

        var link = new CrmQuestionnaireLink
        {
            Token = token,
            AgentUserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = null,
            IsActive = true,
            Uses = 0
        };

        _context.CrmQuestionnaireLinks.Add(link);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            token,
            path = $"/crm-questioner/agent-{token}"
        });
    }


    // =========================================================
    // PUBLIC QUESTIONNAIRE SUBMISSION
    // =========================================================

    [AllowAnonymous]
    [EnableRateLimiting("CrmInquiries")]
    [HttpPost("questionnaire-leads/{token}")]
    public async Task<ActionResult<PublicCrmInquiryResponseDto>>
        CreateQuestionnaireLead(
            string token,
            [FromBody] CreateCrmLeadDto dto,
            CancellationToken cancellationToken)
    {
        token = NormalizeQuestionnaireToken(token);

        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new
            {
                message = "Questionnaire link is invalid."
            });
        }

        var now = DateTime.UtcNow;

        var questionnaireLink = await _context.CrmQuestionnaireLinks
            .FirstOrDefaultAsync(
                item =>
                    item.Token == token &&
                    item.IsActive &&
                    (!item.ExpiresAt.HasValue || item.ExpiresAt.Value > now),
                cancellationToken);

        if (questionnaireLink is null)
        {
            return BadRequest(new
            {
                message = "Questionnaire link is invalid, disabled, or expired."
            });
        }

        // Links may belong to a manager, agent, or uploader. Only an Agent
        // receives assignment; other CRM roles retain ownership as creator.
        var linkOwner = await _context.Users.FirstOrDefaultAsync(
            user => user.Id == questionnaireLink.AgentUserId,
            cancellationToken);
        if (linkOwner is null)
        {
            return BadRequest(new
            {
                message = "The CRM account connected to this questionnaire link is no longer available."
            });
        }
        var ownerRoles = await _userManager.GetRolesAsync(linkOwner);
        var ownerIsAgent = linkOwner.IsAgent && ownerRoles.Contains("Agent");
        var ownerHasCrmAccess = ownerIsAgent || ownerRoles.Any(role =>
            role is "Admin" or "Manager" or "Uploader");
        if (!ownerHasCrmAccess)
        {
            return BadRequest(new
            {
                message = "The CRM account connected to this questionnaire link no longer has access."
            });
        }

        if (!dto.ConsentGiven)
        {
            return BadRequest(new
            {
                message = "Consent to contact is required."
            });
        }

        var validationError = ValidateLeadDetails(
            dto.FullName,
            dto.Email,
            dto.PhoneNumber,
            dto.BudgetMin,
            dto.BudgetMax,
            dto.Currency,
            dto.PreferredDistricts);

        if (validationError is not null)
            return BadRequest(new { message = validationError });

        if (dto.ApartmentId.HasValue &&
            !await ApartmentExistsAsync(
                dto.ApartmentId.Value,
                cancellationToken))
        {
            return BadRequest(new
            {
                message = "Apartment is not valid."
            });
        }

        DateTime? requestedViewingAt = dto.RequestedViewingAt.HasValue
            ? ToUtc(dto.RequestedViewingAt.Value)
            : null;

        var lead = new CrmLead
        {
            Name = dto.FullName.Trim(),
            Email = NormalizeEmail(dto.Email),
            Phone = NormalizeOptional(dto.PhoneNumber),

            // Public questionnaire submissions cannot choose their own
            // status/source/assignment. The backend controls them.
            Status = CrmLeadStatus.New,
            Source = CrmLeadSource.Website,

            Goal = NormalizeOptional(dto.Goal) ?? "rent",
            PreferredContactMethod =
                NormalizeOptional(dto.PreferredContactMethod),
            PreferredDistricts =
                NormalizeDistricts(dto.PreferredDistricts),
            PreferredPropertyType =
                NormalizeOptional(dto.PreferredPropertyType),
            Bedrooms = dto.Bedrooms,
            BudgetMin = dto.BudgetMin,
            BudgetMax = dto.BudgetMax,
            Currency = dto.Currency.Trim().ToUpperInvariant(),
            Preferences = NormalizeOptional(dto.Preferences),
            Message = NormalizeOptional(dto.Message),
            RequestedViewingAt = requestedViewingAt,
            ApartmentId = dto.ApartmentId,

            // Do not trust CustomerUserId or AssignedAgentId from the
            // anonymous browser. Assignment comes only from the token.
            CustomerUserId = null,
            AssignedAgentId = ownerIsAgent ? questionnaireLink.AgentUserId : null,
            CreatedByUserId = ownerIsAgent ? null : questionnaireLink.AgentUserId,

            ConsentGiven = true,
            ConsentGivenAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        lead.Activities.Add(new CrmActivity
        {
            Type = CrmActivityType.Inquiry,
            Content =
                NormalizeOptional(dto.Message) ??
                "Questionnaire submitted through an agent referral link.",
            CreatedByUserId = null,
            CreatedAt = now
        });

        if (requestedViewingAt.HasValue)
        {
            lead.Tasks.Add(new CrmTask
            {
                Type = CrmTaskType.Viewing,
                Title = "Requested property viewing",
                Details = NormalizeOptional(dto.Message),
                DueAt = requestedViewingAt.Value,
                AssignedAgentId = ownerIsAgent ? questionnaireLink.AgentUserId : null,
                CreatedByUserId = null,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        questionnaireLink.Uses++;

        _context.CrmLeads.Add(lead);

        await _context.SaveChangesAsync(cancellationToken);

        return Accepted(new PublicCrmInquiryResponseDto
        {
            Received = true
        });
    }


    [Authorize(Roles = CrmCreateRoles)]
    [HttpPost("leads")]
    public async Task<ActionResult<CrmLeadDetailDto>> CreateLead(
        [FromBody] CreateCrmLeadDto dto,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null)
            return Unauthorized();

        var validationError = ValidateLeadDetails(
            dto.FullName,
            dto.Email,
            dto.PhoneNumber,
            dto.BudgetMin,
            dto.BudgetMax,
            dto.Currency,
            dto.PreferredDistricts);
        if (validationError is not null)
            return BadRequest(new { message = validationError });

        if (!CrmEnumText.TryParseLeadSource(dto.Source, out var source))
        {
            return BadRequest(new
            {
                message =
                    "Invalid source. Use website, manual, phone, referral, " +
                    "or ai-match."
            });
        }

        if (!CrmEnumText.TryParseLeadStatus(dto.Status, out var status))
        {
            return BadRequest(new
            {
                message =
                    "Invalid status. Use new, contacted, qualified, viewing, " +
                    "negotiation, won, or lost."
            });
        }

        if (dto.ApartmentId.HasValue &&
            !await ApartmentExistsAsync(dto.ApartmentId.Value, cancellationToken))
        {
            return BadRequest(new { message = "Apartment is not valid." });
        }

        var customerUserId = NormalizeOptional(dto.CustomerUserId);
        if (customerUserId is not null &&
            !await UserExistsAsync(customerUserId, cancellationToken))
        {
            return BadRequest(new { message = "Customer user is not valid." });
        }

        string? assignedAgentId;
        AppUser? assignedAgent = null;
        if (HasFullCrmAccess())
        {
            assignedAgentId = NormalizeOptional(dto.AssignedAgentId);
            if (assignedAgentId is not null)
            {
                assignedAgent = await FindAgentAsync(
                    assignedAgentId,
                    cancellationToken);
                if (assignedAgent is null)
                {
                    return BadRequest(new
                    {
                        message = "Assigned agent is not valid."
                    });
                }
            }
        }
        else if (User.IsInRole("Agent"))
        {
            assignedAgentId = userId;
        }
        else
        {
            // Uploaders can create leads, but only CRM agents may be assigned.
            assignedAgentId = null;
        }

        var now = DateTime.UtcNow;
        DateTime? requestedViewingAt = dto.RequestedViewingAt.HasValue
            ? ToUtc(dto.RequestedViewingAt.Value)
            : null;
        var lead = new CrmLead
        {
            Name = dto.FullName.Trim(),
            Email = NormalizeEmail(dto.Email),
            Phone = NormalizeOptional(dto.PhoneNumber),
            Status = status,
            Source = source,
            Goal = NormalizeOptional(dto.Goal),
            PreferredContactMethod =
                NormalizeOptional(dto.PreferredContactMethod),
            PreferredDistricts = NormalizeDistricts(dto.PreferredDistricts),
            PreferredPropertyType =
                NormalizeOptional(dto.PreferredPropertyType),
            Bedrooms = dto.Bedrooms,
            BudgetMin = dto.BudgetMin,
            BudgetMax = dto.BudgetMax,
            Currency = dto.Currency.Trim().ToUpperInvariant(),
            Preferences = NormalizeOptional(dto.Preferences),
            Message = NormalizeOptional(dto.Message),
            RequestedViewingAt = requestedViewingAt,
            ApartmentId = dto.ApartmentId,
            CustomerUserId = customerUserId,
            AssignedAgentId = assignedAgentId,
            CreatedByUserId = userId,
            ConsentGiven = dto.ConsentGiven,
            ConsentGivenAt = dto.ConsentGiven ? now : null,
            CreatedAt = now,
            UpdatedAt = now,
            ClosedAt = status is CrmLeadStatus.Won or CrmLeadStatus.Lost
                ? now
                : null
        };

        lead.Activities.Add(new CrmActivity
        {
            Type = CrmActivityType.System,
            Content = $"Lead created with status {status.ToApiValue()}.",
            CreatedByUserId = userId,
            CreatedAt = now
        });

        if (requestedViewingAt.HasValue)
        {
            lead.Tasks.Add(new CrmTask
            {
                Type = CrmTaskType.Viewing,
                Title = "Requested property viewing",
                Details = NormalizeOptional(dto.Message),
                DueAt = requestedViewingAt.Value,
                AssignedAgentId = assignedAgentId,
                CreatedByUserId = userId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        _context.CrmLeads.Add(lead);
        await _context.SaveChangesAsync(cancellationToken);

        var created = await LoadLeadDetailsAsync(
            lead.Id,
            userId,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetLead),
            new { id = lead.Id },
            ToLeadDetail(created!));
    }

    [Authorize(Roles = CrmWriteRoles)]
    [HttpPut("leads/{id:int}")]
    public async Task<ActionResult<CrmLeadDetailDto>> UpdateLead(
        int id,
        [FromBody] UpdateCrmLeadDto dto,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null)
            return Unauthorized();

        var lead = await AccessibleLeads(userId)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (lead is null)
            return NotFound(new { message = "Lead not found." });

        var validationError = ValidateLeadDetails(
            dto.FullName,
            dto.Email,
            dto.PhoneNumber,
            dto.BudgetMin,
            dto.BudgetMax,
            dto.Currency ?? lead.Currency,
            dto.PreferredDistricts);
        if (validationError is not null)
            return BadRequest(new { message = validationError });

        CrmLeadSource? source = null;
        if (dto.Source is not null)
        {
            if (!CrmEnumText.TryParseLeadSource(dto.Source, out var parsedSource))
            {
                return BadRequest(new
                {
                    message =
                        "Invalid source. Use website, manual, phone, referral, " +
                        "or ai-match."
                });
            }

            source = parsedSource;
        }

        if (dto.ApartmentId.HasValue &&
            !await ApartmentExistsAsync(dto.ApartmentId.Value, cancellationToken))
        {
            return BadRequest(new { message = "Apartment is not valid." });
        }

        lead.Name = dto.FullName.Trim();
        lead.Email = NormalizeEmail(dto.Email);
        lead.Phone = NormalizeOptional(dto.PhoneNumber);
        if (source.HasValue)
            lead.Source = source.Value;
        if (dto.Goal is not null)
            lead.Goal = NormalizeOptional(dto.Goal);
        lead.PreferredContactMethod =
            NormalizeOptional(dto.PreferredContactMethod);
        lead.PreferredDistricts = NormalizeDistricts(dto.PreferredDistricts);
        lead.PreferredPropertyType =
            NormalizeOptional(dto.PreferredPropertyType);
        lead.Bedrooms = dto.Bedrooms;
        lead.BudgetMin = dto.BudgetMin;
        lead.BudgetMax = dto.BudgetMax;
        if (dto.Currency is not null)
            lead.Currency = dto.Currency.Trim().ToUpperInvariant();
        if (dto.Preferences is not null)
            lead.Preferences = NormalizeOptional(dto.Preferences);
        lead.ApartmentId = dto.ApartmentId;
        var now = DateTime.UtcNow;
        lead.UpdatedAt = now;

        lead.Activities.Add(new CrmActivity
        {
            Type = CrmActivityType.System,
            Content = "Lead details updated.",
            CreatedByUserId = userId,
            CreatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);

        var updated = await LoadLeadDetailsAsync(id, userId, cancellationToken);
        return Ok(ToLeadDetail(updated!));
    }

    [Authorize(Roles = CrmManagerRoles)]
    [HttpDelete("leads/{id:int}")]
    public async Task<IActionResult> DeleteLead(
        int id,
        CancellationToken cancellationToken)
    {
        var lead = await _context.CrmLeads
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (lead is null)
            return NotFound(new { message = "Lead not found." });

        _context.CrmLeads.Remove(lead);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = CrmWriteRoles)]
    [HttpPatch("leads/{id:int}/status")]
    public async Task<ActionResult<CrmLeadDetailDto>> UpdateLeadStatus(
        int id,
        [FromBody] UpdateCrmLeadStatusDto dto,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null)
            return Unauthorized();

        if (!CrmEnumText.TryParseLeadStatus(dto.Status, out var status))
        {
            return BadRequest(new
            {
                message =
                    "Invalid status. Use new, contacted, qualified, viewing, " +
                    "negotiation, won, or lost."
            });
        }

        var lead = await AccessibleLeads(userId)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (lead is null)
            return NotFound(new { message = "Lead not found." });

        if (lead.Status != status)
        {
            var now = DateTime.UtcNow;
            var oldStatus = lead.Status;
            lead.Status = status;
            lead.UpdatedAt = now;
            lead.ClosedAt = status is CrmLeadStatus.Won or CrmLeadStatus.Lost
                ? now
                : null;
            lead.Activities.Add(new CrmActivity
            {
                Type = CrmActivityType.Status,
                Content =
                    $"Status changed from {oldStatus.ToApiValue()} " +
                    $"to {status.ToApiValue()}.",
                CreatedByUserId = userId,
                CreatedAt = now
            });

            await _context.SaveChangesAsync(cancellationToken);
        }

        var updated = await LoadLeadDetailsAsync(id, userId, cancellationToken);
        return Ok(ToLeadDetail(updated!));
    }

    [Authorize(Roles = CrmManagerRoles)]
    [HttpPut("leads/{id:int}/assignment")]
    public async Task<ActionResult<CrmLeadDetailDto>> AssignLead(
        int id,
        [FromBody] AssignCrmLeadDto dto,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null)
            return Unauthorized();

        var assignedAgentId = NormalizeOptional(dto.AssignedAgentId);
        AppUser? assignedAgent = null;
        if (assignedAgentId is not null)
        {
            assignedAgent = await FindAgentAsync(
                assignedAgentId,
                cancellationToken);
            if (assignedAgent is null)
            {
                return BadRequest(new
                {
                    message = "Assigned agent is not valid."
                });
            }
        }

        var lead = await _context.CrmLeads
            .Include(item => item.AssignedAgent)
            .Include(item => item.Tasks)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (lead is null)
            return NotFound(new { message = "Lead not found." });

        if (lead.AssignedAgentId != assignedAgentId)
        {
            var now = DateTime.UtcNow;
            var oldAgentName = lead.AssignedAgent?.FullName ??
                lead.AssignedAgent?.UserName ??
                "unassigned";
            var newAgentName = assignedAgent?.FullName ??
                assignedAgent?.UserName ??
                "unassigned";

            lead.AssignedAgentId = assignedAgentId;
            lead.AssignedAgent = assignedAgent;
            lead.UpdatedAt = now;
            foreach (var task in lead.Tasks.Where(task => task.CompletedAt == null))
            {
                task.AssignedAgentId = assignedAgentId;
                task.UpdatedAt = now;
            }

            lead.Activities.Add(new CrmActivity
            {
                Type = CrmActivityType.Assignment,
                Content = $"Assignment changed from {oldAgentName} to {newAgentName}.",
                CreatedByUserId = userId,
                CreatedAt = now
            });

            await _context.SaveChangesAsync(cancellationToken);
        }

        var updated = await LoadLeadDetailsAsync(id, userId, cancellationToken);
        return Ok(ToLeadDetail(updated!));
    }

    [Authorize(Roles = CrmWriteRoles)]
    [HttpPost("leads/{leadId:int}/activities")]
    [HttpPost("leads/{leadId:int}/notes")]
    public async Task<ActionResult<CrmActivityResponseDto>> AddActivity(
        int leadId,
        [FromBody] CreateCrmActivityDto dto,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null)
            return Unauthorized();

        if (!CrmEnumText.TryParseActivityType(dto.Type, out var activityType))
        {
            return BadRequest(new
            {
                message =
                    "Invalid activity type. Use note, inquiry, or task for " +
                    "manual activities."
            });
        }
        if (activityType is CrmActivityType.Status or
            CrmActivityType.Assignment or
            CrmActivityType.System)
        {
            return BadRequest(new
            {
                message =
                    "Status, assignment, and system activities are created " +
                    "automatically."
            });
        }
        if (string.IsNullOrWhiteSpace(dto.Body))
            return BadRequest(new { message = "Activity content is required." });

        var lead = await AccessibleLeads(userId)
            .FirstOrDefaultAsync(item => item.Id == leadId, cancellationToken);
        if (lead is null)
            return NotFound(new { message = "Lead not found." });

        var now = DateTime.UtcNow;
        var activity = new CrmActivity
        {
            LeadId = leadId,
            Type = activityType,
            Content = dto.Body.Trim(),
            CreatedByUserId = userId,
            CreatedAt = now
        };
        lead.UpdatedAt = now;
        _context.CrmActivities.Add(activity);
        await _context.SaveChangesAsync(cancellationToken);

        var creatorName = await UserDisplayNameAsync(userId, cancellationToken);
        return Created(
            $"/api/Crm/leads/{leadId}",
            ToActivityDto(activity, creatorName));
    }

    [Authorize(Roles = CrmWriteRoles)]
    [HttpPost("leads/{leadId:int}/tasks")]
    public async Task<ActionResult<CrmTaskResponseDto>> CreateTask(
        int leadId,
        [FromBody] CreateCrmTaskDto dto,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null)
            return Unauthorized();

        if (!CrmEnumText.TryParseTaskType(dto.Type, out var taskType))
        {
            return BadRequest(new
            {
                message = "Invalid task type. Use follow-up, call, viewing, or email."
            });
        }
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "Task title is required." });
        if (dto.DueAt == default)
            return BadRequest(new { message = "Task dueAt is required." });

        var lead = await AccessibleLeads(userId)
            .Include(item => item.AssignedAgent)
            .FirstOrDefaultAsync(item => item.Id == leadId, cancellationToken);
        if (lead is null)
            return NotFound(new { message = "Lead not found." });

        var requestedAgentId = NormalizeOptional(dto.AssignedAgentId);
        var taskAgentId = requestedAgentId ?? lead.AssignedAgentId;
        AppUser? taskAgent = lead.AssignedAgent;

        if (taskAgentId != lead.AssignedAgentId)
        {
            return BadRequest(new
            {
                message =
                    "A task must be assigned to the lead's assigned agent. " +
                    "Assign the lead first."
            });
        }
        if (taskAgentId is not null && taskAgent is null)
        {
            taskAgent = await FindAgentAsync(taskAgentId, cancellationToken);
            if (taskAgent is null)
                return BadRequest(new { message = "Assigned agent is not valid." });
        }

        var now = DateTime.UtcNow;
        var task = new CrmTask
        {
            LeadId = leadId,
            Type = taskType,
            Title = dto.Title.Trim(),
            Details = NormalizeOptional(dto.Description),
            DueAt = ToUtc(dto.DueAt),
            AssignedAgentId = taskAgentId,
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };
        lead.UpdatedAt = now;
        lead.Activities.Add(new CrmActivity
        {
            Type = CrmActivityType.Task,
            Content = $"Task created: {task.Title}.",
            CreatedByUserId = userId,
            CreatedAt = now
        });
        _context.CrmTasks.Add(task);
        await _context.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/Crm/leads/{leadId}",
            ToTaskDto(
                task,
                taskAgent?.FullName ?? taskAgent?.UserName));
    }

    [Authorize(Roles = CrmWriteRoles)]
    [HttpPatch("tasks/{taskId:int}")]
    [HttpPatch("leads/{leadId:int}/tasks/{taskId:int}")]
    public async Task<ActionResult<CrmTaskResponseDto>> PatchTask(
        int taskId,
        [FromRoute] int? leadId,
        [FromBody] PatchCrmTaskDto dto,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null)
            return Unauthorized();

        CrmTaskType? taskType = null;
        if (dto.Type is not null)
        {
            if (!CrmEnumText.TryParseTaskType(dto.Type, out var parsedType))
            {
                return BadRequest(new
                {
                    message =
                        "Invalid task type. Use follow-up, call, viewing, or email."
                });
            }

            taskType = parsedType;
        }
        if (dto.Title is not null && string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "Task title cannot be empty." });
        if (dto.DueAt.HasValue && dto.DueAt.Value == default)
            return BadRequest(new { message = "Task dueAt is not valid." });
        if (dto.Status is not null &&
            dto.Status is not ("open" or "completed"))
        {
            return BadRequest(new
            {
                message = "Task status must be open or completed."
            });
        }

        var hasFullAccess = HasFullCrmAccess();
        var task = await _context.CrmTasks
            .Include(item => item.Lead)
            .Include(item => item.AssignedAgent)
            .Where(item => hasFullAccess ||
                item.Lead.AssignedAgentId == userId)
            .FirstOrDefaultAsync(item => item.Id == taskId, cancellationToken);
        if (task is null)
            return NotFound(new { message = "Task not found." });
        if (leadId.HasValue && task.LeadId != leadId.Value)
            return NotFound(new { message = "Task not found for this lead." });

        var now = DateTime.UtcNow;
        var statusChanged = false;
        if (taskType.HasValue)
            task.Type = taskType.Value;
        if (dto.Title is not null)
            task.Title = dto.Title.Trim();
        if (dto.Description is not null)
            task.Details = NormalizeOptional(dto.Description);
        if (dto.DueAt.HasValue)
            task.DueAt = ToUtc(dto.DueAt.Value);
        if (dto.Status is not null)
        {
            var shouldBeCompleted = dto.Status == "completed";
            statusChanged = shouldBeCompleted != task.CompletedAt.HasValue;
            task.CompletedAt = shouldBeCompleted ? now : null;
        }

        task.UpdatedAt = now;
        task.Lead.UpdatedAt = now;
        task.Lead.Activities.Add(new CrmActivity
        {
            Type = CrmActivityType.Task,
            Content = statusChanged
                ? $"Task marked {dto.Status}: {task.Title}."
                : $"Task updated: {task.Title}.",
            CreatedByUserId = userId,
            CreatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ToTaskDto(
            task,
            task.AssignedAgent?.FullName ?? task.AssignedAgent?.UserName));
    }

    [AllowAnonymous]
    [EnableRateLimiting("CrmInquiries")]
    [HttpPost("inquiries")]
    public async Task<ActionResult<PublicCrmInquiryResponseDto>> CreateInquiry(
        [FromBody] PublicCrmInquiryDto dto,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(dto.Website))
        {
            return Accepted(new PublicCrmInquiryResponseDto
            {
                Received = true
            });
        }

        if (!dto.ConsentToContact)
        {
            return BadRequest(new
            {
                message = "Consent to contact is required."
            });
        }
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Name is required." });
        if (string.IsNullOrWhiteSpace(dto.Email) &&
            string.IsNullOrWhiteSpace(dto.Phone))
        {
            return BadRequest(new
            {
                message = "Provide at least an email address or phone number."
            });
        }
        if (dto.ApartmentId.HasValue &&
            !await ApartmentExistsAsync(dto.ApartmentId.Value, cancellationToken))
        {
            return BadRequest(new { message = "Apartment is not valid." });
        }

        var now = DateTime.UtcNow;
        DateTime? requestedViewingAt = null;
        if (dto.RequestedViewingAt.HasValue)
        {
            requestedViewingAt = ToUtc(dto.RequestedViewingAt.Value);
            if (requestedViewingAt <= now)
            {
                return BadRequest(new
                {
                    message = "requestedViewingAt must be in the future."
                });
            }
        }

        string? customerUserId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var claimUserId = CurrentUserId();
            if (claimUserId is not null &&
                await UserExistsAsync(claimUserId, cancellationToken))
            {
                customerUserId = claimUserId;
            }
        }

        var lead = new CrmLead
        {
            Name = dto.Name.Trim(),
            Email = NormalizeEmail(dto.Email),
            Phone = NormalizeOptional(dto.Phone),
            Status = CrmLeadStatus.New,
            Source = CrmLeadSource.Website,
            Message = NormalizeOptional(dto.Message),
            RequestedViewingAt = requestedViewingAt,
            ApartmentId = dto.ApartmentId,
            CustomerUserId = customerUserId,
            ConsentGiven = true,
            ConsentGivenAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        lead.Activities.Add(new CrmActivity
        {
            Type = CrmActivityType.Inquiry,
            Content = NormalizeOptional(dto.Message) ?? "Website inquiry received.",
            CreatedByUserId = customerUserId,
            CreatedAt = now
        });

        if (requestedViewingAt.HasValue)
        {
            lead.Tasks.Add(new CrmTask
            {
                Type = CrmTaskType.Viewing,
                Title = "Requested property viewing",
                Details = NormalizeOptional(dto.Message),
                DueAt = requestedViewingAt.Value,
                CreatedByUserId = customerUserId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        _context.CrmLeads.Add(lead);
        await _context.SaveChangesAsync(cancellationToken);

        return Accepted(new PublicCrmInquiryResponseDto
        {
            Received = true
        });
    }

    private IQueryable<CrmLead> AccessibleLeads(string userId)
    {
        var query = _context.CrmLeads.AsQueryable();
        if (HasFullCrmAccess())
            return query;
        if (User.IsInRole("Agent"))
            return query.Where(lead => lead.AssignedAgentId == userId);

        return query.Where(lead =>
            lead.CreatedByUserId == userId ||
            (lead.Apartment != null &&
             lead.Apartment.UploadedByUserId == userId));
    }

    private bool HasFullCrmAccess() =>
        User.IsInRole("Admin") || User.IsInRole("Manager");

    private async Task<CrmLead?> LoadLeadDetailsAsync(
        int id,
        string userId,
        CancellationToken cancellationToken) =>
        await AccessibleLeads(userId)
            .AsNoTracking()
            .AsSplitQuery()
            .Include(lead => lead.Apartment)
            .Include(lead => lead.AssignedAgent)
            .Include(lead => lead.Activities)
                .ThenInclude(activity => activity.CreatedByUser)
            .Include(lead => lead.Tasks)
                .ThenInclude(task => task.AssignedAgent)
            .FirstOrDefaultAsync(lead => lead.Id == id, cancellationToken);

    private async Task<AppUser?> FindAgentAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var agent = await _context.Users
            .FirstOrDefaultAsync(
                user => user.Id == id && user.IsAgent,
                cancellationToken);
        if (agent is null || !await _userManager.IsInRoleAsync(agent, "Agent"))
            return null;

        return agent;
    }

    private Task<bool> ApartmentExistsAsync(
        int apartmentId,
        CancellationToken cancellationToken) =>
        _context.Apartments
            .AsNoTracking()
            .AnyAsync(
                apartment => apartment.Id == apartmentId,
                cancellationToken);

    private Task<bool> UserExistsAsync(
        string userId,
        CancellationToken cancellationToken) =>
        _context.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId, cancellationToken);

    private async Task<string?> UserDisplayNameAsync(
        string userId,
        CancellationToken cancellationToken) =>
        await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.FullName ?? user.UserName)
            .FirstOrDefaultAsync(cancellationToken);

    private string? CurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    private static string? ValidateLeadDetails(
        string fullName,
        string? email,
        string? phoneNumber,
        decimal? budgetMin,
        decimal? budgetMax,
        string currency,
        IReadOnlyCollection<string> preferredDistricts)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return "Full name is required.";
        if (string.IsNullOrWhiteSpace(email) &&
            string.IsNullOrWhiteSpace(phoneNumber))
        {
            return "Provide at least an email address or phone number.";
        }
        if (budgetMin.HasValue && budgetMax.HasValue &&
            budgetMin.Value > budgetMax.Value)
        {
            return "budgetMin cannot be greater than budgetMax.";
        }
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            return "Currency must be a three-letter code.";
        if (preferredDistricts.Any(district =>
                !string.IsNullOrWhiteSpace(district) &&
                district.Trim().Length > 100))
        {
            return "Preferred district names cannot exceed 100 characters.";
        }

        return null;
    }

    private static CrmLeadDetailDto ToLeadDetail(CrmLead lead)
    {
        var nextFollowUpAt = lead.Tasks
            .Where(task =>
                task.CompletedAt == null &&
                task.Type == CrmTaskType.FollowUp)
            .Select(task => (DateTime?)task.DueAt)
            .Min();
        var lastActivityAt = lead.Activities
            .Select(activity => (DateTime?)activity.CreatedAt)
            .Max();

        return new CrmLeadDetailDto
        {
            Id = lead.Id,
            FullName = lead.Name,
            Email = lead.Email,
            PhoneNumber = lead.Phone,
            Status = lead.Status.ToApiValue(),
            Source = lead.Source.ToApiValue(),
            Goal = lead.Goal,
            PreferredContactMethod = lead.PreferredContactMethod,
            PreferredDistricts = [.. lead.PreferredDistricts],
            PreferredPropertyType = lead.PreferredPropertyType,
            Bedrooms = lead.Bedrooms,
            BudgetMin = lead.BudgetMin,
            BudgetMax = lead.BudgetMax,
            Currency = lead.Currency,
            Preferences = lead.Preferences,
            Message = lead.Message,
            RequestedViewingAt = lead.RequestedViewingAt,
            NextFollowUpAt = nextFollowUpAt,
            LastActivityAt = lastActivityAt,
            ConsentGiven = lead.ConsentGiven,
            ConsentGivenAt = lead.ConsentGivenAt,
            ApartmentId = lead.ApartmentId,
            ApartmentTitle = lead.Apartment?.Title,
            UploaderUserId = lead.Apartment?.UploadedByUserId,
            CustomerUserId = lead.CustomerUserId,
            AssignedAgentId = lead.AssignedAgentId,
            AssignedAgentName = lead.AssignedAgent?.FullName ??
                lead.AssignedAgent?.UserName,
            CreatedByUserId = lead.CreatedByUserId,
            CreatedAt = lead.CreatedAt,
            UpdatedAt = lead.UpdatedAt,
            ClosedAt = lead.ClosedAt,
            Activities = lead.Activities
                .OrderByDescending(activity => activity.CreatedAt)
                .Select(activity => ToActivityDto(
                    activity,
                    activity.CreatedByUser?.FullName ??
                        activity.CreatedByUser?.UserName))
                .ToList(),
            Tasks = lead.Tasks
                .OrderBy(task => task.CompletedAt.HasValue)
                .ThenBy(task => task.DueAt)
                .Select(task => ToTaskDto(
                    task,
                    task.AssignedAgent?.FullName ??
                        task.AssignedAgent?.UserName))
                .ToList()
        };
    }

    private static CrmActivityResponseDto ToActivityDto(
        CrmActivity activity,
        string? createdByName) =>
        new()
        {
            Id = activity.Id,
            LeadId = activity.LeadId,
            Type = activity.Type.ToApiValue(),
            Body = activity.Content,
            CreatedById = activity.CreatedByUserId,
            CreatedByName = createdByName,
            CreatedAt = activity.CreatedAt
        };

    private static CrmTaskResponseDto ToTaskDto(
        CrmTask task,
        string? assignedAgentName) =>
        new()
        {
            Id = task.Id,
            LeadId = task.LeadId,
            Type = task.Type.ToApiValue(),
            Title = task.Title,
            Description = task.Details,
            DueAt = task.DueAt,
            CompletedAt = task.CompletedAt,
            Status = task.CompletedAt.HasValue ? "completed" : "open",
            AssignedAgentId = task.AssignedAgentId,
            AssignedAgentName = assignedAgentName,
            CreatedByUserId = task.CreatedByUserId,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt
        };

    private static string GenerateQuestionnaireToken()
    {
        // 16 random bytes = 128 bits of randomness.
        // Hex produces a URL-safe 32-character token.
        return Convert
            .ToHexString(RandomNumberGenerator.GetBytes(16))
            .ToLowerInvariant();
    }

    private static string NormalizeQuestionnaireToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var token = value.Trim();

        if (token.StartsWith(
            "agent-",
            StringComparison.OrdinalIgnoreCase))
        {
            token = token["agent-".Length..];
        }

        return token;
    }


    private static string? NormalizeEmail(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string[] NormalizeDistricts(IEnumerable<string> values) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
