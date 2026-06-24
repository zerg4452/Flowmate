namespace PersonalTaskManager.Core.Models;

public sealed class WorkTask : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public Guid StatusId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DeleteOrigin? DeleteOrigin { get; set; }

    public List<TaskComment> Comments { get; set; } = [];

    public List<TaskHistory> Histories { get; set; } = [];

    public List<TimeEntry> TimeEntries { get; set; } = [];
}
