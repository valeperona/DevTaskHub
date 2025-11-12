namespace DevTaskHub.Api.Models;

public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
