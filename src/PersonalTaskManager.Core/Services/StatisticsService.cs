using PersonalTaskManager.Core.Models;
using System.Globalization;

namespace PersonalTaskManager.Core.Services;

public static class StatisticsService
{
    public static int GetProjectTotalMinutes(Project project)
    {
        if (project.IsDeleted)
        {
            return 0;
        }

        return project.Tasks.Sum(TaskManagerDomain.GetTaskTotalMinutes);
    }

    public static IReadOnlyList<ProjectTimeSummary> GetProjectDailyMinutes(Project project)
    {
        return project.Tasks
            .Where(task => !task.IsDeleted)
            .SelectMany(task => task.TimeEntries)
            .Where(entry => !entry.IsDeleted)
            .GroupBy(entry => entry.WorkDate)
            .OrderBy(group => group.Key)
            .Select(group => new ProjectTimeSummary(group.Key.ToString("yyyy-MM-dd"), group.Sum(entry => entry.Minutes)))
            .ToList();
    }

    public static IReadOnlyList<PeriodSummary> GetProjectMinutes(Project project, StatisticsPeriod period)
    {
        if (project.IsDeleted)
        {
            return [];
        }

        return project.Tasks
            .Where(task => !task.IsDeleted)
            .SelectMany(task => task.TimeEntries)
            .Where(entry => !entry.IsDeleted)
            .GroupBy(entry => ToPeriodKey(entry.WorkDate, period))
            .OrderBy(group => group.Key)
            .Select(group => new PeriodSummary(group.Key, group.Sum(entry => entry.Minutes)))
            .ToList();
    }

    public static int GetProjectTaskCount(Project project, DateOnly startDate, DateOnly endDate)
    {
        if (project.IsDeleted)
        {
            return 0;
        }

        return project.Tasks.Count(task =>
            !task.IsDeleted &&
            DateOnly.FromDateTime(task.CreatedAt) >= startDate &&
            DateOnly.FromDateTime(task.CreatedAt) <= endDate);
    }

    private static string ToPeriodKey(DateOnly date, StatisticsPeriod period)
    {
        return period switch
        {
            StatisticsPeriod.Day => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            StatisticsPeriod.Week => $"{ISOWeek.GetYear(date.ToDateTime(TimeOnly.MinValue)):0000}-W{ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue)):00}",
            StatisticsPeriod.Month => date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            StatisticsPeriod.Year => date.ToString("yyyy", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, null)
        };
    }
}

public sealed record ProjectTimeSummary(string Period, int Minutes);

public sealed record PeriodSummary(string Period, int Minutes);

public enum StatisticsPeriod
{
    Day,
    Week,
    Month,
    Year
}
