namespace DevTaskHub.Api.Models;

public class ProjectInvitation
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public ProjectRole Role { get; set; } = ProjectRole.Collaborator;
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
    public DateTime CreatedAt { get; set; }
}

public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2
}
