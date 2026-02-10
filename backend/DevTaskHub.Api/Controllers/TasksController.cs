using DevTaskHub.Api.Models;
using DevTaskHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TaskPriority = DevTaskHub.Api.Models.TaskPriority;
using TaskStatus = DevTaskHub.Api.Models.TaskStatus;

namespace DevTaskHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TasksController(ITaskService taskService) : ControllerBase
{
    private readonly ITaskService _taskService = taskService;

    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<TaskItem>>> GetMyTasks(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var tasks = await _taskService.GetMyTasksAsync(userId.Value, cancellationToken);
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

        var tasks = await _taskService.GetCalendarTasksAsync(userId.Value, from, to, cancellationToken);
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

        var result = await _taskService.UpdateTaskAsync(userId.Value, taskId, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{taskId:guid}/checklist")]
    public async Task<ActionResult<TaskChecklistItem>> AddChecklistItem(Guid taskId, [FromBody] ChecklistItemRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _taskService.AddChecklistItemAsync(userId.Value, taskId, request.Title, cancellationToken);
        return result.Status switch
        {
            TaskResultStatus.Success => CreatedAtAction(nameof(GetMyTasks), new { taskId }, result.Value),
            TaskResultStatus.Forbid => Forbid(),
            TaskResultStatus.NotFound => NotFound(),
            TaskResultStatus.BadRequest => BadRequest(new { message = result.Message }),
            _ => StatusCode(500)
        };
    }

    [HttpPut("checklist/{itemId:guid}/toggle")]
    public async Task<IActionResult> ToggleChecklistItem(Guid itemId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _taskService.ToggleChecklistItemAsync(userId.Value, itemId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("checklist/{itemId:guid}")]
    public async Task<IActionResult> DeleteChecklistItem(Guid itemId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _taskService.DeleteChecklistItemAsync(userId.Value, itemId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("{taskId:guid}")]
    public async Task<IActionResult> DeleteTask(Guid taskId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _taskService.DeleteTaskAsync(userId.Value, taskId, cancellationToken);
        return ToActionResult(result);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(userIdClaim, out var guid) ? guid : null;
    }

    private static IActionResult ToActionResult(TaskResult result) =>
        result.Status switch
        {
            TaskResultStatus.Success => new NoContentResult(),
            TaskResultStatus.NotFound => new NotFoundResult(),
            TaskResultStatus.Forbid => new ForbidResult(),
            TaskResultStatus.BadRequest => new BadRequestObjectResult(new { message = result.Message }),
            _ => new StatusCodeResult(500)
        };

    public record UpdateTaskRequest(TaskStatus Status, TaskPriority Priority, Guid? AssignedToUserId, DateTime? DueDate, List<string>? Labels);
    public record ChecklistItemRequest(string Title);
}
