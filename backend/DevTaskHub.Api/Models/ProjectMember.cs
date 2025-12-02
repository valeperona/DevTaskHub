namespace DevTaskHub.Api.Models;

public class ProjectMember
{
    public Guid ProjectId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public Project? Project { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public ProjectRole Role { get; set; } = ProjectRole.Collaborator;
}

public enum ProjectRole
{
    Owner = 0,
    Collaborator = 1,
    Viewer = 2
}
