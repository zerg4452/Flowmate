using PersonalTaskManager.Core.Models;
using PersonalTaskManager.Core.Services;

namespace PersonalTaskManager.Tests;

public class DomainBehaviorTests
{
    [Fact]
    public void New_project_receives_default_statuses_in_order()
    {
        var project = TaskManagerDomain.CreateProject("KAP 유지보수");

        Assert.Equal("KAP 유지보수", project.Name);
        Assert.True(project.IsActive);
        Assert.Equal(
            new[] { "To do", "in Progress [ing]", "in Progress [Test]", "Done" },
            project.Statuses.Select(status => status.Name));
    }

    [Fact]
    public void Time_entries_accumulate_by_task_and_ignore_deleted_entries()
    {
        var project = TaskManagerDomain.CreateProject("개인화 업무관리 툴");
        var task = TaskManagerDomain.CreateTask(project, "통계 서비스 구현");

        TaskManagerDomain.AddTimeEntry(task, new DateOnly(2026, 6, 23), 90, "초안");
        var deletedEntry = TaskManagerDomain.AddTimeEntry(task, new DateOnly(2026, 6, 23), 30, "삭제 예정");
        TaskManagerDomain.AddTimeEntry(task, new DateOnly(2026, 6, 24), 45, "보강");

        TaskManagerDomain.SoftDelete(deletedEntry, DeleteOrigin.Direct);

        Assert.Equal(135, TaskManagerDomain.GetTaskTotalMinutes(task));
        Assert.Equal(90, TaskManagerDomain.GetTaskMinutesByDate(task, new DateOnly(2026, 6, 23)));
    }

    [Fact]
    public void Moving_task_to_done_sets_completed_at_and_history()
    {
        var project = TaskManagerDomain.CreateProject("메인 업무화면");
        var task = TaskManagerDomain.CreateTask(project, "아코디언 목록 구현");
        var doneStatus = project.Statuses.Single(status => status.Name == "Done");
        var changedAt = new DateTime(2026, 6, 24, 17, 30, 0);

        TaskManagerDomain.ChangeStatus(task, doneStatus, changedAt);

        Assert.Equal(doneStatus.Id, task.StatusId);
        Assert.Equal(changedAt, task.CompletedAt);
        Assert.Contains(task.Histories, history =>
            history.Type == HistoryType.StatusChanged &&
            history.NewValue == "Done" &&
            !history.IsDeleted);
    }

    [Fact]
    public void Moving_done_task_back_to_other_status_clears_completed_at()
    {
        var project = TaskManagerDomain.CreateProject("프로젝트 관리");
        var task = TaskManagerDomain.CreateTask(project, "상태 편집 구현");
        var doneStatus = project.Statuses.Single(status => status.Name == "Done");
        var todoStatus = project.Statuses.Single(status => status.Name == "To do");

        TaskManagerDomain.ChangeStatus(task, doneStatus, new DateTime(2026, 6, 24, 17, 30, 0));
        TaskManagerDomain.ChangeStatus(task, todoStatus, new DateTime(2026, 6, 24, 18, 0, 0));

        Assert.Equal(todoStatus.Id, task.StatusId);
        Assert.Null(task.CompletedAt);
    }

    [Fact]
    public void Deleting_project_soft_deletes_child_records_and_excludes_statistics()
    {
        var project = TaskManagerDomain.CreateProject("삭제 테스트");
        var task = TaskManagerDomain.CreateTask(project, "전파 삭제 검증");
        TaskManagerDomain.AddComment(task, "삭제 전 댓글", new DateTime(2026, 6, 24, 9, 0, 0));
        TaskManagerDomain.AddTimeEntry(task, new DateOnly(2026, 6, 24), 120, "구현");

        TaskManagerDomain.SoftDelete(project, DeleteOrigin.Direct);

        Assert.True(project.IsDeleted);
        Assert.All(project.Statuses, status => Assert.True(status.IsDeleted));
        Assert.True(task.IsDeleted);
        Assert.All(task.Comments, comment => Assert.True(comment.IsDeleted));
        Assert.All(task.TimeEntries, entry => Assert.True(entry.IsDeleted));
        Assert.Equal(0, StatisticsService.GetProjectTotalMinutes(project));
    }

    [Fact]
    public void Editing_comment_updates_content_and_modified_time()
    {
        var project = TaskManagerDomain.CreateProject("댓글 수정");
        var task = TaskManagerDomain.CreateTask(project, "댓글 인라인 수정");
        var createdAt = new DateTime(2026, 6, 24, 9, 0, 0);
        var updatedAt = new DateTime(2026, 6, 24, 10, 0, 0);
        var comment = TaskManagerDomain.AddComment(task, "초기 댓글", createdAt);

        TaskManagerDomain.EditComment(comment, "수정된 댓글", updatedAt);

        Assert.Equal("수정된 댓글", comment.Content);
        Assert.Equal(createdAt, comment.CreatedAt);
        Assert.Equal(updatedAt, comment.UpdatedAt);
    }

    [Fact]
    public void Editing_history_updates_message_and_modified_time()
    {
        var history = new TaskHistory
        {
            TaskId = Guid.NewGuid(),
            Type = HistoryType.StatusChanged,
            Message = "기존 히스토리",
            CreatedAt = new DateTime(2026, 6, 24, 9, 0, 0)
        };
        var updatedAt = new DateTime(2026, 6, 24, 10, 0, 0);

        TaskManagerDomain.EditHistory(history, "수정된 히스토리", updatedAt);

        Assert.Equal("수정된 히스토리", history.Message);
        Assert.Equal(updatedAt, history.UpdatedAt);
    }
}
