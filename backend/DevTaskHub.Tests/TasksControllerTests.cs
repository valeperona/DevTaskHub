using DevTaskHub.Api.Controllers;
using DevTaskHub.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
using TaskPriority = DevTaskHub.Api.Models.TaskPriority;
using TaskStatus = DevTaskHub.Api.Models.TaskStatus;

namespace DevTaskHub.Tests;

public class TasksControllerTests
{
    private static (DevTaskHub.Api.Data.DevTaskHubContext Context, Project Project, User Owner, User Collaborator, User Viewer, TaskItem Task) SeedTask(string dbName)
    {
        var context = TestUtils.CreateContext(dbName);
        var owner = TestUtils.CreateUser("owner@test.com", Guid.NewGuid());
        var collaborator = TestUtils.CreateUser("collab@test.com", Guid.NewGuid());
        var viewer = TestUtils.CreateUser("viewer@test.com", Guid.NewGuid());
        var project = new Project { Id = Guid.NewGuid(), Name = "P", OwnerId = owner.Id, CreatedAt = DateTime.UtcNow };
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Title = "Task",
            Status = TaskStatus.InProgress,
            Priority = TaskPriority.Medium,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };

        context.Users.AddRange(owner, collaborator, viewer);
        context.Projects.Add(project);
        context.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = project.Id, UserId = owner.Id, Role = ProjectRole.Owner },
            new ProjectMember { ProjectId = project.Id, UserId = collaborator.Id, Role = ProjectRole.Collaborator },
            new ProjectMember { ProjectId = project.Id, UserId = viewer.Id, Role = ProjectRole.Viewer }
        );
        context.TaskItems.Add(task);
        context.SaveChanges();
        return (context, project, owner, collaborator, viewer, task);
    }

    [Fact]
    public async Task UpdateTask_ReturnsBadRequest_WhenTransitionInvalid()
    {
        var (context, _, owner, _, _, task) = SeedTask(nameof(UpdateTask_ReturnsBadRequest_WhenTransitionInvalid));
        var controller = new TasksController(context).WithUser(owner.Id);

        var result = await controller.UpdateTask(task.Id, new TasksController.UpdateTaskRequest(TaskStatus.ToDo, TaskPriority.Medium, owner.Id, null, null), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateTask_SetsCompletedLate_WhenClosedAfterDueDate()
    {
        var (context, _, owner, _, _, task) = SeedTask(nameof(UpdateTask_SetsCompletedLate_WhenClosedAfterDueDate));
        task.Status = TaskStatus.InReview;
        task.DueDate = DateTime.UtcNow.AddDays(-1);
        context.SaveChanges();
        var controller = new TasksController(context).WithUser(owner.Id);

        var result = await controller.UpdateTask(task.Id, new TasksController.UpdateTaskRequest(TaskStatus.Done, TaskPriority.Medium, owner.Id, task.DueDate, new List<string>()), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var updated = await context.TaskItems.FirstAsync(t => t.Id == task.Id);
        Assert.True(updated.CompletedLate);
    }

    [Fact]
    public async Task UpdateTask_ReturnsBadRequest_WhenChecklistPending()
    {
        var (context, _, owner, _, _, task) = SeedTask(nameof(UpdateTask_ReturnsBadRequest_WhenChecklistPending));
        context.TaskChecklistItems.Add(new TaskChecklistItem { Id = Guid.NewGuid(), TaskItemId = task.Id, Title = "Item", IsDone = false, CreatedAt = DateTime.UtcNow });
        context.SaveChanges();
        var controller = new TasksController(context).WithUser(owner.Id);

        var result = await controller.UpdateTask(task.Id, new TasksController.UpdateTaskRequest(TaskStatus.Done, TaskPriority.High, owner.Id, null, null), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateTask_ReturnsBadRequest_WhenHighPriorityWithoutAssignee()
    {
        var (context, _, owner, _, _, task) = SeedTask(nameof(UpdateTask_ReturnsBadRequest_WhenHighPriorityWithoutAssignee));
        var controller = new TasksController(context).WithUser(owner.Id);

        var result = await controller.UpdateTask(task.Id, new TasksController.UpdateTaskRequest(TaskStatus.InProgress, TaskPriority.High, null, null, null), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task AddChecklistItem_ReturnsBadRequest_WhenTitleEmpty()
    {
        var (context, _, owner, _, _, task) = SeedTask(nameof(AddChecklistItem_ReturnsBadRequest_WhenTitleEmpty));
        var controller = new TasksController(context).WithUser(owner.Id);

        var result = await controller.AddChecklistItem(task.Id, new TasksController.ChecklistItemRequest("   "), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task AddChecklistItem_ReturnsForbid_WhenViewer()
    {
        var (context, _, _, _, viewer, task) = SeedTask(nameof(AddChecklistItem_ReturnsForbid_WhenViewer));
        var controller = new TasksController(context).WithUser(viewer.Id);

        var result = await controller.AddChecklistItem(task.Id, new TasksController.ChecklistItemRequest("item"), CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task ToggleChecklist_ReturnsForbid_ForViewer()
    {
        var (context, _, _, _, viewer, task) = SeedTask(nameof(ToggleChecklist_ReturnsForbid_ForViewer));
        var item = new TaskChecklistItem { Id = Guid.NewGuid(), TaskItemId = task.Id, Title = "Item", IsDone = false, CreatedAt = DateTime.UtcNow };
        context.TaskChecklistItems.Add(item);
        context.SaveChanges();
        var controller = new TasksController(context).WithUser(viewer.Id);

        var result = await controller.ToggleChecklistItem(item.Id, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ToggleChecklist_UpdatesFlag_WhenAllowed()
    {
        var (context, _, owner, _, _, task) = SeedTask(nameof(ToggleChecklist_UpdatesFlag_WhenAllowed));
        var item = new TaskChecklistItem { Id = Guid.NewGuid(), TaskItemId = task.Id, Title = "Item", IsDone = false, CreatedAt = DateTime.UtcNow };
        context.TaskChecklistItems.Add(item);
        context.SaveChanges();
        var controller = new TasksController(context).WithUser(owner.Id);

        var result = await controller.ToggleChecklistItem(item.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var updated = await context.TaskChecklistItems.FirstAsync(c => c.Id == item.Id);
        Assert.True(updated.IsDone);
    }

    [Fact]
    public async Task DeleteChecklist_ReturnsNotFound_WhenItemMissing()
    {
        var (context, _, owner, _, _, _) = SeedTask(nameof(DeleteChecklist_ReturnsNotFound_WhenItemMissing));
        var controller = new TasksController(context).WithUser(owner.Id);

        var result = await controller.DeleteChecklistItem(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteTask_ReturnsForbid_ForViewer()
    {
        var (context, _, _, _, viewer, task) = SeedTask(nameof(DeleteTask_ReturnsForbid_ForViewer));
        var controller = new TasksController(context).WithUser(viewer.Id);

        var result = await controller.DeleteTask(task.Id, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task DeleteTask_RemovesTask_WhenAllowed()
    {
        var (context, _, owner, _, _, task) = SeedTask(nameof(DeleteTask_RemovesTask_WhenAllowed));
        var controller = new TasksController(context).WithUser(owner.Id);

        var result = await controller.DeleteTask(task.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.False(await context.TaskItems.AnyAsync(t => t.Id == task.Id));
    }

    [Fact]
    public async Task UpdateTask_ReturnsBadRequest_WhenAssigningToViewer()
    {
        var (context, _, owner, _, viewer, task) = SeedTask(nameof(UpdateTask_ReturnsBadRequest_WhenAssigningToViewer));
        var controller = new TasksController(context).WithUser(owner.Id);

        var result = await controller.UpdateTask(task.Id, new TasksController.UpdateTaskRequest(TaskStatus.InProgress, TaskPriority.Medium, viewer.Id, null, null), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateTask_ReturnsForbid_WhenViewerEdits()
    {
        var (context, _, _, _, viewer, task) = SeedTask(nameof(UpdateTask_ReturnsForbid_WhenViewerEdits));
        var controller = new TasksController(context).WithUser(viewer.Id);

        var result = await controller.UpdateTask(task.Id, new TasksController.UpdateTaskRequest(TaskStatus.InProgress, TaskPriority.Medium, viewer.Id, null, null), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetMyTasks_ReturnsOnlyAssignedOnes()
    {
        var (context, project, owner, collaborator, _, task) = SeedTask(nameof(GetMyTasks_ReturnsOnlyAssignedOnes));
        task.AssignedToUserId = collaborator.Id;
        context.SaveChanges();
        var otherTask = new TaskItem { Id = Guid.NewGuid(), ProjectId = project.Id, Title = "Other", Status = TaskStatus.ToDo, Priority = TaskPriority.Low, CreatedAt = DateTime.UtcNow };
        context.TaskItems.Add(otherTask);
        context.SaveChanges();
        var controller = new TasksController(context).WithUser(collaborator.Id);

        var result = await controller.GetMyTasks(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var tasks = Assert.IsAssignableFrom<IEnumerable<TaskItem>>(ok.Value);
        Assert.Single(tasks);
        Assert.Equal(task.Id, tasks.First().Id);
    }

    [Fact]
    public async Task GetTasksForCalendar_FiltersByRange()
    {
        var (context, project, owner, collaborator, _, _) = SeedTask(nameof(GetTasksForCalendar_FiltersByRange));
        var early = new TaskItem { Id = Guid.NewGuid(), ProjectId = project.Id, Title = "Early", Status = TaskStatus.ToDo, Priority = TaskPriority.Low, DueDate = DateTime.UtcNow.AddDays(1), CreatedAt = DateTime.UtcNow, AssignedToUserId = collaborator.Id };
        var late = new TaskItem { Id = Guid.NewGuid(), ProjectId = project.Id, Title = "Late", Status = TaskStatus.ToDo, Priority = TaskPriority.Low, DueDate = DateTime.UtcNow.AddDays(10), CreatedAt = DateTime.UtcNow, AssignedToUserId = collaborator.Id };
        context.TaskItems.AddRange(early, late);
        context.SaveChanges();
        var controller = new TasksController(context).WithUser(collaborator.Id);

        var result = await controller.GetTasksForCalendar(DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(9), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var tasks = Assert.IsAssignableFrom<IEnumerable<TaskItem>>(ok.Value);
        Assert.Single(tasks);
        Assert.Equal(late.Id, tasks.First().Id);
    }
}
