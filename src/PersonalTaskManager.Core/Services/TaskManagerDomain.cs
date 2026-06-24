using PersonalTaskManager.Core.Models;

namespace PersonalTaskManager.Core.Services;

public static class TaskManagerDomain
{
    public static readonly string[] FixedStatusNames =
    [
        "To do",
        "in Progress [ing]",
        "in Progress [Test]",
        "Done"
    ];

    public static Project CreateProject(string name, DateTime? createdAt = null)
    {
        var project = new Project
        {
            Name = name.Trim(),
            CreatedAt = createdAt ?? DateTime.Now
        };

        EnsureFixedStatuses(project);

        return project;
    }

    public static void EnsureFixedStatuses(Project project)
    {
        var statusesByName = project.Statuses
            .Where(status => FixedStatusNames.Contains(status.Name))
            .GroupBy(status => status.Name)
            .ToDictionary(group => group.Key, group => group.First());

        var fixedStatuses = new List<ProjectStatus>();
        for (var index = 0; index < FixedStatusNames.Length; index++)
        {
            var name = FixedStatusNames[index];
            if (!statusesByName.TryGetValue(name, out var status))
            {
                status = new ProjectStatus
                {
                    ProjectId = project.Id,
                    Name = name
                };
            }

            status.ProjectId = project.Id;
            status.Name = name;
            status.SortOrder = index + 1;
            status.IsDefault = true;
            status.IsActive = true;
            status.IsDeleted = false;
            status.DeletedAt = null;
            status.DeleteOrigin = null;
            fixedStatuses.Add(status);
        }

        var fixedStatusIds = fixedStatuses.Select(status => status.Id).ToHashSet();
        var todoStatusId = fixedStatuses[0].Id;
        foreach (var task in project.Tasks.Where(task => !fixedStatusIds.Contains(task.StatusId)))
        {
            task.StatusId = todoStatusId;
            task.CompletedAt = null;
        }

        project.Statuses = fixedStatuses;
    }

    public static WorkTask CreateTask(Project project, string title, DateTime? createdAt = null)
    {
        var firstStatus = project.Statuses
            .Where(status => !status.IsDeleted && status.IsActive)
            .OrderBy(status => status.SortOrder)
            .First();

        var task = new WorkTask
        {
            ProjectId = project.Id,
            Title = title.Trim(),
            StatusId = firstStatus.Id,
            CreatedAt = createdAt ?? DateTime.Now
        };

        project.Tasks.Add(task);
        return task;
    }

    public static void ChangeStatus(WorkTask task, ProjectStatus newStatus, DateTime changedAt)
    {
        var oldStatusId = task.StatusId;
        task.StatusId = newStatus.Id;
        task.UpdatedAt = changedAt;
        task.CompletedAt = IsDoneStatus(newStatus) ? changedAt : null;
        task.Histories.Add(new TaskHistory
        {
            TaskId = task.Id,
            Type = HistoryType.StatusChanged,
            Message = $"Status changed to {newStatus.Name}",
            OldValue = oldStatusId == Guid.Empty ? null : oldStatusId.ToString(),
            NewValue = newStatus.Name,
            CreatedAt = changedAt
        });
    }

    public static TaskComment AddComment(WorkTask task, string content, DateTime createdAt)
    {
        var comment = new TaskComment
        {
            TaskId = task.Id,
            Content = content,
            CreatedAt = createdAt
        };

        task.Comments.Add(comment);
        task.Histories.Add(new TaskHistory
        {
            TaskId = task.Id,
            Type = HistoryType.CommentAdded,
            Message = "Comment added",
            NewValue = content,
            CreatedAt = createdAt
        });

        return comment;
    }

    public static void EditComment(TaskComment comment, string content, DateTime updatedAt)
    {
        comment.Content = content;
        comment.UpdatedAt = updatedAt;
    }

    public static void EditHistory(TaskHistory history, string message, DateTime updatedAt)
    {
        history.Message = message;
        history.UpdatedAt = updatedAt;
    }

    public static TimeEntry AddTimeEntry(WorkTask task, DateOnly workDate, int minutes, string memo)
    {
        if (minutes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minutes), "Minutes must be greater than zero.");
        }

        var now = DateTime.Now;
        var entry = new TimeEntry
        {
            TaskId = task.Id,
            WorkDate = workDate,
            Minutes = minutes,
            Memo = memo,
            CreatedAt = now
        };

        var message = $"{minutes} minutes added on {workDate:yyyy-MM-dd}";
        if (!string.IsNullOrWhiteSpace(memo))
        {
            message += $" - {memo.Trim()}";
        }

        task.TimeEntries.Add(entry);
        task.Histories.Add(new TaskHistory
        {
            TaskId = task.Id,
            Type = HistoryType.TimeAdded,
            Message = message,
            NewValue = minutes.ToString(),
            CreatedAt = now
        });

        return entry;
    }

    public static int GetTaskTotalMinutes(WorkTask task)
    {
        if (task.IsDeleted)
        {
            return 0;
        }

        return task.TimeEntries
            .Where(entry => !entry.IsDeleted)
            .Sum(entry => entry.Minutes);
    }

    public static int GetTaskMinutesByDate(WorkTask task, DateOnly workDate)
    {
        if (task.IsDeleted)
        {
            return 0;
        }

        return task.TimeEntries
            .Where(entry => !entry.IsDeleted && entry.WorkDate == workDate)
            .Sum(entry => entry.Minutes);
    }

    public static void SoftDelete(Project project, DeleteOrigin origin, DateTime? deletedAt = null)
    {
        MarkDeleted(project, origin, deletedAt);

        foreach (var status in project.Statuses)
        {
            MarkDeleted(status, DeleteOrigin.Cascade, deletedAt);
        }

        foreach (var task in project.Tasks)
        {
            SoftDelete(task, DeleteOrigin.Cascade, deletedAt);
        }
    }

    public static void SoftDelete(WorkTask task, DeleteOrigin origin, DateTime? deletedAt = null)
    {
        MarkDeleted(task, origin, deletedAt);

        foreach (var comment in task.Comments)
        {
            MarkDeleted(comment, DeleteOrigin.Cascade, deletedAt);
        }

        foreach (var history in task.Histories)
        {
            MarkDeleted(history, DeleteOrigin.Cascade, deletedAt);
        }

        foreach (var entry in task.TimeEntries)
        {
            MarkDeleted(entry, DeleteOrigin.Cascade, deletedAt);
        }
    }

    public static void SoftDelete(ISoftDeletable item, DeleteOrigin origin, DateTime? deletedAt = null)
    {
        MarkDeleted(item, origin, deletedAt);
    }

    private static void MarkDeleted(ISoftDeletable item, DeleteOrigin origin, DateTime? deletedAt)
    {
        item.IsDeleted = true;
        item.DeletedAt = deletedAt ?? DateTime.Now;
        item.DeleteOrigin = origin;
    }

    private static bool IsDoneStatus(ProjectStatus status)
    {
        return string.Equals(status.Name, "Done", StringComparison.OrdinalIgnoreCase);
    }
}
