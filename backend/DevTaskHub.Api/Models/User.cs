namespace DevTaskHub.Api.Models;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    [System.Text.Json.Serialization.JsonIgnore]
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public ICollection<Project> Projects { get; set; } = new List<Project>();
    [System.Text.Json.Serialization.JsonIgnore]
    public ICollection<ProjectMember> Memberships { get; set; } = new List<ProjectMember>();
}
