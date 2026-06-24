using PersonalTaskManager.Core.Models;
using PersonalTaskManager.Core.Services;

namespace PersonalTaskManager.Tests;

public class BoardLayoutTests
{
    [Fact]
    public void Fixed_statuses_replace_custom_statuses_in_order()
    {
        var project = TaskManagerDomain.CreateProject("상태 고정");
        project.Statuses.Add(new ProjectStatus
        {
            ProjectId = project.Id,
            Name = "Custom Review",
            SortOrder = 99
        });

        TaskManagerDomain.EnsureFixedStatuses(project);

        Assert.Equal(TaskManagerDomain.FixedStatusNames, project.Statuses.Select(status => status.Name));
        Assert.All(project.Statuses, status => Assert.True(status.IsDefault));
        Assert.All(project.Statuses, status => Assert.True(status.IsActive));
        Assert.All(project.Statuses, status => Assert.False(status.IsDeleted));
    }

    [Fact]
    public void Board_columns_group_tasks_by_status_then_project_when_project_filter_is_empty()
    {
        var alpha = TaskManagerDomain.CreateProject("프로젝트1");
        var beta = TaskManagerDomain.CreateProject("프로젝트2");
        var alphaTodo = TaskManagerDomain.CreateTask(alpha, "알파 대기");
        var alphaDone = TaskManagerDomain.CreateTask(alpha, "알파 완료");
        var betaTodo = TaskManagerDomain.CreateTask(beta, "베타 대기");

        TaskManagerDomain.ChangeStatus(alphaDone, alpha.Statuses.Single(status => status.Name == "Done"), DateTime.Now);

        var board = BoardLayoutService.BuildColumns([alpha, beta], null, null, null, string.Empty);

        var todo = board.Single(column => column.StatusName == "To do");
        Assert.Equal(new[] { "프로젝트1", "프로젝트2" }, todo.ProjectGroups.Select(group => group.ProjectName));
        Assert.Equal(new[] { alphaTodo.Id }, todo.ProjectGroups[0].Tasks.Select(task => task.Id));
        Assert.Equal(new[] { betaTodo.Id }, todo.ProjectGroups[1].Tasks.Select(task => task.Id));

        var done = board.Single(column => column.StatusName == "Done");
        var doneGroup = Assert.Single(done.ProjectGroups);
        Assert.Equal("프로젝트1", doneGroup.ProjectName);
        Assert.Equal(new[] { alphaDone.Id }, doneGroup.Tasks.Select(task => task.Id));
    }

    [Fact]
    public void Board_columns_apply_project_date_and_keyword_filters()
    {
        var alpha = TaskManagerDomain.CreateProject("프로젝트1");
        var beta = TaskManagerDomain.CreateProject("프로젝트2");
        TaskManagerDomain.CreateTask(alpha, "검색 대상", new DateTime(2026, 6, 24, 9, 0, 0));
        TaskManagerDomain.CreateTask(alpha, "날짜 제외", new DateTime(2026, 6, 20, 9, 0, 0));
        TaskManagerDomain.CreateTask(beta, "검색 대상", new DateTime(2026, 6, 24, 9, 0, 0));

        var board = BoardLayoutService.BuildColumns(
            [alpha, beta],
            alpha,
            new DateOnly(2026, 6, 24),
            new DateOnly(2026, 6, 24),
            "검색");

        var todo = board.Single(column => column.StatusName == "To do");
        var group = Assert.Single(todo.ProjectGroups);
        var task = Assert.Single(group.Tasks);

        Assert.Equal("프로젝트1", group.ProjectName);
        Assert.Equal("검색 대상", task.Title);
    }
}
