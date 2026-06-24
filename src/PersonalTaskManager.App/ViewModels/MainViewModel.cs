using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using PersonalTaskManager.App.Infrastructure;
using PersonalTaskManager.Core.Models;
using PersonalTaskManager.Core.Persistence;
using PersonalTaskManager.Core.Services;

namespace PersonalTaskManager.App.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly LiteDbTaskStore store;
    private readonly DispatcherTimer reminderTimer;
    private readonly DispatcherTimer keywordTimer;
    private AppOptions options = new();
    private string currentView = "Main";
    private string newProjectName = string.Empty;
    private string newTaskTitle = string.Empty;
    private string newTaskBody = string.Empty;
    private byte[]? newTaskBodyDocument;
    private string newComment = string.Empty;
    private string editingTaskBody = string.Empty;
    private byte[]? editingTaskBodyDocument;
    private string editingCommentText = string.Empty;
    private string editingHistoryText = string.Empty;
    private string timeMemo = string.Empty;
    private string keyword = string.Empty;
    private int timeMinutes = 30;
    private DateTime? filterStartDate;
    private DateTime? filterEndDate;
    private DateTime? timeEntryDate = DateTime.Today;
    private Project? selectedProjectFilter;
    private Project? selectedProject;
    private WorkTask? selectedTask;
    private ProjectStatus? selectedTaskStatus;
    private TrashItemView? selectedTrashItem;
    private TaskComment? editingComment;
    private TaskHistory? editingHistory;
    private bool isTaskDetailOpen;
    private bool isBodyEditMode;
    private string reminderMessage = string.Empty;
    private DateOnly? reminderAcknowledgedDate;

    public MainViewModel()
        : this(new LiteDbTaskStore(AppDataPaths.DatabasePath))
    {
    }

    public MainViewModel(LiteDbTaskStore store)
    {
        this.store = store;
        this.store.Initialize();
        options = this.store.GetOptions();

        SelectViewCommand = new RelayCommand<string>(view => CurrentView = view ?? "Main");
        AddProjectCommand = new RelayCommand(AddProject);
        ToggleProjectActiveCommand = new RelayCommand(ToggleProjectActive, () => SelectedProject is not null);
        AddTaskCommand = new RelayCommand(AddTask, () => SelectedProject is not null);
        OpenTaskDetailCommand = new RelayCommand<WorkTask>(OpenTaskDetail, task => task is not null);
        CloseTaskDetailCommand = new RelayCommand(CloseTaskDetail);
        StartEditBodyCommand = new RelayCommand(StartEditBody, () => SelectedTask is not null && !IsBodyEditMode);
        CancelEditBodyCommand = new RelayCommand(CancelEditBody);
        SaveTaskBodyCommand = new RelayCommand(SaveTaskBody, () => SelectedTask is not null);
        ChangeTaskStatusCommand = new RelayCommand(ChangeTaskStatus, () => SelectedTask is not null && SelectedTaskStatus is not null);
        AddCommentCommand = new RelayCommand(AddComment, () => SelectedTask is not null);
        StartEditCommentCommand = new RelayCommand<TaskComment>(StartEditComment, comment => comment is not null);
        SaveCommentEditCommand = new RelayCommand(SaveCommentEdit, () => EditingComment is not null);
        CancelCommentEditCommand = new RelayCommand(CancelCommentEdit);
        DeleteCommentCommand = new RelayCommand<TaskComment>(DeleteComment, comment => comment is not null);
        StartEditHistoryCommand = new RelayCommand<TaskHistory>(StartEditHistory, history => history is not null);
        SaveHistoryEditCommand = new RelayCommand(SaveHistoryEdit, () => EditingHistory is not null);
        CancelHistoryEditCommand = new RelayCommand(CancelHistoryEdit);
        DeleteHistoryCommand = new RelayCommand<TaskHistory>(DeleteHistory, history => history is not null);
        AddTimeEntryCommand = new RelayCommand(AddTimeEntry, () => SelectedTask is not null);
        DeleteProjectCommand = new RelayCommand(DeleteProject, () => SelectedProject is not null);
        DeleteTaskCommand = new RelayCommand(DeleteTask, () => SelectedTask is not null);
        SaveOptionsCommand = new RelayCommand(SaveOptions);
        ApplyFiltersCommand = new RelayCommand(RefreshBoardColumns);
        ClearReminderCommand = new RelayCommand(() => ReminderMessage = string.Empty);
        RestoreTrashItemCommand = new RelayCommand(RestoreTrashItem, () => SelectedTrashItem is not null);
        PermanentlyDeleteTrashItemCommand = new RelayCommand(PermanentlyDeleteTrashItem, () => SelectedTrashItem is not null);

        Load();
        reminderTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        reminderTimer.Tick += (_, _) => CheckReminder();
        reminderTimer.Start();
        CheckReminder();

        keywordTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        keywordTimer.Tick += (_, _) =>
        {
            keywordTimer.Stop();
            RefreshBoardColumns();
        };
    }

    public ObservableCollection<Project> Projects { get; } = [];

    public ObservableCollection<BoardColumn> BoardColumns { get; } = [];

    public ObservableCollection<ProjectStatus> SelectedProjectStatuses { get; } = [];

    public ObservableCollection<PeriodSummary> DailyStatistics { get; } = [];

    public ObservableCollection<PeriodSummary> WeeklyStatistics { get; } = [];

    public ObservableCollection<PeriodSummary> MonthlyStatistics { get; } = [];

    public ObservableCollection<PeriodSummary> YearlyStatistics { get; } = [];

    public ObservableCollection<TrashItemView> TrashItems { get; } = [];

    public ObservableCollection<TaskComment> SelectedTaskComments { get; } = [];

    public ObservableCollection<TaskHistory> SelectedTaskHistories { get; } = [];

    public IReadOnlyList<string> MenuItems { get; } = ["Main", "Projects", "Statistics", "Options"];

    public RelayCommand<string> SelectViewCommand { get; }

    public RelayCommand AddProjectCommand { get; }

    public RelayCommand ToggleProjectActiveCommand { get; }

    public RelayCommand AddTaskCommand { get; }

    public RelayCommand<WorkTask> OpenTaskDetailCommand { get; }

    public RelayCommand CloseTaskDetailCommand { get; }

    public RelayCommand StartEditBodyCommand { get; }

    public RelayCommand CancelEditBodyCommand { get; }

    public RelayCommand SaveTaskBodyCommand { get; }

    public RelayCommand ChangeTaskStatusCommand { get; }

    public RelayCommand AddCommentCommand { get; }

    public RelayCommand<TaskComment> StartEditCommentCommand { get; }

    public RelayCommand SaveCommentEditCommand { get; }

    public RelayCommand CancelCommentEditCommand { get; }

    public RelayCommand<TaskComment> DeleteCommentCommand { get; }

    public RelayCommand<TaskHistory> StartEditHistoryCommand { get; }

    public RelayCommand SaveHistoryEditCommand { get; }

    public RelayCommand CancelHistoryEditCommand { get; }

    public RelayCommand<TaskHistory> DeleteHistoryCommand { get; }

    public RelayCommand AddTimeEntryCommand { get; }

    public RelayCommand DeleteProjectCommand { get; }

    public RelayCommand DeleteTaskCommand { get; }

    public RelayCommand SaveOptionsCommand { get; }

    public RelayCommand ApplyFiltersCommand { get; }

    public RelayCommand ClearReminderCommand { get; }

    public RelayCommand RestoreTrashItemCommand { get; }

    public RelayCommand PermanentlyDeleteTrashItemCommand { get; }

    public string CurrentView
    {
        get => currentView;
        set => SetProperty(ref currentView, value);
    }

    public string NewProjectName
    {
        get => newProjectName;
        set => SetProperty(ref newProjectName, value);
    }

    public string NewTaskTitle
    {
        get => newTaskTitle;
        set => SetProperty(ref newTaskTitle, value);
    }

    public string NewTaskBody
    {
        get => newTaskBody;
        set => SetProperty(ref newTaskBody, value);
    }

    public byte[]? NewTaskBodyDocument
    {
        get => newTaskBodyDocument;
        set => SetProperty(ref newTaskBodyDocument, value);
    }

    public string NewComment
    {
        get => newComment;
        set => SetProperty(ref newComment, value);
    }

    public string EditingTaskBody
    {
        get => editingTaskBody;
        set => SetProperty(ref editingTaskBody, value);
    }

    public byte[]? EditingTaskBodyDocument
    {
        get => editingTaskBodyDocument;
        set => SetProperty(ref editingTaskBodyDocument, value);
    }

    public string EditingCommentText
    {
        get => editingCommentText;
        set => SetProperty(ref editingCommentText, value);
    }

    public string EditingHistoryText
    {
        get => editingHistoryText;
        set => SetProperty(ref editingHistoryText, value);
    }

    public string TimeMemo
    {
        get => timeMemo;
        set => SetProperty(ref timeMemo, value);
    }

    public string Keyword
    {
        get => keyword;
        set
        {
            if (SetProperty(ref keyword, value))
            {
                keywordTimer.Stop();
                keywordTimer.Start();
            }
        }
    }

    public int TimeMinutes
    {
        get => timeMinutes;
        set => SetProperty(ref timeMinutes, Math.Max(1, value));
    }

    public DateTime? FilterStartDate
    {
        get => filterStartDate;
        set => SetProperty(ref filterStartDate, value);
    }

    public DateTime? FilterEndDate
    {
        get => filterEndDate;
        set => SetProperty(ref filterEndDate, value);
    }

    public DateTime? TimeEntryDate
    {
        get => timeEntryDate;
        set => SetProperty(ref timeEntryDate, value);
    }

    public Project? SelectedProjectFilter
    {
        get => selectedProjectFilter;
        set
        {
            if (SetProperty(ref selectedProjectFilter, value))
            {
                RefreshBoardColumns();
            }
        }
    }

    public Project? SelectedProject
    {
        get => selectedProject;
        set
        {
            if (SetProperty(ref selectedProject, value))
            {
                RefreshSelectedProjectStatuses();
                RaiseCommandStates();
            }
        }
    }

    public WorkTask? SelectedTask
    {
        get => selectedTask;
        set
        {
            if (SetProperty(ref selectedTask, value))
            {
                SelectedTaskStatus = ResolveSelectedTaskStatus();
                RaiseCommandStates();
                OnPropertyChanged(nameof(SelectedTaskTotalText));
            }
        }
    }

    public ProjectStatus? SelectedTaskStatus
    {
        get => selectedTaskStatus;
        set
        {
            if (SetProperty(ref selectedTaskStatus, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public TrashItemView? SelectedTrashItem
    {
        get => selectedTrashItem;
        set
        {
            if (SetProperty(ref selectedTrashItem, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public TaskComment? EditingComment
    {
        get => editingComment;
        set
        {
            if (SetProperty(ref editingComment, value))
            {
                OnPropertyChanged(nameof(EditingCommentId));
                SaveCommentEditCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public Guid? EditingCommentId => EditingComment?.Id;

    public TaskHistory? EditingHistory
    {
        get => editingHistory;
        set
        {
            if (SetProperty(ref editingHistory, value))
            {
                OnPropertyChanged(nameof(EditingHistoryId));
                SaveHistoryEditCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public Guid? EditingHistoryId => EditingHistory?.Id;

    public bool IsTaskDetailOpen
    {
        get => isTaskDetailOpen;
        set => SetProperty(ref isTaskDetailOpen, value);
    }

    public bool IsBodyEditMode
    {
        get => isBodyEditMode;
        set
        {
            if (SetProperty(ref isBodyEditMode, value))
            {
                StartEditBodyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public double WindowWidth
    {
        get => options.WindowWidth;
        set
        {
            if (value > 0)
            {
                options.WindowWidth = value;
            }
        }
    }

    public double WindowHeight
    {
        get => options.WindowHeight;
        set
        {
            if (value > 0)
            {
                options.WindowHeight = value;
            }
        }
    }

    public double? WindowLeft
    {
        get => options.WindowLeft;
        set => options.WindowLeft = value;
    }

    public double? WindowTop
    {
        get => options.WindowTop;
        set => options.WindowTop = value;
    }

    public bool WindowMaximized
    {
        get => options.WindowMaximized;
        set => options.WindowMaximized = value;
    }

    public void PersistWindowState()
    {
        store.SaveOptions(options);
    }

    public void ApplyThemePreset(string foreground, string background)
    {
        ForegroundColor = foreground;
        BackgroundColor = background;
    }

    public string SelectedTaskTotalText =>
        SelectedTask is null ? "0분" : FormatMinutes(TaskManagerDomain.GetTaskTotalMinutes(SelectedTask));

    public bool IsReminderEnabled
    {
        get => options.IsReminderEnabled;
        set
        {
            options.IsReminderEnabled = value;
            OnPropertyChanged();
        }
    }

    public string ReminderTimeText
    {
        get => options.ReminderTime.ToString("HH:mm");
        set
        {
            if (TimeOnly.TryParse(value, out var parsed))
            {
                options.ReminderTime = parsed;
                OnPropertyChanged();
            }
        }
    }

    public string FontFamily
    {
        get => options.FontFamily;
        set
        {
            options.FontFamily = value;
            OnPropertyChanged();
        }
    }

    public double FontSize
    {
        get => options.FontSize;
        set
        {
            options.FontSize = Math.Clamp(value, 10, 24);
            OnPropertyChanged();
        }
    }

    public string ForegroundColor
    {
        get => options.ForegroundColor;
        set
        {
            options.ForegroundColor = value;
            OnPropertyChanged();
        }
    }

    public string BackgroundColor
    {
        get => options.BackgroundColor;
        set
        {
            options.BackgroundColor = value;
            OnPropertyChanged();
        }
    }

    public string ReminderMessage
    {
        get => reminderMessage;
        set => SetProperty(ref reminderMessage, value);
    }

    public string TotalProjectMinutesText => FormatMinutes(Projects.Sum(StatisticsService.GetProjectTotalMinutes));

    public void Dispose()
    {
        reminderTimer.Stop();
        keywordTimer.Stop();
        store.Dispose();
    }

    private void Load()
    {
        Projects.Clear();
        foreach (var project in store.GetProjects(includeDeleted: true))
        {
            TaskManagerDomain.EnsureFixedStatuses(project);
            store.UpsertProject(project);
            Projects.Add(project);
        }

        RefreshBoardColumns();
        RefreshStatistics();
        RefreshTrash();
    }

    private void AddProject()
    {
        if (string.IsNullOrWhiteSpace(NewProjectName))
        {
            return;
        }

        var project = TaskManagerDomain.CreateProject(NewProjectName);
        Projects.Add(project);
        store.UpsertProject(project);
        NewProjectName = string.Empty;
        SelectedProject = project;
        RefreshAllViews();
    }

    private void ToggleProjectActive()
    {
        if (SelectedProject is null)
        {
            return;
        }

        SelectedProject.IsActive = !SelectedProject.IsActive;
        SelectedProject.UpdatedAt = DateTime.Now;
        store.UpsertProject(SelectedProject);
        RefreshAllViews();
    }

    private void AddTask()
    {
        if (SelectedProject is null || string.IsNullOrWhiteSpace(NewTaskTitle))
        {
            return;
        }

        var task = TaskManagerDomain.CreateTask(SelectedProject, NewTaskTitle);
        task.Body = NewTaskBody;
        task.BodyDocument = NewTaskBodyDocument;
        store.UpsertProject(SelectedProject);
        NewTaskTitle = string.Empty;
        NewTaskBody = string.Empty;
        NewTaskBodyDocument = null;
        SelectedTask = task;
        RefreshAllViews();
    }

    private void OpenTaskDetail(WorkTask? task)
    {
        if (task is null)
        {
            return;
        }

        SelectedProject = Projects.FirstOrDefault(project => project.Id == task.ProjectId);
        SelectedTask = task;
        EditingTaskBody = task.Body;
        EditingTaskBodyDocument = task.BodyDocument;
        IsBodyEditMode = false;
        NewComment = string.Empty;
        TimeMemo = string.Empty;
        CancelCommentEdit();
        CancelHistoryEdit();
        RefreshSelectedTaskDetails();
        IsTaskDetailOpen = true;
    }

    private void StartEditBody()
    {
        if (SelectedTask is null)
        {
            return;
        }

        EditingTaskBody = SelectedTask.Body;
        EditingTaskBodyDocument = SelectedTask.BodyDocument;
        IsBodyEditMode = true;
    }

    private void CancelEditBody()
    {
        if (SelectedTask is not null)
        {
            EditingTaskBody = SelectedTask.Body;
            EditingTaskBodyDocument = SelectedTask.BodyDocument;
        }

        IsBodyEditMode = false;
    }

    private void CloseTaskDetail()
    {
        if (SelectedTask is not null && EditingTaskBody != SelectedTask.Body)
        {
            var answer = MessageBox.Show(
                "수정 중인 본문을 저장하시겠습니까?",
                "저장 확인",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (answer == MessageBoxResult.Cancel)
            {
                return;
            }

            if (answer == MessageBoxResult.Yes)
            {
                SaveTaskBody();
            }
            else
            {
                EditingTaskBody = SelectedTask.Body;
            }
        }

        IsBodyEditMode = false;
        IsTaskDetailOpen = false;
    }

    private void SaveTaskBody()
    {
        var project = FindProjectForSelectedTask();
        if (project is null || SelectedTask is null)
        {
            return;
        }

        SelectedTask.Body = EditingTaskBody;
        SelectedTask.BodyDocument = EditingTaskBodyDocument;
        SelectedTask.UpdatedAt = DateTime.Now;
        SelectedTask.Histories.Add(new TaskHistory
        {
            TaskId = SelectedTask.Id,
            Type = HistoryType.BodyEdited,
            Message = "Body edited",
            CreatedAt = DateTime.Now
        });
        store.UpsertProject(project);
        IsBodyEditMode = false;
        RefreshAllViews();
    }

    private void ChangeTaskStatus()
    {
        var project = FindProjectForSelectedTask();
        if (project is null || SelectedTask is null || SelectedTaskStatus is null)
        {
            return;
        }

        TaskManagerDomain.ChangeStatus(SelectedTask, SelectedTaskStatus, DateTime.Now);
        store.UpsertProject(project);
        RefreshAllViews();
    }

    private void AddComment()
    {
        var project = FindProjectForSelectedTask();
        if (project is null || SelectedTask is null || string.IsNullOrWhiteSpace(NewComment))
        {
            return;
        }

        TaskManagerDomain.AddComment(SelectedTask, NewComment.Trim(), DateTime.Now);
        store.UpsertProject(project);
        NewComment = string.Empty;
        RefreshAllViews();
    }

    private void StartEditComment(TaskComment? comment)
    {
        if (comment is null)
        {
            return;
        }

        EditingComment = comment;
        EditingCommentText = comment.Content;
    }

    private void SaveCommentEdit()
    {
        var project = FindProjectForSelectedTask();
        if (project is null || EditingComment is null)
        {
            return;
        }

        TaskManagerDomain.EditComment(EditingComment, EditingCommentText.Trim(), DateTime.Now);
        store.UpsertProject(project);
        CancelCommentEdit();
        RefreshAllViews();
    }

    private void CancelCommentEdit()
    {
        EditingComment = null;
        EditingCommentText = string.Empty;
    }

    private void DeleteComment(TaskComment? comment)
    {
        var project = FindProjectForSelectedTask();
        if (project is null || comment is null)
        {
            return;
        }

        var answer = MessageBox.Show(
            "이 댓글을 삭제하시겠습니까?\n삭제한 댓글은 옵션 > 휴지통에서 복구할 수 있습니다.",
            "댓글 삭제 확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        if (EditingComment?.Id == comment.Id)
        {
            CancelCommentEdit();
        }

        TaskManagerDomain.SoftDelete(comment, DeleteOrigin.Direct);
        store.UpsertProject(project);
        RefreshAllViews();
    }

    private void DeleteHistory(TaskHistory? history)
    {
        var project = FindProjectForSelectedTask();
        if (project is null || history is null)
        {
            return;
        }

        var answer = MessageBox.Show(
            "이 히스토리 항목을 삭제하시겠습니까?",
            "히스토리 삭제 확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        if (EditingHistory?.Id == history.Id)
        {
            CancelHistoryEdit();
        }

        TaskManagerDomain.SoftDelete(history, DeleteOrigin.Direct);
        store.UpsertProject(project);
        RefreshAllViews();
    }

    private void StartEditHistory(TaskHistory? history)
    {
        if (history is null)
        {
            return;
        }

        EditingHistory = history;
        EditingHistoryText = history.Message;
    }

    private void SaveHistoryEdit()
    {
        var project = FindProjectForSelectedTask();
        if (project is null || EditingHistory is null)
        {
            return;
        }

        TaskManagerDomain.EditHistory(EditingHistory, EditingHistoryText.Trim(), DateTime.Now);
        store.UpsertProject(project);
        CancelHistoryEdit();
        RefreshAllViews();
    }

    private void CancelHistoryEdit()
    {
        EditingHistory = null;
        EditingHistoryText = string.Empty;
    }

    private void AddTimeEntry()
    {
        var project = FindProjectForSelectedTask();
        if (project is null || SelectedTask is null || TimeEntryDate is null)
        {
            return;
        }

        TaskManagerDomain.AddTimeEntry(SelectedTask, DateOnly.FromDateTime(TimeEntryDate.Value), TimeMinutes, TimeMemo);
        store.UpsertProject(project);
        TimeMemo = string.Empty;
        OnPropertyChanged(nameof(SelectedTaskTotalText));
        RefreshAllViews();
    }

    public void MoveTaskToStatus(WorkTask task, string statusName)
    {
        var project = Projects.FirstOrDefault(project => project.Id == task.ProjectId);
        var status = project?.Statuses.FirstOrDefault(status => status.Name == statusName && !status.IsDeleted);
        if (project is null || status is null || task.StatusId == status.Id)
        {
            return;
        }

        TaskManagerDomain.ChangeStatus(task, status, DateTime.Now);
        store.UpsertProject(project);
        if (SelectedTask?.Id == task.Id)
        {
            SelectedTaskStatus = status;
        }

        RefreshAllViews();
    }

    private void DeleteProject()
    {
        if (SelectedProject is null)
        {
            return;
        }

        var minutes = StatisticsService.GetProjectTotalMinutes(SelectedProject);
        var answer = MessageBox.Show(
            $"{SelectedProject.Name} 프로젝트를 삭제하시겠습니까?\n테스크 {SelectedProject.Tasks.Count}개, 누적시간 {FormatMinutes(minutes)}가 일반 조회에서 제외됩니다.",
            "프로젝트 삭제 확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        TaskManagerDomain.SoftDelete(SelectedProject, DeleteOrigin.Direct);
        store.UpsertProject(SelectedProject);
        SelectedProject = null;
        SelectedTask = null;
        RefreshAllViews();
    }

    private void DeleteTask()
    {
        var project = FindProjectForSelectedTask();
        if (project is null || SelectedTask is null)
        {
            return;
        }

        var answer = MessageBox.Show(
            $"{SelectedTask.Title} 테스크를 삭제하시겠습니까?",
            "테스크 삭제 확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        TaskManagerDomain.SoftDelete(SelectedTask, DeleteOrigin.Direct);
        store.UpsertProject(project);
        SelectedTask = null;
        RefreshAllViews();
    }

    private void SaveOptions()
    {
        store.SaveOptions(options);
        CheckReminder();
    }

    private void RestoreTrashItem()
    {
        if (SelectedTrashItem is null)
        {
            return;
        }

        RestoreProjectTree(SelectedTrashItem.Project);
        store.UpsertProject(SelectedTrashItem.Project);
        RefreshAllViews();
    }

    private void PermanentlyDeleteTrashItem()
    {
        if (SelectedTrashItem is null)
        {
            return;
        }

        var answer = MessageBox.Show(
            $"{SelectedTrashItem.Name} 항목을 영구 삭제하시겠습니까? 이 작업은 되돌릴 수 없습니다.",
            "영구 삭제 확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        if (SelectedTrashItem.Task is not null)
        {
            SelectedTrashItem.Project.Tasks.Remove(SelectedTrashItem.Task);
            store.UpsertProject(SelectedTrashItem.Project);
        }
        else if (SelectedTrashItem.Status is not null)
        {
            SelectedTrashItem.Project.Statuses.Remove(SelectedTrashItem.Status);
            store.UpsertProject(SelectedTrashItem.Project);
        }
        else if (SelectedTrashItem.Comment is not null)
        {
            var owner = SelectedTrashItem.Project.Tasks.FirstOrDefault(task => task.Id == SelectedTrashItem.Comment.TaskId);
            owner?.Comments.Remove(SelectedTrashItem.Comment);
            store.UpsertProject(SelectedTrashItem.Project);
        }
        else
        {
            store.DeleteProjectPermanently(SelectedTrashItem.Project.Id);
            Projects.Remove(SelectedTrashItem.Project);
        }

        SelectedTrashItem = null;
        RefreshAllViews();
    }

    private void RefreshAllViews()
    {
        RefreshSelectedProjectStatuses();
        RefreshSelectedTaskDetails();
        RefreshBoardColumns();
        RefreshStatistics();
        RefreshTrash();
        OnPropertyChanged(nameof(TotalProjectMinutesText));
        OnPropertyChanged(nameof(SelectedTaskTotalText));
    }

    private void RefreshSelectedTaskDetails()
    {
        SelectedTaskComments.Clear();
        SelectedTaskHistories.Clear();
        if (SelectedTask is null)
        {
            return;
        }

        foreach (var comment in SelectedTask.Comments
                     .Where(comment => !comment.IsDeleted)
                     .OrderBy(comment => comment.CreatedAt))
        {
            SelectedTaskComments.Add(comment);
        }

        foreach (var history in SelectedTask.Histories
                     .Where(history => !history.IsDeleted)
                     .OrderByDescending(history => history.CreatedAt))
        {
            SelectedTaskHistories.Add(history);
        }
    }

    private void RefreshSelectedProjectStatuses()
    {
        SelectedProjectStatuses.Clear();
        var project = SelectedProject ?? FindProjectForSelectedTask();
        if (project is null)
        {
            return;
        }

        foreach (var status in project.Statuses.Where(status => !status.IsDeleted && status.IsActive).OrderBy(status => status.SortOrder))
        {
            SelectedProjectStatuses.Add(status);
        }
    }

    private void RefreshBoardColumns()
    {
        BoardColumns.Clear();
        DateOnly? start = FilterStartDate is null ? null : DateOnly.FromDateTime(FilterStartDate.Value);
        DateOnly? end = FilterEndDate is null ? null : DateOnly.FromDateTime(FilterEndDate.Value);

        foreach (var column in BoardLayoutService.BuildColumns(Projects, SelectedProjectFilter, start, end, Keyword))
        {
            BoardColumns.Add(column);
        }
    }

    private void RefreshStatistics()
    {
        DailyStatistics.Clear();
        WeeklyStatistics.Clear();
        MonthlyStatistics.Clear();
        YearlyStatistics.Clear();

        foreach (var summary in MergeStatistics(StatisticsPeriod.Day))
        {
            DailyStatistics.Add(summary);
        }

        foreach (var summary in MergeStatistics(StatisticsPeriod.Week))
        {
            WeeklyStatistics.Add(summary);
        }

        foreach (var summary in MergeStatistics(StatisticsPeriod.Month))
        {
            MonthlyStatistics.Add(summary);
        }

        foreach (var summary in MergeStatistics(StatisticsPeriod.Year))
        {
            YearlyStatistics.Add(summary);
        }
    }

    private IEnumerable<PeriodSummary> MergeStatistics(StatisticsPeriod period)
    {
        return Projects
            .Where(project => project.IsActive && !project.IsDeleted)
            .SelectMany(project => StatisticsService.GetProjectMinutes(project, period))
            .GroupBy(summary => summary.Period)
            .OrderBy(group => group.Key)
            .Select(group => new PeriodSummary(group.Key, group.Sum(summary => summary.Minutes)));
    }

    private void RefreshTrash()
    {
        TrashItems.Clear();
        foreach (var project in Projects)
        {
            if (project.IsDeleted)
            {
                TrashItems.Add(new TrashItemView { Kind = "프로젝트", Name = project.Name, DeletedAt = project.DeletedAt, Project = project });
            }

            foreach (var status in project.Statuses.Where(status => status.IsDeleted))
            {
                TrashItems.Add(new TrashItemView { Kind = "상태", Name = status.Name, DeletedAt = status.DeletedAt, Project = project, Status = status });
            }

            foreach (var task in project.Tasks)
            {
                if (task.IsDeleted)
                {
                    TrashItems.Add(new TrashItemView { Kind = "테스크", Name = task.Title, DeletedAt = task.DeletedAt, Project = project, Task = task });
                }

                foreach (var comment in task.Comments.Where(comment => comment.IsDeleted))
                {
                    TrashItems.Add(new TrashItemView { Kind = "댓글", Name = comment.Content, DeletedAt = comment.DeletedAt, Project = project, Comment = comment });
                }
            }
        }
    }

    private ProjectStatus? ResolveSelectedTaskStatus()
    {
        var project = FindProjectForSelectedTask();
        RefreshSelectedProjectStatuses();
        return project?.Statuses.FirstOrDefault(status => status.Id == SelectedTask?.StatusId);
    }

    private Project? FindProjectForSelectedTask()
    {
        if (SelectedTask is null)
        {
            return null;
        }

        return Projects.FirstOrDefault(project => project.Id == SelectedTask.ProjectId);
    }

    private void RestoreProjectTree(Project project)
    {
        project.IsDeleted = false;
        project.DeletedAt = null;
        project.DeleteOrigin = null;

        foreach (var status in project.Statuses.Where(status => status.DeleteOrigin == DeleteOrigin.Cascade))
        {
            status.IsDeleted = false;
            status.DeletedAt = null;
            status.DeleteOrigin = null;
        }

        foreach (var task in project.Tasks.Where(task => task.DeleteOrigin == DeleteOrigin.Cascade || task.Id != Guid.Empty))
        {
            task.IsDeleted = false;
            task.DeletedAt = null;
            task.DeleteOrigin = null;
            foreach (var comment in task.Comments.Where(comment => comment.DeleteOrigin == DeleteOrigin.Cascade))
            {
                comment.IsDeleted = false;
                comment.DeletedAt = null;
                comment.DeleteOrigin = null;
            }

            foreach (var history in task.Histories.Where(history => history.DeleteOrigin == DeleteOrigin.Cascade))
            {
                history.IsDeleted = false;
                history.DeletedAt = null;
                history.DeleteOrigin = null;
            }

            foreach (var entry in task.TimeEntries.Where(entry => entry.DeleteOrigin == DeleteOrigin.Cascade))
            {
                entry.IsDeleted = false;
                entry.DeletedAt = null;
                entry.DeleteOrigin = null;
            }
        }
    }

    private void CheckReminder()
    {
        if (!options.IsReminderEnabled)
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (reminderAcknowledgedDate == today || TimeOnly.FromDateTime(DateTime.Now) < options.ReminderTime)
        {
            return;
        }

        var hasTodayEntry = Projects
            .Where(project => !project.IsDeleted)
            .SelectMany(project => project.Tasks)
            .Where(task => !task.IsDeleted)
            .SelectMany(task => task.TimeEntries)
            .Any(entry => !entry.IsDeleted && entry.WorkDate == today);

        if (!hasTodayEntry)
        {
            ReminderMessage = "오늘 입력된 작업시간이 없습니다. 퇴근 전 업무시간을 기록해 주세요.";
            reminderAcknowledgedDate = today;
        }
    }

    private void RaiseCommandStates()
    {
        ToggleProjectActiveCommand.RaiseCanExecuteChanged();
        AddTaskCommand.RaiseCanExecuteChanged();
        OpenTaskDetailCommand.RaiseCanExecuteChanged();
        CloseTaskDetailCommand.RaiseCanExecuteChanged();
        StartEditBodyCommand.RaiseCanExecuteChanged();
        SaveTaskBodyCommand.RaiseCanExecuteChanged();
        ChangeTaskStatusCommand.RaiseCanExecuteChanged();
        AddCommentCommand.RaiseCanExecuteChanged();
        StartEditCommentCommand.RaiseCanExecuteChanged();
        SaveCommentEditCommand.RaiseCanExecuteChanged();
        CancelCommentEditCommand.RaiseCanExecuteChanged();
        StartEditHistoryCommand.RaiseCanExecuteChanged();
        SaveHistoryEditCommand.RaiseCanExecuteChanged();
        CancelHistoryEditCommand.RaiseCanExecuteChanged();
        AddTimeEntryCommand.RaiseCanExecuteChanged();
        DeleteProjectCommand.RaiseCanExecuteChanged();
        DeleteTaskCommand.RaiseCanExecuteChanged();
        RestoreTrashItemCommand.RaiseCanExecuteChanged();
        PermanentlyDeleteTrashItemCommand.RaiseCanExecuteChanged();
    }

    private static string FormatMinutes(int minutes)
    {
        var hours = minutes / 60;
        var rest = minutes % 60;
        return hours == 0 ? $"{rest}분" : $"{hours}시간 {rest}분";
    }
}
