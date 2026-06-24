using PersonalTaskManager.Core.Models;
using PersonalTaskManager.Core.Persistence;
using PersonalTaskManager.Core.Services;

namespace PersonalTaskManager.Tests;

public class PersistenceAndStatisticsTests
{
    [Fact]
    public void LiteDb_store_initializes_schema_version_and_default_options()
    {
        using var temp = TempDatabase.Create();
        using var store = new LiteDbTaskStore(temp.Path);

        store.Initialize();

        Assert.Equal(1, store.GetSchemaVersion());
        var options = store.GetOptions();
        Assert.True(options.IsReminderEnabled);
        Assert.Equal(new TimeOnly(17, 30), options.ReminderTime);
        Assert.Equal("Malgun Gothic", options.FontFamily);
    }

    [Fact]
    public void LiteDb_store_persists_project_statuses_tasks_and_time_entries()
    {
        using var temp = TempDatabase.Create();
        var project = TaskManagerDomain.CreateProject("LiteDB 검증");
        var task = TaskManagerDomain.CreateTask(project, "저장 후 재조회");
        TaskManagerDomain.AddTimeEntry(task, new DateOnly(2026, 6, 24), 75, "저장 테스트");

        using (var store = new LiteDbTaskStore(temp.Path))
        {
            store.Initialize();
            store.UpsertProject(project);
        }

        using (var store = new LiteDbTaskStore(temp.Path))
        {
            var saved = Assert.Single(store.GetProjects(includeDeleted: false));
            Assert.Equal("LiteDB 검증", saved.Name);
            Assert.Equal(4, saved.Statuses.Count);
            var savedTask = Assert.Single(saved.Tasks);
            Assert.Equal(75, TaskManagerDomain.GetTaskTotalMinutes(savedTask));
        }
    }

    [Fact]
    public void Statistics_group_minutes_by_day_week_month_and_year()
    {
        var project = TaskManagerDomain.CreateProject("통계");
        var task = TaskManagerDomain.CreateTask(project, "기간 집계");
        TaskManagerDomain.AddTimeEntry(task, new DateOnly(2026, 6, 23), 60, "화요일");
        TaskManagerDomain.AddTimeEntry(task, new DateOnly(2026, 6, 24), 90, "수요일");
        TaskManagerDomain.AddTimeEntry(task, new DateOnly(2026, 7, 1), 30, "다음 달");

        Assert.Equal(
            new[] { new PeriodSummary("2026-06-23", 60), new PeriodSummary("2026-06-24", 90), new PeriodSummary("2026-07-01", 30) },
            StatisticsService.GetProjectMinutes(project, StatisticsPeriod.Day));
        Assert.Equal(
            new[] { new PeriodSummary("2026-W26", 150), new PeriodSummary("2026-W27", 30) },
            StatisticsService.GetProjectMinutes(project, StatisticsPeriod.Week));
        Assert.Equal(
            new[] { new PeriodSummary("2026-06", 150), new PeriodSummary("2026-07", 30) },
            StatisticsService.GetProjectMinutes(project, StatisticsPeriod.Month));
        Assert.Equal(
            new[] { new PeriodSummary("2026", 180) },
            StatisticsService.GetProjectMinutes(project, StatisticsPeriod.Year));
    }

    private sealed class TempDatabase : IDisposable
    {
        private TempDatabase(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDatabase Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
            return new TempDatabase(path);
        }

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}
