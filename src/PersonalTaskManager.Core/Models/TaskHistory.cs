namespace PersonalTaskManager.Core.Models;

public sealed class TaskHistory : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TaskId { get; set; }

    public HistoryType Type { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DeleteOrigin? DeleteOrigin { get; set; }
}
