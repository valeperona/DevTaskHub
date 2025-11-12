using DevTaskHub.Api.Data;
using DevTaskHub.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskPriority = DevTaskHub.Api.Models.TaskPriority;
using TaskStatus = DevTaskHub.Api.Models.TaskStatus;

namespace DevTaskHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController(DevTaskHubContext context) : ControllerBase
{
    private readonly DevTaskHubContext _context = context;

    [HttpPut("{taskId:guid}")]
    public async Task<IActionResult> UpdateTask(Guid taskId, [FromBody] UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task is null)
        {
            return NotFound();
        }

        task.Status = request.Status;
        task.Priority = request.Priority;

        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{taskId:guid}")]
    public async Task<IActionResult> DeleteTask(Guid taskId, CancellationToken cancellationToken)
    {
        var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task is null)
        {
            return NotFound();
        }

        _context.TaskItems.Remove(task);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    public record UpdateTaskRequest(TaskStatus Status, TaskPriority Priority);
}
