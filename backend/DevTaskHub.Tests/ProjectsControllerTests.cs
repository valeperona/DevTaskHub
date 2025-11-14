using System;
using System.Threading;
using System.Threading.Tasks;
using DevTaskHub.Api.Controllers;
using DevTaskHub.Api.Data;
using DevTaskHub.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevTaskHub.Tests;

public class ProjectsControllerTests
{
    private static DevTaskHubContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DevTaskHubContext>()
            .UseInMemoryDatabase($"ProjectsControllerTests-{Guid.NewGuid()}")
            .Options;

        return new DevTaskHubContext(options);
    }

    [Fact]
    public async Task GetProjectById_ReturnsNotFound_WhenProjectDoesNotExist()
    {
        await using var context = CreateContext();
        var controller = new ProjectsController(context);
        var projectId = Guid.NewGuid();

        var result = await controller.GetProjectById(projectId, CancellationToken.None);

        Assert.IsType<ActionResult<Project>>(result);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetProjectById_ReturnsProject_WhenProjectExists()
    {
        await using var context = CreateContext();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Proyecto Demo",
            Description = "Testing",
            CreatedAt = DateTime.UtcNow
        };
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        var controller = new ProjectsController(context);

        var result = await controller.GetProjectById(project.Id, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedProject = Assert.IsType<Project>(okResult.Value);
        Assert.Equal(project.Id, returnedProject.Id);
        Assert.Equal("Proyecto Demo", returnedProject.Name);
    }

    [Fact]
    public async Task CreateProject_ReturnsBadRequest_WhenNameIsMissing()
    {
        await using var context = CreateContext();
        var controller = new ProjectsController(context);
        var request = new ProjectsController.CreateProjectRequest("   ", null);

        var result = await controller.CreateProject(request, CancellationToken.None);

        var validationResult = Assert.IsType<ObjectResult>(result.Result);
        var problem = Assert.IsType<ValidationProblemDetails>(validationResult.Value);
        Assert.Contains(nameof(ProjectsController.CreateProjectRequest.Name), problem.Errors.Keys);
        Assert.Empty(context.Projects);
    }

    [Fact]
    public async Task CreateProject_ReturnsCreated_WhenPayloadIsValid()
    {
        await using var context = CreateContext();
        var controller = new ProjectsController(context);
        var request = new ProjectsController.CreateProjectRequest("  Proyecto válido  ", "  desc  ");

        var result = await controller.CreateProject(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var project = Assert.IsType<Project>(created.Value);
        Assert.Equal("Proyecto válido", project.Name);
        Assert.Equal("desc", project.Description);
        Assert.Single(context.Projects);
    }

    [Fact]
    public async Task DeleteProject_ReturnsNotFound_WhenProjectDoesNotExist()
    {
        await using var context = CreateContext();
        var controller = new ProjectsController(context);

        var result = await controller.DeleteProject(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteProject_RemovesProject_WhenProjectExists()
    {
        await using var context = CreateContext();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Borrar",
            CreatedAt = DateTime.UtcNow
        };
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        var controller = new ProjectsController(context);

        var result = await controller.DeleteProject(project.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(context.Projects);
    }

    [Fact]
    public async Task AddTaskToProject_ReturnsNotFound_WhenProjectIsMissing()
    {
        await using var context = CreateContext();
        var controller = new ProjectsController(context);
        var request = new ProjectsController.CreateTaskRequest("Task", null);

        var result = await controller.AddTaskToProject(Guid.NewGuid(), request, CancellationToken.None);

        Assert.IsType<ActionResult<TaskItem>>(result);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task AddTaskToProject_ReturnsBadRequest_WhenTitleIsMissing()
    {
        await using var context = CreateContext();
        var project = new Project { Id = Guid.NewGuid(), Name = "Con tareas", CreatedAt = DateTime.UtcNow };
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        var controller = new ProjectsController(context);
        var request = new ProjectsController.CreateTaskRequest("   ", null);

        var result = await controller.AddTaskToProject(project.Id, request, CancellationToken.None);

        var validationResult = Assert.IsType<ObjectResult>(result.Result);
        var problem = Assert.IsType<ValidationProblemDetails>(validationResult.Value);
        Assert.Contains(nameof(ProjectsController.CreateTaskRequest.Title), problem.Errors.Keys);
        Assert.Empty(context.TaskItems);
    }

    [Fact]
    public async Task AddTaskToProject_ReturnsCreated_WhenPayloadIsValid()
    {
        await using var context = CreateContext();
        var project = new Project { Id = Guid.NewGuid(), Name = "Con tareas", CreatedAt = DateTime.UtcNow };
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        var controller = new ProjectsController(context);
        var request = new ProjectsController.CreateTaskRequest("  Tarea nueva  ", "  detalle  ", TaskPriority.High);

        var result = await controller.AddTaskToProject(project.Id, request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var task = Assert.IsType<TaskItem>(created.Value);
        Assert.Equal(project.Id, task.ProjectId);
        Assert.Equal("Tarea nueva", task.Title);
        Assert.Equal(TaskPriority.High, task.Priority);
        Assert.Single(context.TaskItems);
    }
}
