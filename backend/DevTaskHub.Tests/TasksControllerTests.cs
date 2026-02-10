using DevTaskHub.Api.Controllers;
using DevTaskHub.Api.Models;
using DevTaskHub.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace DevTaskHub.Tests;

public class TasksControllerTests
{
    private static TasksController CreateController(Mock<ITaskService> serviceMock, Guid userId)
    {
        var controller = new TasksController(serviceMock.Object).WithUser(userId);
        return controller;
    }

    [Fact]
    public async Task GetMyTasks_ReturnsTasksFromService()
    {
        var userId = Guid.NewGuid();
        var tasks = new List<TaskItem> { new() { Id = Guid.NewGuid(), Title = "T", ProjectId = Guid.NewGuid(), Status = Api.Models.TaskStatus.ToDo, Priority = Api.Models.TaskPriority.Low, CreatedAt = DateTime.UtcNow } };
        var service = new Mock<ITaskService>();
        service.Setup(s => s.GetMyTasksAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(tasks);
        var controller = CreateController(service, userId);

        var result = await controller.GetMyTasks(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(tasks, ok.Value);
    }

    [Fact]
    public async Task UpdateTask_MapsBadRequest()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<ITaskService>();
        service.Setup(s => s.UpdateTaskAsync(userId, It.IsAny<Guid>(), It.IsAny<TasksController.UpdateTaskRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TaskResult.BadRequest("error"));
        var controller = CreateController(service, userId);

        var result = await controller.UpdateTask(Guid.NewGuid(), new TasksController.UpdateTaskRequest(Api.Models.TaskStatus.ToDo, Api.Models.TaskPriority.Medium, null, null, null), CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var payload = bad.Value;
        var prop = payload?.GetType().GetProperty("message");
        Assert.Equal("error", prop?.GetValue(payload)?.ToString());
    }

    [Fact]
    public async Task AddChecklistItem_ReturnsCreatedOnSuccess()
    {
        var userId = Guid.NewGuid();
        var checklist = new TaskChecklistItem { Id = Guid.NewGuid(), Title = "Item", TaskItemId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        var service = new Mock<ITaskService>();
        service.Setup(s => s.AddChecklistItemAsync(userId, checklist.TaskItemId, "Item", It.IsAny<CancellationToken>()))
            .ReturnsAsync(TaskResult<TaskChecklistItem>.Success(checklist));
        var controller = CreateController(service, userId);

        var result = await controller.AddChecklistItem(checklist.TaskItemId, new TasksController.ChecklistItemRequest("Item"), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Same(checklist, created.Value);
    }

    [Fact]
    public async Task DeleteTask_ReturnsForbid()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<ITaskService>();
        service.Setup(s => s.DeleteTaskAsync(userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TaskResult.Forbid());
        var controller = CreateController(service, userId);

        var result = await controller.DeleteTask(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ToggleChecklist_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<ITaskService>();
        service.Setup(s => s.ToggleChecklistItemAsync(userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TaskResult.NotFound());
        var controller = CreateController(service, userId);

        var result = await controller.ToggleChecklistItem(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
