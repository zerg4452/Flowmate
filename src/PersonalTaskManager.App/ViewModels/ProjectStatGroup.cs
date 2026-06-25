namespace PersonalTaskManager.App.ViewModels;

public sealed class ProjectStatGroup
{
    public string ProjectName { get; init; } = string.Empty;

    public string TotalText { get; init; } = string.Empty;

    public IReadOnlyList<TaskStatRow> Tasks { get; init; } = [];
}

public sealed class TaskStatRow
{
    public string TaskTitle { get; init; } = string.Empty;

    public string MinutesText { get; init; } = string.Empty;
}
