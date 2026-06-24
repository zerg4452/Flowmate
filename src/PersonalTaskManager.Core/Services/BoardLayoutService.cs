using PersonalTaskManager.Core.Models;

namespace PersonalTaskManager.Core.Services;

public static class BoardLayoutService
{
    public static IReadOnlyList<BoardColumn> BuildColumns(
        IEnumerable<Project> projects,
        Project? selectedProject,
        DateOnly? startDate,
        DateOnly? endDate,
        string keyword)
    {
        var activeProjects = projects
            .Where(project => project.IsActive && !project.IsDeleted)
            .Where(project => selectedProject is null || project.Id == selectedProject.Id)
            .OrderBy(project => project.Name)
            .ToList();

        foreach (var project in activeProjects)
        {
            TaskManagerDomain.EnsureFixedStatuses(project);
        }

        return TaskManagerDomain.FixedStatusNames
            .Select((statusName, index) => BuildColumn(activeProjects, statusName, index, startDate, endDate, keyword))
            .ToList();
    }

    private static BoardColumn BuildColumn(
        IReadOnlyList<Project> projects,
        string statusName,
        int statusIndex,
        DateOnly? startDate,
        DateOnly? endDate,
        string keyword)
    {
        var projectGroups = new List<BoardProjectGroup>();
        foreach (var project in projects)
        {
            var statusIds = project.Statuses
                .Where(status => !status.IsDeleted && status.Name == statusName)
                .Select(status => status.Id)
                .ToHashSet();

            var tasks = project.Tasks
                .Where(task => !task.IsDeleted && statusIds.Contains(task.StatusId))
                .Where(task => IsInDateRange(task, startDate, endDate))
                .Where(task => MatchesKeyword(task, keyword))
                .OrderByDescending(task => task.CreatedAt)
                .ToList();

            if (tasks.Count > 0)
            {
                projectGroups.Add(new BoardProjectGroup(
                    project.Id,
                    project.Name,
                    HeaderColorFor(projectGroups.Count),
                    tasks));
            }
        }

        return new BoardColumn(statusName, HeaderColorForStatus(statusIndex), projectGroups);
    }

    private static bool IsInDateRange(WorkTask task, DateOnly? startDate, DateOnly? endDate)
    {
        var createdDate = DateOnly.FromDateTime(task.CreatedAt);
        return (startDate is null || createdDate >= startDate) &&
               (endDate is null || createdDate <= endDate);
    }

    private static bool MatchesKeyword(WorkTask task, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        return task.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               task.Body.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static string HeaderColorForStatus(int index)
    {
        return index switch
        {
            0 => "#DBE4F0",
            1 => "#FCE9B8",
            2 => "#CDEBD8",
            _ => "#E2E5EA"
        };
    }

    private static string HeaderColorFor(int index)
    {
        return index % 2 == 0 ? "#E8EEF7" : "#E6F3F6";
    }
}

public sealed record BoardColumn(
    string StatusName,
    string HeaderColor,
    IReadOnlyList<BoardProjectGroup> ProjectGroups);

public sealed record BoardProjectGroup(
    Guid ProjectId,
    string ProjectName,
    string HeaderColor,
    IReadOnlyList<WorkTask> Tasks);
