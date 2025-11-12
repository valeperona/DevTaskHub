using DevTaskHub.Api.Data;
using DevTaskHub.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskPriority = DevTaskHub.Api.Models.TaskPriority;
using TaskStatus = DevTaskHub.Api.Models.TaskStatus;

namespace DevTaskHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController(DevTaskHubContext context) : ControllerBase
{
    private readonly DevTaskHubContext _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Project>>> GetProjects(CancellationToken cancellationToken)
    {
        var projects = await _context.Projects
            .AsNoTracking()
            .Include(p => p.Tasks)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(projects);
    }

    [HttpPost]
    public async Task<ActionResult<Project>> CreateProject([FromBody] CreateProjectRequest request, CancellationToken cancellationToken)
    {
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
            CreatedAt = DateTime.UtcNow
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetProjectById), new { id = project.Id }, project);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Project>> GetProjectById(Guid id, CancellationToken cancellationToken)
    {
        var project = await _context.Projects
            .AsNoTracking()
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return project is null ? NotFound() : Ok(project);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteProject(Guid id, CancellationToken cancellationToken)
    {
        var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
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
        var exists = await _context.Projects.AnyAsync(p => p.Id == projectId, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        var tasks = await _context.TaskItems
            .Where(t => t.ProjectId == projectId)
            .OrderByDescending(t => t.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return Ok(tasks);
    }

    [HttpPost("{projectId:guid}/tasks")]
    public async Task<ActionResult<TaskItem>> AddTaskToProject(Guid projectId, [FromBody] CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
        if (project is null)
        {
            return NotFound();
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
            CreatedAt = DateTime.UtcNow
        };

        _context.TaskItems.Add(taskItem);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetTasksForProject), new { projectId }, taskItem);
    }

    public record CreateProjectRequest(string Name, string? Description);

    public record CreateTaskRequest(string Title, string? Description, TaskPriority Priority = TaskPriority.Medium);
}
