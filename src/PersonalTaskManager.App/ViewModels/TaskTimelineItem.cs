using PersonalTaskManager.Core.Models;

namespace PersonalTaskManager.App.ViewModels;

public sealed class TaskTimelineItem
{
    public required Guid Id { get; init; }

    public required DateTime CreatedAt { get; init; }

    public required string Kind { get; init; }

    public required string Text { get; init; }

    public TaskComment? Comment { get; init; }

    public TimeEntry? TimeEntry { get; init; }

    public TaskHistory? History { get; init; }
}
