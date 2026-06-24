namespace PersonalTaskManager.Core.Models;

public sealed class Project : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DeleteOrigin? DeleteOrigin { get; set; }

    public List<ProjectStatus> Statuses { get; set; } = [];

    public List<WorkTask> Tasks { get; set; } = [];
}
