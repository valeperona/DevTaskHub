using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevTaskHub.Api.Controllers;
using DevTaskHub.Api.Data;
using DevTaskHub.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevTaskHub.Tests;

public class TasksControllerTests
{
    private static DevTaskHubContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DevTaskHubContext>()
            .UseInMemoryDatabase($"TasksControllerTests-{Guid.NewGuid()}")
            .Options;

        return new DevTaskHubContext(options);
    }

    [Fact]
    public async Task UpdateTask_ReturnsNotFound_WhenTaskDoesNotExist()
    {
        await using var context = CreateContext();
        var controller = new TasksController(context);
        var request = new TasksController.UpdateTaskRequest(DevTaskHub.Api.Models.TaskStatus.InProgress, DevTaskHub.Api.Models.TaskPriority.High);

        var result = await controller.UpdateTask(Guid.NewGuid(), request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateTask_UpdatesTask_WhenTaskExists()
    {
        await using var context = CreateContext();
        var project = new Project { Id = Guid.NewGuid(), Name = "Backend", CreatedAt = DateTime.UtcNow };
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Title = "Fix bug",
            Status = DevTaskHub.Api.Models.TaskStatus.ToDo,
            Priority = DevTaskHub.Api.Models.TaskPriority.Low,
            CreatedAt = DateTime.UtcNow
        };
        context.Projects.Add(project);
        context.TaskItems.Add(task);
        await context.SaveChangesAsync();
        var controller = new TasksController(context);
        var request = new TasksController.UpdateTaskRequest(DevTaskHub.Api.Models.TaskStatus.Done, DevTaskHub.Api.Models.TaskPriority.High);

        var result = await controller.UpdateTask(task.Id, request, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var updatedTask = await context.TaskItems.FirstAsync(t => t.Id == task.Id);
        Assert.Equal(DevTaskHub.Api.Models.TaskStatus.Done, updatedTask.Status);
        Assert.Equal(DevTaskHub.Api.Models.TaskPriority.High, updatedTask.Priority);
    }

    [Fact]
    public async Task DeleteTask_ReturnsNotFound_WhenTaskDoesNotExist()
    {
        await using var context = CreateContext();
        var controller = new TasksController(context);

        var result = await controller.DeleteTask(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteTask_RemovesTask_WhenTaskExists()
    {
        await using var context = CreateContext();
        var project = new Project { Id = Guid.NewGuid(), Name = "Frontend", CreatedAt = DateTime.UtcNow };
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Title = "Task",
            Status = DevTaskHub.Api.Models.TaskStatus.InProgress,
            Priority = DevTaskHub.Api.Models.TaskPriority.Medium,
            CreatedAt = DateTime.UtcNow
        };
        context.Projects.Add(project);
        context.TaskItems.Add(task);
        await context.SaveChangesAsync();
        var controller = new TasksController(context);

        var result = await controller.DeleteTask(task.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.False(context.TaskItems.Any());
    }
}
