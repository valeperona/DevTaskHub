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

namespace DevTaskHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TasksController(DevTaskHubContext context) : ControllerBase
{
    private readonly DevTaskHubContext _context = context;

    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<TaskItem>>> GetMyTasks(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var tasks = await _context.TaskItems
            .AsNoTracking()
            .Include(t => t.Project)!.ThenInclude(p => p.Members)
            .Include(t => t.AssignedTo)
            .Where(t => t.AssignedToUserId == userId.Value)
            .OrderBy(t => t.Status)
            .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
            .ToListAsync(cancellationToken);

        return Ok(tasks);
    }

    [HttpGet("calendar")]
    public async Task<ActionResult<IEnumerable<TaskItem>>> GetTasksForCalendar([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var query = _context.TaskItems
            .AsNoTracking()
            .Include(t => t.Project)!.ThenInclude(p => p.Members)
            .Include(t => t.AssignedTo)
            .Where(t => t.DueDate != null && t.Project != null && (t.Project.OwnerId == userId.Value || t.Project.Members.Any(m => m.UserId == userId.Value)));

        if (from.HasValue)
        {
            query = query.Where(t => t.DueDate >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(t => t.DueDate <= to.Value);
        }

        query = query.Where(t => t.AssignedToUserId == userId.Value);
        var tasks = await query.OrderBy(t => t.DueDate).ToListAsync(cancellationToken);
        return Ok(tasks);
    }

    [HttpPut("{taskId:guid}")]
    public async Task<IActionResult> UpdateTask(Guid taskId, [FromBody] UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var task = await _context.TaskItems
            .Include(t => t.Project)!.ThenInclude(p => p.Members)
            .Include(t => t.Checklist)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task is null)
        {
            return NotFound();
        }
        if (task.Project is null || !HasEditRights(task.Project, userId.Value))
        {
            return Forbid();
        }

        // Validar transiciones simples
        if (!IsTransitionAllowed(task.Status, request.Status))
        {
            return BadRequest(new { message = "Transición de estado no permitida" });
        }

        // Reglas: prioridad alta requiere asignado y fecha
        if (request.Priority == DevTaskHub.Api.Models.TaskPriority.High && request.AssignedToUserId is null)
        {
            return BadRequest(new { message = "Las tareas de alta prioridad requieren un responsable" });
        }

        // Reglas: no se puede cerrar si checklist pendiente
        if (request.Status == DevTaskHub.Api.Models.TaskStatus.Done && task.Checklist.Any(c => !c.IsDone))
        {
            return BadRequest(new { message = "Completa todos los items del checklist antes de cerrar la tarea" });
        }

        task.Status = request.Status;
        task.Priority = request.Priority;
        task.DueDate = NormalizeDate(request.DueDate);
        if (request.Labels is not null)
        {
            task.Labels = request.Labels;
        }
        if (request.AssignedToUserId.HasValue)
        {
            if (!UserCanBeAssigned(task.Project!, request.AssignedToUserId.Value))
            {
                return BadRequest(new { message = "No se puede asignar la tarea a un Viewer o a un usuario fuera del proyecto" });
            }
            task.AssignedToUserId = request.AssignedToUserId;
        }
        if (request.Status == DevTaskHub.Api.Models.TaskStatus.Done && task.DueDate.HasValue)
        {
            task.CompletedLate = task.DueDate.Value < DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{taskId:guid}/checklist")]
    public async Task<ActionResult<TaskChecklistItem>> AddChecklistItem(Guid taskId, [FromBody] ChecklistItemRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var task = await _context.TaskItems
            .Include(t => t.Project)!.ThenInclude(p => p.Members)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task is null)
        {
            return NotFound();
        }
        if (task.Project is null || !HasEditRights(task.Project, userId.Value))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { message = "El título del checklist es obligatorio" });
        }

        var item = new TaskChecklistItem
        {
            Id = Guid.NewGuid(),
            TaskItemId = taskId,
            Title = request.Title.Trim(),
            IsDone = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.TaskChecklistItems.Add(item);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetMyTasks), new { taskId }, item);
    }

    [HttpPut("checklist/{itemId:guid}/toggle")]
    public async Task<IActionResult> ToggleChecklistItem(Guid itemId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var item = await _context.TaskChecklistItems
            .Include(c => c.TaskItem)!.ThenInclude(t => t.Project)!.ThenInclude(p => p.Members)
            .FirstOrDefaultAsync(c => c.Id == itemId, cancellationToken);

        if (item is null || item.TaskItem is null || item.TaskItem.Project is null)
        {
            return NotFound();
        }
        if (!HasEditRights(item.TaskItem.Project, userId.Value))
        {
            return Forbid();
        }

        item.IsDone = !item.IsDone;
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("checklist/{itemId:guid}")]
    public async Task<IActionResult> DeleteChecklistItem(Guid itemId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var item = await _context.TaskChecklistItems
            .Include(c => c.TaskItem)!.ThenInclude(t => t.Project)!.ThenInclude(p => p.Members)
            .FirstOrDefaultAsync(c => c.Id == itemId, cancellationToken);

        if (item is null || item.TaskItem is null || item.TaskItem.Project is null)
        {
            return NotFound();
        }
        if (!HasEditRights(item.TaskItem.Project, userId.Value))
        {
            return Forbid();
        }

        _context.TaskChecklistItems.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{taskId:guid}")]
    public async Task<IActionResult> DeleteTask(Guid taskId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var task = await _context.TaskItems
            .Include(t => t.Project)!.ThenInclude(p => p.Members)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task is null)
        {
            return NotFound();
        }
        if (task.Project is null || !HasEditRights(task.Project, userId.Value))
        {
            return Forbid();
        }

        _context.TaskItems.Remove(task);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    public record UpdateTaskRequest(TaskStatus Status, TaskPriority Priority, Guid? AssignedToUserId = null, DateTime? DueDate = null, List<string>? Labels = null);
    public record ChecklistItemRequest(string Title);

    private Guid? GetUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(id, out var guid) ? guid : null;
    }

    private static bool UserIsMember(Project project, Guid userId) =>
        project.OwnerId == userId || project.Members.Any(m => m.UserId == userId);

    private static bool UserCanBeAssigned(Project project, Guid userId)
    {
        if (project.OwnerId == userId) return true;
        var member = project.Members.FirstOrDefault(m => m.UserId == userId);
        return member is not null && member.Role != ProjectRole.Viewer;
    }

    private static bool IsTransitionAllowed(TaskStatus current, TaskStatus next)
    {
        if (current == next) return true;
        return current switch
        {
            DevTaskHub.Api.Models.TaskStatus.ToDo => next is DevTaskHub.Api.Models.TaskStatus.InProgress,
            DevTaskHub.Api.Models.TaskStatus.InProgress => next is DevTaskHub.Api.Models.TaskStatus.InReview or DevTaskHub.Api.Models.TaskStatus.ToDo,
            DevTaskHub.Api.Models.TaskStatus.InReview => next is DevTaskHub.Api.Models.TaskStatus.Done or DevTaskHub.Api.Models.TaskStatus.InProgress,
            DevTaskHub.Api.Models.TaskStatus.Done => false,
            _ => false
        };
    }

    private static DateTime? NormalizeDate(DateTime? date)
    {
        if (date is null) return null;
        if (date.Value.Kind == DateTimeKind.Utc) return date;
        return DateTime.SpecifyKind(date.Value, DateTimeKind.Utc);
    }

    private static bool HasEditRights(Project project, Guid userId)
    {
        if (project.OwnerId == userId) return true;
        var member = project.Members.FirstOrDefault(m => m.UserId == userId);
        return member is not null && member.Role != ProjectRole.Viewer;
    }
}
