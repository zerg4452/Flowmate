namespace PersonalTaskManager.Core.Models;

public sealed class TimeEntry : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TaskId { get; set; }

    public DateOnly WorkDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public int Minutes { get; set; }

    public string Memo { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DeleteOrigin? DeleteOrigin { get; set; }
}
