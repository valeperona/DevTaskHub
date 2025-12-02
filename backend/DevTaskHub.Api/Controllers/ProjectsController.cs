using DevTaskHub.Api.Data;
using DevTaskHub.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TaskPriority = DevTaskHub.Api.Models.TaskPriority;
using TaskStatus = DevTaskHub.Api.Models.TaskStatus;
using ProjectRole = DevTaskHub.Api.Models.ProjectRole;
using InvitationStatus = DevTaskHub.Api.Models.InvitationStatus;

namespace DevTaskHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProjectsController(DevTaskHubContext context) : ControllerBase
{
    private readonly DevTaskHubContext _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProjectDto>>> GetProjects(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var projects = await _context.Projects
            .AsNoTracking()
            .Where(p => p.OwnerId == userId.Value || p.Members.Any(m => m.UserId == userId.Value))
            .Include(p => p.Tasks)!.ThenInclude(t => t.AssignedTo)
            .Include(p => p.Members).ThenInclude(m => m.User)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(projects.Select(MapProjectDto));
    }


    [HttpPost]
    public async Task<ActionResult<Project>> CreateProject([FromBody] CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            ModelState.AddModelError(nameof(request.Name), "Name is required");
            return ValidationProblem(ModelState);
        }

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CreatedAt = DateTime.UtcNow,
            OwnerId = userId.Value
        };

        _context.Projects.Add(project);
        _context.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = userId.Value,
            Role = ProjectRole.Owner
        });
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetProjectById), new { id = project.Id }, project);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> GetProjectById(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var project = await _context.Projects
            .AsNoTracking()
            .Include(p => p.Tasks)!.ThenInclude(t => t.AssignedTo)
            .Include(p => p.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(p => p.Id == id && (p.OwnerId == userId.Value || p.Members.Any(m => m.UserId == userId.Value)), cancellationToken);

        return project is null ? NotFound() : Ok(MapProjectDto(project));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteProject(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == userId.Value, cancellationToken);
        if (project is null)
        {
            return NotFound();
        }

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpGet("{projectId:guid}/tasks")]
    public async Task<ActionResult<IEnumerable<TaskItem>>> GetTasksForProject(Guid projectId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var exists = await _context.Projects.AnyAsync(p => p.Id == projectId && (p.OwnerId == userId.Value || p.Members.Any(m => m.UserId == userId.Value)), cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        var tasks = await _context.TaskItems
            .Where(t => t.ProjectId == projectId)
            .Include(t => t.AssignedTo)
            .OrderByDescending(t => t.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return Ok(tasks);
    }

    [HttpPost("{projectId:guid}/tasks")]
    public async Task<ActionResult<TaskItem>> AddTaskToProject(Guid projectId, [FromBody] CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var project = await _context.Projects.Include(p => p.Members).FirstOrDefaultAsync(p => p.Id == projectId && (p.OwnerId == userId.Value || p.Members.Any(m => m.UserId == userId.Value)), cancellationToken);
        if (project is null)
        {
            return NotFound();
        }

        var role = GetUserRole(project, userId.Value);
        if (role is ProjectRole.Viewer or null)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            ModelState.AddModelError(nameof(request.Title), "Title is required");
            return ValidationProblem(ModelState);
        }

        var taskItem = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Priority = request.Priority,
            Status = TaskStatus.ToDo,
            AssignedToUserId = await ResolveAssignee(project, request.AssignedToUserId, cancellationToken),
            DueDate = NormalizeDate(request.DueDate),
            Labels = request.Labels ?? new List<string>(),
            CreatedAt = DateTime.UtcNow
        };

        if (taskItem.Priority == TaskPriority.High && taskItem.AssignedToUserId is null)
        {
            return BadRequest(new { message = "Las tareas de alta prioridad requieren un responsable" });
        }

        _context.TaskItems.Add(taskItem);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetTasksForProject), new { projectId }, taskItem);
    }

    [HttpPost("{projectId:guid}/transfer-owner")]
    public async Task<IActionResult> TransferOwnership(Guid projectId, [FromBody] TransferOwnerRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var project = await _context.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        if (project.OwnerId != userId.Value)
        {
            return Forbid();
        }

        var targetMember = project.Members.FirstOrDefault(m => m.UserId == request.NewOwnerUserId);
        if (targetMember is null)
        {
            return NotFound(new { message = "El nuevo owner debe ser miembro del proyecto" });
        }

        // Degradar owner actual a Collaborator
        var existingOldOwnerMember = project.Members.FirstOrDefault(m => m.UserId == project.OwnerId);
        if (existingOldOwnerMember is null)
        {
            project.Members.Add(new ProjectMember { ProjectId = projectId, UserId = project.OwnerId, Role = ProjectRole.Collaborator });
        }
        else
        {
            existingOldOwnerMember.Role = ProjectRole.Collaborator;
        }

        // Promover nuevo owner (remover de miembros para evitar duplicado)
        project.Members.Remove(targetMember);
        project.OwnerId = request.NewOwnerUserId;

        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/members")]
    public async Task<ActionResult<IEnumerable<ProjectMember>>> GetMembers(Guid projectId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var project = await _context.Projects
            .Include(p => p.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(p => p.Id == projectId && (p.OwnerId == userId.Value || p.Members.Any(m => m.UserId == userId.Value)), cancellationToken);

        return project is null ? NotFound() : Ok(project.Members);
    }

    [HttpPost("{projectId:guid}/members")]
    public async Task<ActionResult<ProjectMember>> AddMember(Guid projectId, [FromBody] AddMemberRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var project = await _context.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        if (project.OwnerId != userId.Value)
        {
            return Forbid();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email.ToLower().Trim(), cancellationToken);
        if (user is null)
        {
            return NotFound(new { message = "Usuario no encontrado" });
        }

        var alreadyMember = project.Members.Any(m => m.UserId == user.Id);
        if (alreadyMember)
        {
            return Conflict(new { message = "El usuario ya es miembro" });
        }

        if (request.Role == ProjectRole.Owner)
        {
            return BadRequest(new { message = "No se puede asignar Owner directo. Usa transferencia de ownership." });
        }

        var member = new ProjectMember
        {
            ProjectId = projectId,
            UserId = user.Id,
            Role = request.Role
        };

        _context.ProjectMembers.Add(member);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetMembers), new { projectId }, member);
    }

    [HttpPost("{projectId:guid}/leave")]
    public async Task<IActionResult> LeaveProject(Guid projectId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var project = await _context.Projects
            .Include(p => p.Members)
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        if (project.OwnerId == userId.Value)
        {
            return BadRequest(new { message = "El owner no puede desvincularse; transfiere ownership primero." });
        }

        var membership = project.Members.FirstOrDefault(m => m.UserId == userId.Value);
        if (membership is null)
        {
            return NotFound(new { message = "No estás unido a este proyecto" });
        }

        // Reasignar tareas pendientes al owner
        foreach (var task in project.Tasks.Where(t => t.AssignedToUserId == userId.Value))
        {
            task.AssignedToUserId = project.OwnerId;
        }

        _context.ProjectMembers.Remove(membership);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("{projectId:guid}/invitations")]
    public async Task<ActionResult<InvitationDto>> InviteMember(Guid projectId, [FromBody] InvitationRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var project = await _context.Projects
            .Include(p => p.Members)
            .Include(p => p.Invitations)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        if (project.OwnerId != userId.Value)
        {
            return Forbid();
        }

        if (request.Role == ProjectRole.Owner)
        {
            return BadRequest(new { message = "No se puede invitar como Owner; transfiere ownership luego." });
        }

        var email = request.Email.ToLower().Trim();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null)
        {
            return NotFound(new { message = "Usuario no encontrado" });
        }

        if (project.Members.Any(m => m.UserId == user.Id))
        {
            return Conflict(new { message = "El usuario ya es miembro" });
        }

        if (project.Invitations.Any(i => i.UserId == user.Id && i.Status == InvitationStatus.Pending))
        {
            return Conflict(new { message = "Ya existe una invitación pendiente" });
        }

        var invitation = new ProjectInvitation
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = user.Id,
            Role = request.Role,
            Status = InvitationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.ProjectInvitations.Add(invitation);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetInvitations), new { projectId }, MapInvitationDto(invitation, project.Name, user.Email));
    }

    [HttpGet("{projectId:guid}/invitations")]
    public async Task<ActionResult<IEnumerable<InvitationDto>>> GetInvitations(Guid projectId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var invitations = await _context.ProjectInvitations
            .AsNoTracking()
            .Include(i => i.Project)
            .Include(i => i.User)
            .Where(i => i.ProjectId == projectId && i.Project != null && i.Project.OwnerId == userId.Value)
            .ToListAsync(cancellationToken);

        return Ok(invitations.Select(i => MapInvitationDto(i, i.Project?.Name ?? string.Empty, i.User?.Email ?? string.Empty)));
    }

    [HttpGet("invitations/mine")]
    public async Task<ActionResult<IEnumerable<InvitationDto>>> GetMyInvitations(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var invitations = await _context.ProjectInvitations
            .AsNoTracking()
            .Include(i => i.Project)
            .Include(i => i.User)
            .Where(i => i.UserId == userId.Value && i.Status == InvitationStatus.Pending)
            .ToListAsync(cancellationToken);

        return Ok(invitations.Select(i => MapInvitationDto(i, i.Project?.Name ?? string.Empty, i.User?.Email ?? string.Empty)));
    }

    [HttpPost("invitations/{invitationId:guid}/respond")]
    public async Task<IActionResult> RespondInvitation(Guid invitationId, [FromBody] InvitationRespondRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var invitation = await _context.ProjectInvitations
            .Include(i => i.Project)
            .FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken);

        if (invitation is null)
        {
            return NotFound();
        }

        if (invitation.UserId != userId.Value)
        {
            return Forbid();
        }

        if (invitation.Status != InvitationStatus.Pending)
        {
            return BadRequest(new { message = "La invitación ya fue respondida" });
        }

        if (request.Status == InvitationStatus.Accepted)
        {
            var exists = await _context.ProjectMembers.AnyAsync(m => m.ProjectId == invitation.ProjectId && m.UserId == invitation.UserId, cancellationToken);
            if (!exists)
            {
                _context.ProjectMembers.Add(new ProjectMember
                {
                    ProjectId = invitation.ProjectId,
                    UserId = invitation.UserId,
                    Role = invitation.Role
                });
            }
        }

        invitation.Status = request.Status;
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    public record CreateProjectRequest(string Name, string? Description);

    public record CreateTaskRequest(string Title, string? Description, TaskPriority Priority = TaskPriority.Medium, Guid? AssignedToUserId = null, DateTime? DueDate = null, List<string>? Labels = null);
    public record AddMemberRequest(string Email, ProjectRole Role = ProjectRole.Collaborator);
    public record TransferOwnerRequest(Guid NewOwnerUserId);
    public record InvitationRequest(string Email, ProjectRole Role = ProjectRole.Collaborator);
    public record InvitationRespondRequest(InvitationStatus Status);
    public record ProjectDto(Guid Id, string Name, string? Description, DateTime CreatedAt, Guid OwnerId, IEnumerable<ProjectMemberDto> Members, IEnumerable<TaskDto> Tasks);
    public record ProjectMemberDto(Guid UserId, string Email, ProjectRole Role);
    public record TaskDto(Guid Id, string Title, string? Description, TaskStatus Status, TaskPriority Priority, Guid ProjectId, Guid? AssignedToUserId, DateTime CreatedAt, DateTime? DueDate, IEnumerable<string> Labels);
    public record InvitationDto(Guid Id, Guid ProjectId, string ProjectName, Guid UserId, string Email, ProjectRole Role, InvitationStatus Status, DateTime CreatedAt);

    private Guid? GetUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(id, out var guid) ? guid : null;
    }

    private static bool UserIsMember(Project project, Guid userId) =>
        project.OwnerId == userId || project.Members.Any(m => m.UserId == userId);

    private static ProjectRole? GetUserRole(Project project, Guid userId)
    {
        if (project.OwnerId == userId) return ProjectRole.Owner;
        return project.Members.FirstOrDefault(m => m.UserId == userId)?.Role;
    }

    private static ProjectDto MapProjectDto(Project project)
    {
        var members = project.Members.Select(m => new ProjectMemberDto(m.UserId, m.User?.Email ?? string.Empty, m.Role)).ToList();
        var tasks = project.Tasks.Select(t => new TaskDto(
            t.Id,
            t.Title,
            t.Description,
            t.Status,
            t.Priority,
            t.ProjectId,
            t.AssignedToUserId,
            t.CreatedAt,
            t.DueDate,
            t.Labels ?? new List<string>())).ToList();
        return new ProjectDto(project.Id, project.Name, project.Description, project.CreatedAt, project.OwnerId, members, tasks);
    }
    private static async Task<Guid?> ResolveAssignee(Project project, Guid? requestedUserId, CancellationToken cancellationToken)
    {
        if (requestedUserId is null)
        {
            return null;
        }

        var memberRole = project.OwnerId == requestedUserId.Value
            ? ProjectRole.Owner
            : project.Members.FirstOrDefault(m => m.UserId == requestedUserId.Value)?.Role;

        if (memberRole is null || memberRole == ProjectRole.Viewer)
        {
            return null;
        }

        return requestedUserId;
    }

    private static DateTime? NormalizeDate(DateTime? date)
    {
        if (date is null) return null;
        if (date.Value.Kind == DateTimeKind.Utc) return date;
        return DateTime.SpecifyKind(date.Value, DateTimeKind.Utc);
    }

    private static InvitationDto MapInvitationDto(ProjectInvitation invitation, string projectName, string email) =>
        new(invitation.Id, invitation.ProjectId, projectName, invitation.UserId, email, invitation.Role, invitation.Status, invitation.CreatedAt);
}
