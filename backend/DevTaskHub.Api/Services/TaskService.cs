using DevTaskHub.Api.Controllers;
using DevTaskHub.Api.Data;
using DevTaskHub.Api.Models;
using Microsoft.EntityFrameworkCore;
using TaskStatusEnum = DevTaskHub.Api.Models.TaskStatus;

namespace DevTaskHub.Api.Services;

public class TaskService(DevTaskHubContext context) : ITaskService
{
    private readonly DevTaskHubContext _context = context;

    public async Task<IEnumerable<TaskItem>> GetMyTasksAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _context.TaskItems
            .AsNoTracking()
            .Include(t => t.Project)!.ThenInclude(p => p.Members)
            .Include(t => t.AssignedTo)
            .Where(t => t.AssignedToUserId == userId)
            .OrderBy(t => t.Status)
            .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<TaskItem>> GetCalendarTasksAsync(Guid userId, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var query = _context.TaskItems
            .AsNoTracking()
            .Include(t => t.Project)!.ThenInclude(p => p.Members)
            .Include(t => t.AssignedTo)
            .Where(t => t.DueDate != null && t.Project != null && (t.Project.OwnerId == userId || t.Project.Members.Any(m => m.UserId == userId)));

        if (from.HasValue)
        {
            query = query.Where(t => t.DueDate >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(t => t.DueDate <= to.Value);
        }

        query = query.Where(t => t.AssignedToUserId == userId);
        return await query.OrderBy(t => t.DueDate).ToListAsync(cancellationToken);
    }

    public async Task<TaskResult> UpdateTaskAsync(Guid userId, Guid taskId, TasksController.UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await _context.TaskItems
            .Include(t => t.Project)!.ThenInclude(p => p.Members)
            .Include(t => t.Checklist)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task is null)
        {
            return TaskResult.NotFound();
        }
        if (task.Project is null || !HasEditRights(task.Project, userId))
        {
            return TaskResult.Forbid();
        }

        if (!IsTransitionAllowed(task.Status, request.Status))
        {
            return TaskResult.BadRequest("Transición de estado no permitida");
        }

        if (request.Priority == DevTaskHub.Api.Models.TaskPriority.High && request.AssignedToUserId is null)
        {
            return TaskResult.BadRequest("Las tareas de alta prioridad requieren un responsable");
        }

        if (request.Status == DevTaskHub.Api.Models.TaskStatus.Done && task.Checklist.Any(c => !c.IsDone))
        {
            return TaskResult.BadRequest("Completa todos los items del checklist antes de cerrar la tarea");
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
                return TaskResult.BadRequest("No se puede asignar la tarea a un Viewer o a un usuario fuera del proyecto");
            }
            task.AssignedToUserId = request.AssignedToUserId;
        }
        if (request.Status == DevTaskHub.Api.Models.TaskStatus.Done && task.DueDate.HasValue)
        {
            task.CompletedLate = task.DueDate.Value < DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return TaskResult.Success();
    }

    public async Task<TaskResult<TaskChecklistItem>> AddChecklistItemAsync(Guid userId, Guid taskId, string title, CancellationToken cancellationToken)
    {
        var task = await _context.TaskItems
            .Include(t => t.Project)!.ThenInclude(p => p.Members)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task is null)
        {
            return TaskResult<TaskChecklistItem>.NotFound();
        }
        if (task.Project is null || !HasEditRights(task.Project, userId))
        {
            return TaskResult<TaskChecklistItem>.Forbid();
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return TaskResult<TaskChecklistItem>.BadRequest("El título del checklist es obligatorio");
        }

        var item = new TaskChecklistItem
        {
            Id = Guid.NewGuid(),
            TaskItemId = taskId,
            Title = title.Trim(),
            IsDone = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.TaskChecklistItems.Add(item);
        await _context.SaveChangesAsync(cancellationToken);

        return TaskResult<TaskChecklistItem>.Success(item);
    }

    public async Task<TaskResult> ToggleChecklistItemAsync(Guid userId, Guid itemId, CancellationToken cancellationToken)
    {
        var item = await _context.TaskChecklistItems
            .Include(c => c.TaskItem)!.ThenInclude(t => t.Project)!.ThenInclude(p => p.Members)
            .FirstOrDefaultAsync(c => c.Id == itemId, cancellationToken);

        if (item is null || item.TaskItem is null || item.TaskItem.Project is null)
        {
            return TaskResult.NotFound();
        }
        if (!HasEditRights(item.TaskItem.Project, userId))
        {
            return TaskResult.Forbid();
        }

        item.IsDone = !item.IsDone;
        await _context.SaveChangesAsync(cancellationToken);
        return TaskResult.Success();
    }

    public async Task<TaskResult> DeleteChecklistItemAsync(Guid userId, Guid itemId, CancellationToken cancellationToken)
    {
        var item = await _context.TaskChecklistItems
            .Include(c => c.TaskItem)!.ThenInclude(t => t.Project)!.ThenInclude(p => p.Members)
            .FirstOrDefaultAsync(c => c.Id == itemId, cancellationToken);

        if (item is null || item.TaskItem is null || item.TaskItem.Project is null)
        {
            return TaskResult.NotFound();
        }
        if (!HasEditRights(item.TaskItem.Project, userId))
        {
            return TaskResult.Forbid();
        }

        _context.TaskChecklistItems.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);
        return TaskResult.Success();
    }

    public async Task<TaskResult> DeleteTaskAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
    {
        var task = await _context.TaskItems
            .Include(t => t.Project)!.ThenInclude(p => p.Members)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task is null || task.Project is null)
        {
            return TaskResult.NotFound();
        }
        if (!HasEditRights(task.Project, userId))
        {
            return TaskResult.Forbid();
        }

        _context.TaskItems.Remove(task);
        await _context.SaveChangesAsync(cancellationToken);
        return TaskResult.Success();
    }

    private static DateTime? NormalizeDate(DateTime? date) =>
        date.HasValue ? DateTime.SpecifyKind(date.Value, DateTimeKind.Utc).Date : null;

    private static bool IsTransitionAllowed(TaskStatusEnum current, TaskStatusEnum next) =>
        current switch
        {
            TaskStatusEnum.ToDo => next is TaskStatusEnum.InProgress or TaskStatusEnum.InReview,
            TaskStatusEnum.InProgress => next is TaskStatusEnum.InReview or TaskStatusEnum.Done,
            TaskStatusEnum.InReview => next is TaskStatusEnum.InProgress or TaskStatusEnum.Done,
            TaskStatusEnum.Done => false,
            _ => false
        };

    private static bool UserCanBeAssigned(Project project, Guid userId) =>
        project.OwnerId == userId || project.Members.Any(m => m.UserId == userId && m.Role != ProjectRole.Viewer);

    private static bool HasEditRights(Project project, Guid userId) =>
        project.OwnerId == userId || project.Members.Any(m => m.UserId == userId && m.Role != ProjectRole.Viewer);
}
