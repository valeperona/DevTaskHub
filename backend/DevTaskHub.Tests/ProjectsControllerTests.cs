using DevTaskHub.Api.Controllers;
using DevTaskHub.Api.Data;
using DevTaskHub.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
using TaskPriority = DevTaskHub.Api.Models.TaskPriority;
using TaskStatus = DevTaskHub.Api.Models.TaskStatus;

namespace DevTaskHub.Tests;

public class ProjectsControllerTests
{
    private static (DevTaskHubContext Context, Project Project, User Owner, User Collaborator, User Viewer) SeedProject(string dbName)
    {
        var context = TestUtils.CreateContext(dbName);
        var owner = TestUtils.CreateUser("owner@test.com", Guid.NewGuid());
        var collaborator = TestUtils.CreateUser("collab@test.com", Guid.NewGuid());
        var viewer = TestUtils.CreateUser("viewer@test.com", Guid.NewGuid());

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Proyecto X",
            Description = "desc",
            OwnerId = owner.Id,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.AddRange(owner, collaborator, viewer);
        context.Projects.Add(project);
        context.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = project.Id, UserId = owner.Id, Role = ProjectRole.Owner },
            new ProjectMember { ProjectId = project.Id, UserId = collaborator.Id, Role = ProjectRole.Collaborator },
            new ProjectMember { ProjectId = project.Id, UserId = viewer.Id, Role = ProjectRole.Viewer }
        );
        context.SaveChanges();
        return (context, project, owner, collaborator, viewer);
    }

    [Fact]
    public async Task CreateProject_AddsOwnerMembership()
    {
        using var context = TestUtils.CreateContext();
        var userId = Guid.NewGuid();
        var controller = new ProjectsController(context).WithUser(userId);

        var result = await controller.CreateProject(new ProjectsController.CreateProjectRequest("Nuevo", "desc"), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var project = Assert.IsType<Project>(created.Value);
        Assert.True(context.ProjectMembers.Any(pm => pm.ProjectId == project.Id && pm.UserId == userId && pm.Role == ProjectRole.Owner));
    }

    [Fact]
    public async Task CreateProject_ReturnsValidationProblem_WhenNameMissing()
    {
        using var context = TestUtils.CreateContext();
        var controller = new ProjectsController(context).WithUser(Guid.NewGuid());

        var result = await controller.CreateProject(new ProjectsController.CreateProjectRequest("   ", null), CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(400, problem.StatusCode ?? problem.StatusCode.GetValueOrDefault(400));
    }

    [Fact]
    public async Task GetProjects_FiltersByMembership()
    {
        using var context = TestUtils.CreateContext();
        var userA = TestUtils.CreateUser("a@test.com");
        var userB = TestUtils.CreateUser("b@test.com");
        var projectA = new Project { Id = Guid.NewGuid(), Name = "A", OwnerId = userA.Id, CreatedAt = DateTime.UtcNow };
        var projectB = new Project { Id = Guid.NewGuid(), Name = "B", OwnerId = userB.Id, CreatedAt = DateTime.UtcNow };
        context.Users.AddRange(userA, userB);
        context.Projects.AddRange(projectA, projectB);
        context.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = projectA.Id, UserId = userA.Id, Role = ProjectRole.Owner },
            new ProjectMember { ProjectId = projectB.Id, UserId = userB.Id, Role = ProjectRole.Owner }
        );
        context.SaveChanges();
        var controller = new ProjectsController(context).WithUser(userA.Id);

        var result = await controller.GetProjects(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var projects = Assert.IsAssignableFrom<IEnumerable<ProjectsController.ProjectDto>>(ok.Value);
        Assert.Single(projects);
        Assert.Equal(projectA.Id, projects.First().Id);
    }

    [Fact]
    public async Task AddTaskToProject_ReturnsBadRequest_WhenHighPriorityWithoutAssignee()
    {
        var (context, project, owner, _, _) = SeedProject(nameof(AddTaskToProject_ReturnsBadRequest_WhenHighPriorityWithoutAssignee));
        var controller = new ProjectsController(context).WithUser(owner.Id);

        var result = await controller.AddTaskToProject(project.Id, new ProjectsController.CreateTaskRequest("Tarea", null, TaskPriority.High, null, null, null), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task AddTaskToProject_ReturnsForbid_WhenViewer()
    {
        var (context, project, _, _, viewer) = SeedProject(nameof(AddTaskToProject_ReturnsForbid_WhenViewer));
        var controller = new ProjectsController(context).WithUser(viewer.Id);

        var result = await controller.AddTaskToProject(project.Id, new ProjectsController.CreateTaskRequest("Tarea", null, TaskPriority.Medium, null, null, null), CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task AddTaskToProject_ReturnsNotFound_WhenUserNotMember()
    {
        var (context, project, _, _, _) = SeedProject(nameof(AddTaskToProject_ReturnsNotFound_WhenUserNotMember));
        var outsider = TestUtils.CreateUser("outsider@test.com");
        context.Users.Add(outsider);
        context.SaveChanges();
        var controller = new ProjectsController(context).WithUser(outsider.Id);

        var result = await controller.AddTaskToProject(project.Id, new ProjectsController.CreateTaskRequest("Tarea", null, TaskPriority.Medium, null, null, null), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task AddTaskToProject_CreatesTask_WhenValid()
    {
        var (context, project, owner, _, _) = SeedProject(nameof(AddTaskToProject_CreatesTask_WhenValid));
        var controller = new ProjectsController(context).WithUser(owner.Id);

        var result = await controller.AddTaskToProject(project.Id, new ProjectsController.CreateTaskRequest("Tarea", "desc", TaskPriority.Low, owner.Id, DateTime.UtcNow, new List<string> { "a" }), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var task = Assert.IsType<TaskItem>(created.Value);
        Assert.Equal(project.Id, task.ProjectId);
    }

    [Fact]
    public async Task AddTaskToProject_ReturnsValidationProblem_WhenTitleMissing()
    {
        var (context, project, owner, _, _) = SeedProject(nameof(AddTaskToProject_ReturnsValidationProblem_WhenTitleMissing));
        var controller = new ProjectsController(context).WithUser(owner.Id);

        var result = await controller.AddTaskToProject(project.Id, new ProjectsController.CreateTaskRequest("   ", null, TaskPriority.Low, null, null, null), CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(400, problem.StatusCode ?? problem.StatusCode.GetValueOrDefault(400));
    }

    [Fact]
    public async Task TransferOwnership_ReturnsForbid_WhenCallerIsNotOwner()
    {
        var (context, project, owner, collaborator, _) = SeedProject(nameof(TransferOwnership_ReturnsForbid_WhenCallerIsNotOwner));
        var controller = new ProjectsController(context).WithUser(collaborator.Id);

        var result = await controller.TransferOwnership(project.Id, new ProjectsController.TransferOwnerRequest(owner.Id), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task TransferOwnership_ReturnsNotFound_WhenTargetIsNotMember()
    {
        var (context, project, owner, _, _) = SeedProject(nameof(TransferOwnership_ReturnsNotFound_WhenTargetIsNotMember));
        var newUser = TestUtils.CreateUser("new@test.com");
        context.Users.Add(newUser);
        context.SaveChanges();
        var controller = new ProjectsController(context).WithUser(owner.Id);

        var result = await controller.TransferOwnership(project.Id, new ProjectsController.TransferOwnerRequest(newUser.Id), CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains("nuevo owner debe ser miembro", notFound.Value!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetProjectById_ReturnsNotFound_WhenUserNotMember()
    {
        var (context, project, _, _, _) = SeedProject(nameof(GetProjectById_ReturnsNotFound_WhenUserNotMember));
        var outsider = TestUtils.CreateUser("out@test.com");
        context.Users.Add(outsider);
        context.SaveChanges();
        var controller = new ProjectsController(context).WithUser(outsider.Id);

        var result = await controller.GetProjectById(project.Id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task LeaveProject_ReassignsTasksToOwner()
    {
        var (context, project, owner, collaborator, _) = SeedProject(nameof(LeaveProject_ReassignsTasksToOwner));
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Title = "Task",
            Status = TaskStatus.ToDo,
            Priority = TaskPriority.Medium,
            AssignedToUserId = collaborator.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.TaskItems.Add(task);
        context.SaveChanges();
        var controller = new ProjectsController(context).WithUser(collaborator.Id);

        var result = await controller.LeaveProject(project.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var updated = await context.TaskItems.FirstAsync(t => t.Id == task.Id);
        Assert.Equal(owner.Id, updated.AssignedToUserId);
    }

    [Fact]
    public async Task LeaveProject_ReturnsNotFound_WhenUserNotMember()
    {
        var (context, project, _, _, _) = SeedProject(nameof(LeaveProject_ReturnsNotFound_WhenUserNotMember));
        var outsider = TestUtils.CreateUser("outside@test.com");
        context.Users.Add(outsider);
        context.SaveChanges();
        var controller = new ProjectsController(context).WithUser(outsider.Id);

        var result = await controller.LeaveProject(project.Id, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task AddMember_ReturnsConflict_WhenUserAlreadyMember()
    {
        var (context, project, owner, collaborator, _) = SeedProject(nameof(AddMember_ReturnsConflict_WhenUserAlreadyMember));
        var controller = new ProjectsController(context).WithUser(owner.Id);

        var result = await controller.AddMember(project.Id, new ProjectsController.AddMemberRequest(collaborator.Email, ProjectRole.Collaborator), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task AddMember_ReturnsForbid_WhenCallerIsNotOwner()
    {
        var (context, project, _, collaborator, _) = SeedProject(nameof(AddMember_ReturnsForbid_WhenCallerIsNotOwner));
        var controller = new ProjectsController(context).WithUser(collaborator.Id);

        var result = await controller.AddMember(project.Id, new ProjectsController.AddMemberRequest("new@test.com", ProjectRole.Collaborator), CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task InviteMember_ReturnsBadRequest_WhenRoleIsOwner()
    {
        var (context, project, owner, _, _) = SeedProject(nameof(InviteMember_ReturnsBadRequest_WhenRoleIsOwner));
        var target = TestUtils.CreateUser("target@test.com");
        context.Users.Add(target);
        context.SaveChanges();
        var controller = new ProjectsController(context).WithUser(owner.Id);

        var result = await controller.InviteMember(project.Id, new ProjectsController.InvitationRequest(target.Email, ProjectRole.Owner), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task InviteMember_ReturnsConflict_WhenPendingInvitationExists()
    {
        var (context, project, owner, _, _) = SeedProject(nameof(InviteMember_ReturnsConflict_WhenPendingInvitationExists));
        var target = TestUtils.CreateUser("target@test.com");
        context.Users.Add(target);
        context.ProjectInvitations.Add(new ProjectInvitation
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = target.Id,
            Role = ProjectRole.Collaborator,
            Status = InvitationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        context.SaveChanges();
        var controller = new ProjectsController(context).WithUser(owner.Id);

        var result = await controller.InviteMember(project.Id, new ProjectsController.InvitationRequest(target.Email, ProjectRole.Collaborator), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task RespondInvitation_ReturnsForbid_WhenUserDiffers()
    {
        var (context, project, owner, collaborator, _) = SeedProject(nameof(RespondInvitation_ReturnsForbid_WhenUserDiffers));
        var invitee = TestUtils.CreateUser("invitee@test.com");
        context.Users.Add(invitee);
        var invitation = new ProjectInvitation
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = invitee.Id,
            Role = ProjectRole.Collaborator,
            Status = InvitationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        context.ProjectInvitations.Add(invitation);
        context.SaveChanges();
        var controller = new ProjectsController(context).WithUser(collaborator.Id);

        var result = await controller.RespondInvitation(invitation.Id, new ProjectsController.InvitationRespondRequest(InvitationStatus.Accepted), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetTasksForProject_ReturnsNotFound_WhenNotMember()
    {
        var (context, project, _, _, _) = SeedProject(nameof(GetTasksForProject_ReturnsNotFound_WhenNotMember));
        var outsider = TestUtils.CreateUser("outside@test.com");
        context.Users.Add(outsider);
        context.SaveChanges();
        var controller = new ProjectsController(context).WithUser(outsider.Id);

        var result = await controller.GetTasksForProject(project.Id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetTasksForProject_ReturnsTasks_WhenMember()
    {
        var (context, project, owner, _, _) = SeedProject(nameof(GetTasksForProject_ReturnsTasks_WhenMember));
        var task = new TaskItem { Id = Guid.NewGuid(), ProjectId = project.Id, Title = "T", Status = TaskStatus.ToDo, Priority = TaskPriority.Medium, CreatedAt = DateTime.UtcNow };
        context.TaskItems.Add(task);
        context.SaveChanges();
        var controller = new ProjectsController(context).WithUser(owner.Id);

        var result = await controller.GetTasksForProject(project.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var tasks = Assert.IsAssignableFrom<IEnumerable<TaskItem>>(ok.Value);
        Assert.Single(tasks);
        Assert.Equal(task.Id, tasks.First().Id);
    }
}
