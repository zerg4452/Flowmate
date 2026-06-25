using PersonalTaskManager.Core.Models;

namespace PersonalTaskManager.App.ViewModels;

public sealed class TrashItemView
{
    public required string Kind { get; init; }

    public required string Name { get; init; }

    public DateTime? DeletedAt { get; init; }

    public required Project Project { get; init; }

    public WorkTask? Task { get; init; }

    public ProjectStatus? Status { get; init; }

    public TaskComment? Comment { get; init; }

    public ProjectDocument? Document { get; init; }
}
