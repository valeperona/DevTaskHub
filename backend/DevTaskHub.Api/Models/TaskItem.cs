using System.Text.Json.Serialization;

namespace DevTaskHub.Api.Models;

public class TaskItem
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public User? AssignedTo { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.ToDo;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public DateTime? DueDate { get; set; }
    public List<string> Labels { get; set; } = new();
    public bool CompletedLate { get; set; }
    public DateTime CreatedAt { get; set; }
    [JsonIgnore]
    public Project? Project { get; set; }
    public ICollection<TaskChecklistItem> Checklist { get; set; } = new List<TaskChecklistItem>();
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
