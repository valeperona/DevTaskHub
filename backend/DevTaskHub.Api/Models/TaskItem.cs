using System.Text.Json.Serialization;

namespace DevTaskHub.Api.Models;

public class TaskItem
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.ToDo;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public DateTime CreatedAt { get; set; }
    [JsonIgnore]
    public Project? Project { get; set; }
}

public enum TaskStatus
{
    ToDo,
    InProgress,
    InReview,
    Done
}

public enum TaskPriority
{
    Low,
    Medium,
    High
}
