namespace DevTaskHub.Api.Models;

public class TaskChecklistItem
{
    public Guid Id { get; set; }
    public Guid TaskItemId { get; set; }
    public TaskItem? TaskItem { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsDone { get; set; }
    public DateTime CreatedAt { get; set; }
}
