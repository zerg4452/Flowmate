using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
    private string editingTimelineText = string.Empty;
    private string timeMemo = string.Empty;
    private string mattyLink = string.Empty;
    private string newTaskMattyLink = string.Empty;
    private string keyword = string.Empty;
    private int timeMinutes = 30;
    private bool logTimeEntry;
    private DateTime? filterStartDate;
    private DateTime? filterEndDate;
    private DateTime? timeEntryDate = DateTime.Today;
    private Project? selectedProjectFilter;
    private Project? selectedProject;
    private WorkTask? selectedTask;
    private ProjectStatus? selectedTaskStatus;
    private TrashItemView? selectedTrashItem;
    private TaskTimelineItem? editingTimelineItem;
    private ProjectDocument? selectedDocument;
    private string editingDocumentTitle = string.Empty;
    private string editingDocumentBody = string.Empty;
    private byte[]? editingDocumentBodyDocument;
    private bool isTaskDetailOpen;
    private bool isBodyEditMode;
    private bool isAddTaskOpen;
    private bool isDocumentEditMode;
    private string reminderMessage = string.Empty;
    private DateOnly? reminderAcknowledgedDate;
    private string projectWorkspacePath = string.Empty;
    private AiCliTool? projectAiTool;
    private bool isAiPanelActivated;
    private string aiPrompt = string.Empty;

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
        AddTaskCommand = new RelayCommand(AddTask, () => SelectedProjectFilter is not null);
        OpenAddTaskCommand = new RelayCommand(OpenAddTask, () => SelectedProjectFilter is not null);
        CloseAddTaskCommand = new RelayCommand(() => IsAddTaskOpen = false);
        AddDocumentCommand = new RelayCommand(AddDocument, () => SelectedProject is not null);
        StartEditDocumentCommand = new RelayCommand(StartEditDocument, () => SelectedDocument is not null && !IsDocumentEditMode);
        SaveDocumentCommand = new RelayCommand(SaveDocument, () => SelectedDocument is not null);
        CancelEditDocumentCommand = new RelayCommand(CancelEditDocument);
        DeleteDocumentCommand = new RelayCommand(DeleteDocument, () => SelectedDocument is not null);
        OpenTaskDetailCommand = new RelayCommand<WorkTask>(OpenTaskDetail, task => task is not null);
        CloseTaskDetailCommand = new RelayCommand(CloseTaskDetail);
        StartEditBodyCommand = new RelayCommand(StartEditBody, () => SelectedTask is not null && !IsBodyEditMode);
        CancelEditBodyCommand = new RelayCommand(CancelEditBody);
        SaveTaskBodyCommand = new RelayCommand(SaveTaskBody, () => SelectedTask is not null);
        ChangeTaskStatusCommand = new RelayCommand(ChangeTaskStatus, () => SelectedTask is not null && SelectedTaskStatus is not null);
        AddEntryCommand = new RelayCommand(AddEntry, () => SelectedTask is not null);
        StartEditTimelineItemCommand = new RelayCommand<TaskTimelineItem>(StartEditTimelineItem, item => item?.Comment is not null || item?.History is not null);
        SaveTimelineItemEditCommand = new RelayCommand(SaveTimelineItemEdit, () => EditingTimelineItem is not null);
        CancelTimelineItemEditCommand = new RelayCommand(CancelTimelineItemEdit);
        DeleteTimelineItemCommand = new RelayCommand<TaskTimelineItem>(DeleteTimelineItem, item => item is not null);
        DeleteProjectCommand = new RelayCommand(DeleteProject, () => SelectedProject is not null);
        DeleteTaskCommand = new RelayCommand(DeleteTask, () => SelectedTask is not null);
        SaveOptionsCommand = new RelayCommand(SaveOptions);
        ApplyFiltersCommand = new RelayCommand(RefreshBoardColumns);
        ClearReminderCommand = new RelayCommand(() => ReminderMessage = string.Empty);
        RestoreTrashItemCommand = new RelayCommand(RestoreTrashItem, () => SelectedTrashItem is not null);
        PermanentlyDeleteTrashItemCommand = new RelayCommand(PermanentlyDeleteTrashItem, () => SelectedTrashItem is not null);
        SaveProjectAiSettingsCommand = new RelayCommand(SaveProjectAiSettings, () => SelectedProject is not null);
        ActivateAiPanelCommand = new RelayCommand(() => IsAiPanelActivated = true, () => IsAiWorkAvailable && !IsAiPanelActivated);
        RunAiPromptCommand = new RelayCommand(RunAiPrompt, () => IsAiWorkAvailable && !string.IsNullOrWhiteSpace(AiPrompt));

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

    public ObservableCollection<TaskTimelineItem> SelectedTaskTimeline { get; } = [];

    public ObservableCollection<ProjectStatGroup> ProjectTaskStatistics { get; } = [];

    public ObservableCollection<ProjectDocument> ProjectDocuments { get; } = [];

    public IReadOnlyList<string> MenuItems { get; } = ["Main", "Projects", "Statistics", "Options"];

    public RelayCommand<string> SelectViewCommand { get; }

    public RelayCommand AddProjectCommand { get; }

    public RelayCommand ToggleProjectActiveCommand { get; }

    public RelayCommand AddTaskCommand { get; }

    public RelayCommand OpenAddTaskCommand { get; }

    public RelayCommand CloseAddTaskCommand { get; }

    public RelayCommand AddDocumentCommand { get; }

    public RelayCommand StartEditDocumentCommand { get; }

    public RelayCommand SaveDocumentCommand { get; }

    public RelayCommand CancelEditDocumentCommand { get; }

    public RelayCommand DeleteDocumentCommand { get; }

    public RelayCommand<WorkTask> OpenTaskDetailCommand { get; }

    public RelayCommand CloseTaskDetailCommand { get; }

    public RelayCommand StartEditBodyCommand { get; }

    public RelayCommand CancelEditBodyCommand { get; }

    public RelayCommand SaveTaskBodyCommand { get; }

    public RelayCommand ChangeTaskStatusCommand { get; }

    public RelayCommand AddEntryCommand { get; }

    public RelayCommand<TaskTimelineItem> StartEditTimelineItemCommand { get; }

    public RelayCommand SaveTimelineItemEditCommand { get; }

    public RelayCommand CancelTimelineItemEditCommand { get; }

    public RelayCommand<TaskTimelineItem> DeleteTimelineItemCommand { get; }

    public RelayCommand DeleteProjectCommand { get; }

    public RelayCommand DeleteTaskCommand { get; }

    public RelayCommand SaveOptionsCommand { get; }

    public RelayCommand ApplyFiltersCommand { get; }

    public RelayCommand ClearReminderCommand { get; }

    public RelayCommand RestoreTrashItemCommand { get; }

    public RelayCommand PermanentlyDeleteTrashItemCommand { get; }

    public RelayCommand SaveProjectAiSettingsCommand { get; }

    public RelayCommand ActivateAiPanelCommand { get; }

    public RelayCommand RunAiPromptCommand { get; }

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

    public string EditingTimelineText
    {
        get => editingTimelineText;
        set => SetProperty(ref editingTimelineText, value);
    }

    public string TimeMemo
    {
        get => timeMemo;
        set => SetProperty(ref timeMemo, value);
    }

    public string MattyLink
    {
        get => mattyLink;
        set => SetProperty(ref mattyLink, value);
    }

    public string NewTaskMattyLink
    {
        get => newTaskMattyLink;
        set => SetProperty(ref newTaskMattyLink, value);
    }

    public static bool TryParseMattyTaskId(string? link, out string taskId)
    {
        taskId = string.Empty;
        if (string.IsNullOrWhiteSpace(link))
        {
            return false;
        }

        var match = System.Text.RegularExpressions.Regex.Match(link, @"/Task/Go/(\d+)");
        if (!match.Success)
        {
            match = System.Text.RegularExpressions.Regex.Match(link, @"(\d{4,})");
        }

        if (!match.Success)
        {
            return false;
        }

        taskId = match.Groups[1].Value;
        return true;
    }

    public void ApplyMattyComments(IReadOnlyList<string> comments)
    {
        var project = FindProjectForSelectedTask();
        if (project is null || SelectedTask is null)
        {
            return;
        }

        foreach (var comment in comments.Where(comment => !string.IsNullOrWhiteSpace(comment)))
        {
            TaskManagerDomain.AddComment(SelectedTask, comment.Trim(), DateTime.Now);
        }

        store.UpsertProject(project);
        RefreshAllViews();
    }

    public bool LogTimeEntry
    {
        get => logTimeEntry;
        set => SetProperty(ref logTimeEntry, value);
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
                AddTaskCommand.RaiseCanExecuteChanged();
                OpenAddTaskCommand.RaiseCanExecuteChanged();
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
                ProjectWorkspacePath = value?.WorkspacePath ?? string.Empty;
                ProjectAiTool = value?.AiCliTool;
                RefreshSelectedProjectStatuses();
                RefreshProjectDocuments();
                RaiseCommandStates();
            }
        }
    }

    public string ProjectWorkspacePath
    {
        get => projectWorkspacePath;
        set => SetProperty(ref projectWorkspacePath, value);
    }

    public AiCliTool? ProjectAiTool
    {
        get => projectAiTool;
        set => SetProperty(ref projectAiTool, value);
    }

    public bool IsAiPanelActivated
    {
        get => isAiPanelActivated;
        set
        {
            if (SetProperty(ref isAiPanelActivated, value))
            {
                ActivateAiPanelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string AiPrompt
    {
        get => aiPrompt;
        set
        {
            if (SetProperty(ref aiPrompt, value))
            {
                RunAiPromptCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsAiWorkAvailable
    {
        get
        {
            var project = FindProjectForSelectedTask();
            return project is not null &&
                   !string.IsNullOrWhiteSpace(project.WorkspacePath) &&
                   project.AiCliTool is not null;
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

    public TaskTimelineItem? EditingTimelineItem
    {
        get => editingTimelineItem;
        set
        {
            if (SetProperty(ref editingTimelineItem, value))
            {
                OnPropertyChanged(nameof(EditingTimelineItemId));
                SaveTimelineItemEditCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public Guid? EditingTimelineItemId => EditingTimelineItem?.Id;

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

    public bool IsAddTaskOpen
    {
        get => isAddTaskOpen;
        set => SetProperty(ref isAddTaskOpen, value);
    }

    public bool IsDocumentEditMode
    {
        get => isDocumentEditMode;
        set
        {
            if (SetProperty(ref isDocumentEditMode, value))
            {
                StartEditDocumentCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ProjectDocument? SelectedDocument
    {
        get => selectedDocument;
        set
        {
            if (SetProperty(ref selectedDocument, value))
            {
                IsDocumentEditMode = false;
                editingDocumentTitle = value?.Title ?? string.Empty;
                editingDocumentBody = value?.Body ?? string.Empty;
                editingDocumentBodyDocument = value?.BodyDocument;
                OnPropertyChanged(nameof(EditingDocumentTitle));
                OnPropertyChanged(nameof(EditingDocumentBody));
                StartEditDocumentCommand.RaiseCanExecuteChanged();
                SaveDocumentCommand.RaiseCanExecuteChanged();
                DeleteDocumentCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string EditingDocumentTitle
    {
        get => editingDocumentTitle;
        set => SetProperty(ref editingDocumentTitle, value);
    }

    public string EditingDocumentBody
    {
        get => editingDocumentBody;
        set => SetProperty(ref editingDocumentBody, value);
    }

    public byte[]? EditingDocumentBodyDocument
    {
        get => editingDocumentBodyDocument;
        set => SetProperty(ref editingDocumentBodyDocument, value);
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

    public string TotalProjectMinutesText => FormatMinutes(GetTodayMinutes());

    private int GetTodayMinutes()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return Projects
            .Where(project => !project.IsDeleted)
            .SelectMany(project => project.Tasks)
            .Where(task => !task.IsDeleted)
            .SelectMany(task => task.TimeEntries)
            .Where(entry => !entry.IsDeleted && entry.WorkDate == today)
            .Sum(entry => entry.Minutes);
    }

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

    private void OpenAddTask()
    {
        if (SelectedProjectFilter is null)
        {
            MessageBox.Show("먼저 상단 필터에서 프로젝트를 선택해 주세요.", "테스크 추가", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        NewTaskTitle = string.Empty;
        NewTaskBody = string.Empty;
        NewTaskBodyDocument = null;
        NewTaskMattyLink = string.Empty;
        IsAddTaskOpen = true;
    }

    private void SaveProjectAiSettings()
    {
        if (SelectedProject is null)
        {
            return;
        }

        SelectedProject.WorkspacePath = ProjectWorkspacePath.Trim();
        SelectedProject.AiCliTool = ProjectAiTool;
        SelectedProject.UpdatedAt = DateTime.Now;
        store.UpsertProject(SelectedProject);
        RaiseCommandStates();
    }

    private void RunAiPrompt()
    {
        var project = FindProjectForSelectedTask();
        if (project is null || SelectedTask is null || string.IsNullOrWhiteSpace(project.WorkspacePath))
        {
            return;
        }

        if (!Directory.Exists(project.WorkspacePath))
        {
            MessageBox.Show("워크스페이스 경로를 찾을 수 없습니다.", "AI 실행 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var promptDirectory = Path.Combine(project.WorkspacePath, ".flowmate");
        var promptPath = Path.Combine(promptDirectory, $"prompt-{SelectedTask.Id}.txt");

        try
        {
            Directory.CreateDirectory(promptDirectory);
            var workspacePath = Path.GetFullPath(project.WorkspacePath);
            var executionPrompt = BuildWorkspaceBoundPrompt(AiPrompt, workspacePath);
            File.WriteAllText(promptPath, executionPrompt);
            var promptPathLiteral = ToPowerShellSingleQuotedLiteral(promptPath);
            var workspacePathLiteral = ToPowerShellSingleQuotedLiteral(workspacePath);
            var cliPath = project.AiCliTool switch
            {
                AiCliTool.ClaudeCode => FindExecutableOnPath("claude.cmd", "claude.exe", "claude"),
                AiCliTool.Codex => FindExecutableOnPath("codex.cmd", "codex.exe", "codex"),
                _ => null
            };

            if (string.IsNullOrWhiteSpace(cliPath))
            {
                MessageBox.Show("선택한 AI 도구의 실행 파일을 찾을 수 없습니다. PATH 설정을 확인해주세요.", "AI 실행 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var cliPathLiteral = ToPowerShellSingleQuotedLiteral(cliPath);
            var command = project.AiCliTool switch
            {
                AiCliTool.ClaudeCode => $"$prompt = Get-Content -Raw -LiteralPath {promptPathLiteral}; & {cliPathLiteral} -p $prompt",
                AiCliTool.Codex => $"$OutputEncoding = [System.Text.UTF8Encoding]::new($false); [Console]::OutputEncoding = $OutputEncoding; Get-Content -Raw -Encoding UTF8 -LiteralPath {promptPathLiteral} | & {cliPathLiteral} exec --skip-git-repo-check --cd {workspacePathLiteral} -",
                _ => string.Empty
            };

            if (string.IsNullOrWhiteSpace(command))
            {
                MessageBox.Show("AI 도구를 선택해주세요.", "AI 실행 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Process.Start(new ProcessStartInfo("powershell.exe")
            {
                WorkingDirectory = project.WorkspacePath,
                Arguments = $"-NoExit -Command \"{command}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"AI를 실행할 수 없습니다.\n{ex.Message}", "AI 실행 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string ToPowerShellSingleQuotedLiteral(string value)
    {
        return $"'{value.Replace("'", "''")}'";
    }

    private static string BuildWorkspaceBoundPrompt(string prompt, string workspacePath)
    {
        return $"""
작업 루트는 다음 워크스페이스 폴더입니다.
{workspacePath}

모든 작업 결과물은 반드시 위 워크스페이스 폴더 안에 생성하거나 수정하세요.
이미지, 문서, 코드, 로그, 기타 산출물을 만들 때는 워크스페이스 밖의 임시 폴더, 사용자 프로필 폴더, .codex 전역 폴더에 최종 결과물을 남기지 마세요.
도구가 불가피하게 워크스페이스 밖에 파일을 만들면, 최종 결과물을 워크스페이스 안으로 복사하고 사용자에게 워크스페이스 내부 경로를 알려주세요.

사용자 요청.
{prompt}
""";
    }

    private static string? FindExecutableOnPath(params string[] names)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var directory in pathValue.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            foreach (var name in names)
            {
                var candidate = Path.Combine(directory.Trim(), name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private void AddTask()
    {
        var project = SelectedProjectFilter;
        if (project is null || string.IsNullOrWhiteSpace(NewTaskTitle))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(NewTaskBody))
        {
            MessageBox.Show("테스크 내용을 입력해 주세요. 내용이 비어 있으면 등록할 수 없습니다.", "테스크 등록", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var task = TaskManagerDomain.CreateTask(project, NewTaskTitle);
        task.Body = NewTaskBody;
        task.BodyDocument = NewTaskBodyDocument;
        store.UpsertProject(project);
        NewTaskTitle = string.Empty;
        NewTaskBody = string.Empty;
        NewTaskBodyDocument = null;
        SelectedTask = task;
        IsAddTaskOpen = false;
        RefreshAllViews();
    }

    private void RefreshProjectDocuments()
    {
        ProjectDocuments.Clear();
        if (SelectedProject is null)
        {
            return;
        }

        foreach (var document in SelectedProject.Documents
                     .Where(document => !document.IsDeleted)
                     .OrderByDescending(document => document.CreatedAt))
        {
            ProjectDocuments.Add(document);
        }
    }

    private void AddDocument()
    {
        if (SelectedProject is null)
        {
            return;
        }

        var document = new ProjectDocument
        {
            ProjectId = SelectedProject.Id,
            Title = "새 문서",
            CreatedAt = DateTime.Now
        };
        SelectedProject.Documents.Add(document);
        store.UpsertProject(SelectedProject);
        RefreshProjectDocuments();
        SelectedDocument = document;
        StartEditDocument();
    }

    private void StartEditDocument()
    {
        if (SelectedDocument is null)
        {
            return;
        }

        EditingDocumentTitle = SelectedDocument.Title;
        EditingDocumentBody = SelectedDocument.Body;
        EditingDocumentBodyDocument = SelectedDocument.BodyDocument;
        IsDocumentEditMode = true;
    }

    private void CancelEditDocument()
    {
        if (SelectedDocument is not null)
        {
            EditingDocumentTitle = SelectedDocument.Title;
            EditingDocumentBody = SelectedDocument.Body;
            EditingDocumentBodyDocument = SelectedDocument.BodyDocument;
        }

        IsDocumentEditMode = false;
    }

    private void SaveDocument()
    {
        if (SelectedProject is null || SelectedDocument is null)
        {
            return;
        }

        SelectedDocument.Title = string.IsNullOrWhiteSpace(EditingDocumentTitle) ? "제목 없음" : EditingDocumentTitle.Trim();
        SelectedDocument.Body = EditingDocumentBody;
        SelectedDocument.BodyDocument = EditingDocumentBodyDocument;
        SelectedDocument.UpdatedAt = DateTime.Now;
        store.UpsertProject(SelectedProject);
        IsDocumentEditMode = false;
        var saved = SelectedDocument;
        RefreshProjectDocuments();
        SelectedDocument = ProjectDocuments.FirstOrDefault(document => document.Id == saved.Id);
    }

    private void DeleteDocument()
    {
        if (SelectedProject is null || SelectedDocument is null)
        {
            return;
        }

        var answer = MessageBox.Show(
            $"'{SelectedDocument.Title}' 문서를 삭제하시겠습니까?",
            "문서 삭제",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        TaskManagerDomain.SoftDelete(SelectedDocument, DeleteOrigin.Direct);
        store.UpsertProject(SelectedProject);
        SelectedDocument = null;
        IsDocumentEditMode = false;
        RefreshProjectDocuments();
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
        LogTimeEntry = false;
        IsAiPanelActivated = false;
        AiPrompt = string.Empty;
        CancelTimelineItemEdit();
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

    private void AddEntry()
    {
        var project = FindProjectForSelectedTask();
        if (project is null || SelectedTask is null)
        {
            return;
        }

        var hasComment = !string.IsNullOrWhiteSpace(NewComment);
        var hasTimeEntry = LogTimeEntry && TimeEntryDate is not null;
        if (!hasComment && !hasTimeEntry)
        {
            return;
        }

        if (hasComment)
        {
            TaskManagerDomain.AddComment(SelectedTask, NewComment.Trim(), DateTime.Now);
        }

        if (hasTimeEntry)
        {
            TaskManagerDomain.AddTimeEntry(SelectedTask, DateOnly.FromDateTime(TimeEntryDate!.Value), TimeMinutes, TimeMemo);
        }

        store.UpsertProject(project);
        NewComment = string.Empty;
        TimeMemo = string.Empty;
        LogTimeEntry = false;
        OnPropertyChanged(nameof(SelectedTaskTotalText));
        RefreshAllViews();
    }

    private void StartEditTimelineItem(TaskTimelineItem? item)
    {
        if (item?.Comment is null && item?.History is null)
        {
            return;
        }

        EditingTimelineItem = item;
        EditingTimelineText = item.Comment?.Content ?? item.History!.Message;
    }

    private void SaveTimelineItemEdit()
    {
        var project = FindProjectForSelectedTask();
        if (project is null || EditingTimelineItem is null)
        {
            return;
        }

        if (EditingTimelineItem.Comment is { } comment)
        {
            TaskManagerDomain.EditComment(comment, EditingTimelineText.Trim(), DateTime.Now);
        }
        else if (EditingTimelineItem.History is { } history)
        {
            TaskManagerDomain.EditHistory(history, EditingTimelineText.Trim(), DateTime.Now);
        }

        store.UpsertProject(project);
        CancelTimelineItemEdit();
        RefreshAllViews();
    }

    private void CancelTimelineItemEdit()
    {
        EditingTimelineItem = null;
        EditingTimelineText = string.Empty;
    }

    private void DeleteTimelineItem(TaskTimelineItem? item)
    {
        var project = FindProjectForSelectedTask();
        if (project is null || item is null)
        {
            return;
        }

        string label;
        string message;
        ISoftDeletable target;
        if (item.Comment is { } comment)
        {
            label = "댓글";
            message = "이 댓글을 삭제하시겠습니까?\n삭제한 댓글은 옵션 > 휴지통에서 복구할 수 있습니다.";
            target = comment;
        }
        else if (item.TimeEntry is { } timeEntry)
        {
            label = "작업시간 기록";
            message = "이 작업시간 기록을 삭제하시겠습니까?";
            target = timeEntry;
        }
        else if (item.History is { } history)
        {
            label = "히스토리 항목";
            message = "이 히스토리 항목을 삭제하시겠습니까?";
            target = history;
        }
        else
        {
            return;
        }

        var answer = MessageBox.Show(message, $"{label} 삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        if (EditingTimelineItem?.Id == item.Id)
        {
            CancelTimelineItemEdit();
        }

        TaskManagerDomain.SoftDelete(target, DeleteOrigin.Direct);
        store.UpsertProject(project);
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

        if (SelectedTrashItem.Document is { } document)
        {
            document.IsDeleted = false;
            document.DeletedAt = null;
            document.DeleteOrigin = null;
        }
        else if (SelectedTrashItem.Comment is { } comment)
        {
            comment.IsDeleted = false;
            comment.DeletedAt = null;
            comment.DeleteOrigin = null;
        }
        else
        {
            RestoreProjectTree(SelectedTrashItem.Project);
        }

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
        else if (SelectedTrashItem.Document is not null)
        {
            SelectedTrashItem.Project.Documents.Remove(SelectedTrashItem.Document);
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
        SelectedTaskTimeline.Clear();
        if (SelectedTask is null)
        {
            return;
        }

        var items = new List<TaskTimelineItem>();

        foreach (var comment in SelectedTask.Comments.Where(comment => !comment.IsDeleted))
        {
            items.Add(new TaskTimelineItem
            {
                Id = comment.Id,
                CreatedAt = comment.CreatedAt,
                Kind = "댓글",
                Text = comment.Content,
                Comment = comment
            });
        }

        foreach (var entry in SelectedTask.TimeEntries.Where(entry => !entry.IsDeleted))
        {
            var text = $"{entry.Minutes}분 작업 ({entry.WorkDate:yyyy-MM-dd})";
            if (!string.IsNullOrWhiteSpace(entry.Memo))
            {
                text += $" - {entry.Memo}";
            }

            items.Add(new TaskTimelineItem
            {
                Id = entry.Id,
                CreatedAt = entry.CreatedAt,
                Kind = "작업시간",
                Text = text,
                TimeEntry = entry
            });
        }

        foreach (var history in SelectedTask.Histories
                     .Where(history => !history.IsDeleted && history.Type is not (HistoryType.CommentAdded or HistoryType.TimeAdded)))
        {
            items.Add(new TaskTimelineItem
            {
                Id = history.Id,
                CreatedAt = history.CreatedAt,
                Kind = history.Type switch
                {
                    HistoryType.StatusChanged => "상태변경",
                    HistoryType.BodyEdited => "본문수정",
                    HistoryType.Deleted => "삭제",
                    HistoryType.Restored => "복구",
                    _ => history.Type.ToString()
                },
                Text = history.Message,
                History = history
            });
        }

        foreach (var item in items.OrderByDescending(item => item.CreatedAt))
        {
            SelectedTaskTimeline.Add(item);
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
        RefreshProjectTaskStatistics();

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

    private void RefreshProjectTaskStatistics()
    {
        ProjectTaskStatistics.Clear();
        foreach (var project in Projects
                     .Where(project => project.IsActive && !project.IsDeleted)
                     .OrderBy(project => project.Name))
        {
            var rows = project.Tasks
                .Where(task => !task.IsDeleted)
                .Select(task => new
                {
                    task.Title,
                    Minutes = TaskManagerDomain.GetTaskTotalMinutes(task)
                })
                .OrderByDescending(row => row.Minutes)
                .Select(row => new TaskStatRow
                {
                    TaskTitle = row.Title,
                    MinutesText = FormatMinutes(row.Minutes)
                })
                .ToList();

            if (rows.Count == 0)
            {
                continue;
            }

            ProjectTaskStatistics.Add(new ProjectStatGroup
            {
                ProjectName = project.Name,
                TotalText = FormatMinutes(StatisticsService.GetProjectTotalMinutes(project)),
                Tasks = rows
            });
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

            foreach (var document in project.Documents.Where(document => document.IsDeleted))
            {
                TrashItems.Add(new TrashItemView { Kind = "문서", Name = document.Title, DeletedAt = document.DeletedAt, Project = project, Document = document });
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

        foreach (var document in project.Documents.Where(document => document.DeleteOrigin == DeleteOrigin.Cascade))
        {
            document.IsDeleted = false;
            document.DeletedAt = null;
            document.DeleteOrigin = null;
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
        OpenAddTaskCommand.RaiseCanExecuteChanged();
        AddDocumentCommand.RaiseCanExecuteChanged();
        StartEditDocumentCommand.RaiseCanExecuteChanged();
        SaveDocumentCommand.RaiseCanExecuteChanged();
        DeleteDocumentCommand.RaiseCanExecuteChanged();
        OpenTaskDetailCommand.RaiseCanExecuteChanged();
        CloseTaskDetailCommand.RaiseCanExecuteChanged();
        StartEditBodyCommand.RaiseCanExecuteChanged();
        SaveTaskBodyCommand.RaiseCanExecuteChanged();
        ChangeTaskStatusCommand.RaiseCanExecuteChanged();
        AddEntryCommand.RaiseCanExecuteChanged();
        StartEditTimelineItemCommand.RaiseCanExecuteChanged();
        SaveTimelineItemEditCommand.RaiseCanExecuteChanged();
        CancelTimelineItemEditCommand.RaiseCanExecuteChanged();
        DeleteTimelineItemCommand.RaiseCanExecuteChanged();
        DeleteProjectCommand.RaiseCanExecuteChanged();
        DeleteTaskCommand.RaiseCanExecuteChanged();
        RestoreTrashItemCommand.RaiseCanExecuteChanged();
        PermanentlyDeleteTrashItemCommand.RaiseCanExecuteChanged();
        SaveProjectAiSettingsCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(IsAiWorkAvailable));
        ActivateAiPanelCommand.RaiseCanExecuteChanged();
        RunAiPromptCommand.RaiseCanExecuteChanged();
    }

    private static string FormatMinutes(int minutes)
    {
        var hours = minutes / 60;
        var rest = minutes % 60;
        return hours == 0 ? $"{rest}분" : $"{hours}시간 {rest}분";
    }
}
