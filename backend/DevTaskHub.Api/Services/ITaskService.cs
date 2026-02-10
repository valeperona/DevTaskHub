using DevTaskHub.Api.Controllers;
using DevTaskHub.Api.Models;

namespace DevTaskHub.Api.Services;

public interface ITaskService
{
    Task<IEnumerable<TaskItem>> GetMyTasksAsync(Guid userId, CancellationToken cancellationToken);
    Task<IEnumerable<TaskItem>> GetCalendarTasksAsync(Guid userId, DateTime? from, DateTime? to, CancellationToken cancellationToken);
    Task<TaskResult> UpdateTaskAsync(Guid userId, Guid taskId, TasksController.UpdateTaskRequest request, CancellationToken cancellationToken);
    Task<TaskResult<TaskChecklistItem>> AddChecklistItemAsync(Guid userId, Guid taskId, string title, CancellationToken cancellationToken);
    Task<TaskResult> ToggleChecklistItemAsync(Guid userId, Guid itemId, CancellationToken cancellationToken);
    Task<TaskResult> DeleteChecklistItemAsync(Guid userId, Guid itemId, CancellationToken cancellationToken);
    Task<TaskResult> DeleteTaskAsync(Guid userId, Guid taskId, CancellationToken cancellationToken);
}
