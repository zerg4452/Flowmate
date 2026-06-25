using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PersonalTaskManager.Core.Models;
using PersonalTaskManager.Core.Services;
using PersonalTaskManager.App.ViewModels;
using WinFormsColorDialog = System.Windows.Forms.ColorDialog;
using DrawingColor = System.Drawing.Color;

namespace PersonalTaskManager.App;

public partial class MainWindow : Window
{
    private const int MaxInsertedImagePixelWidth = 1200;
    private const double MaxInsertedImageDisplayWidth = 480;

    private readonly MainViewModel viewModel;
    private Point? dragStartPoint;
    private IReadOnlyList<string> pendingNewTaskComments = [];

    public MainWindow()
    {
        InitializeComponent();
        viewModel = new MainViewModel();
        DataContext = viewModel;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        DocFontSizeBox.ItemsSource = new double[] { 10, 12, 14, 16, 18, 20, 24, 28, 32 };
        RestoreWindowState();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.SelectedTask):
                LoadBodyDocument(DetailBodyViewer, viewModel.SelectedTask?.BodyDocument);
                break;
            case nameof(MainViewModel.IsBodyEditMode):
                if (viewModel.IsBodyEditMode)
                {
                    LoadBodyDocument(DetailBodyEditor, viewModel.EditingTaskBodyDocument);
                }
                else
                {
                    LoadBodyDocument(DetailBodyViewer, viewModel.SelectedTask?.BodyDocument);
                }

                break;
            case nameof(MainViewModel.SelectedDocument):
                LoadBodyDocument(DocumentViewer, viewModel.SelectedDocument?.BodyDocument);
                break;
            case nameof(MainViewModel.IsDocumentEditMode):
                if (viewModel.IsDocumentEditMode)
                {
                    LoadBodyDocument(DocumentEditor, viewModel.EditingDocumentBodyDocument);
                }
                else
                {
                    LoadBodyDocument(DocumentViewer, viewModel.SelectedDocument?.BodyDocument);
                }

                break;
        }
    }

    private static void LoadBodyDocument(RichTextBox box, byte[]? data)
    {
        box.Document = new FlowDocument();
        if (data is not { Length: > 0 })
        {
            return;
        }

        using var stream = new MemoryStream(data);
        var range = new TextRange(box.Document.ContentStart, box.Document.ContentEnd);
        range.Load(stream, DataFormats.XamlPackage);
    }

    private void SyncDetailBodyDocument()
    {
        if (!viewModel.IsBodyEditMode)
        {
            return;
        }

        var range = new TextRange(DetailBodyEditor.Document.ContentStart, DetailBodyEditor.Document.ContentEnd);
        viewModel.EditingTaskBody = range.Text.TrimEnd('\r', '\n');

        using var stream = new MemoryStream();
        range.Save(stream, DataFormats.XamlPackage);
        viewModel.EditingTaskBodyDocument = stream.ToArray();
    }

    private void DetailBodyEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        var range = new TextRange(DetailBodyEditor.Document.ContentStart, DetailBodyEditor.Document.ContentEnd);
        viewModel.EditingTaskBody = range.Text.TrimEnd('\r', '\n');
    }

    private void SaveTaskBody_Click(object sender, RoutedEventArgs e)
    {
        SyncDetailBodyDocument();
        if (viewModel.SaveTaskBodyCommand.CanExecute(null))
        {
            viewModel.SaveTaskBodyCommand.Execute(null);
        }
    }

    private void CloseTaskDetail_Click(object sender, RoutedEventArgs e)
    {
        SyncDetailBodyDocument();
        if (viewModel.CloseTaskDetailCommand.CanExecute(null))
        {
            viewModel.CloseTaskDetailCommand.Execute(null);
        }
    }

    private static async Task<FlowDocument> BuildMattyBodyDocument(MattyImportResult result)
    {
        var hasImage = result.BodyNodes.Any(node => node.IsImage);
        if (!hasImage)
        {
            return new FlowDocument(new Paragraph(new Run(result.Body)));
        }

        var urls = result.BodyNodes.Where(node => node.IsImage).Select(node => node.ImageUrl).ToList();
        var images = await MattyImportWindow.DownloadBodyImagesAsync(urls);

        var paragraph = new Paragraph();
        foreach (var node in result.BodyNodes)
        {
            if (node.IsImage)
            {
                if (images.TryGetValue(node.ImageUrl, out var bytes) && TryLoadBitmap(bytes) is { } bitmap)
                {
                    var image = new Image
                    {
                        Source = bitmap,
                        Stretch = Stretch.Uniform,
                        Width = Math.Min(bitmap.PixelWidth, MaxInsertedImageDisplayWidth)
                    };
                    paragraph.Inlines.Add(new InlineUIContainer(image));
                }

                continue;
            }

            var parts = node.Text.Split('\n');
            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    paragraph.Inlines.Add(new LineBreak());
                }

                if (parts[i].Length > 0)
                {
                    paragraph.Inlines.Add(new Run(parts[i]));
                }
            }
        }

        var document = new FlowDocument();
        document.Blocks.Add(paragraph);
        return document;
    }

    private static BitmapImage? TryLoadBitmap(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private async void ImportMatty_Click(object sender, RoutedEventArgs e)
    {
        if (!MainViewModel.TryParseMattyTaskId(viewModel.MattyLink, out var taskId))
        {
            MessageBox.Show("올바른 메티 테스크 링크가 아닙니다.\n예: https://easymedia.matty.works:8443/Task/Go/71129", "메티 가져오기", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MattyImportButton.IsEnabled = false;
        try
        {
            var result = await MattyImportWindow.FetchAsync(this, taskId);
            if (result is null)
            {
                return;
            }

            viewModel.ApplyMattyComments(result.Comments);

            if (result.BodyNodes.Count > 0 || !string.IsNullOrWhiteSpace(result.Body))
            {
                viewModel.IsBodyEditMode = true;
                DetailBodyEditor.Document = await BuildMattyBodyDocument(result);
                SyncDetailBodyDocument();
            }

            viewModel.MattyLink = string.Empty;
            MessageBox.Show(
                $"본문과 댓글 {result.Comments.Count}개를 가져왔습니다.\n본문을 확인한 뒤 '본문 저장'을 눌러 반영하세요.",
                "메티 가져오기",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        finally
        {
            MattyImportButton.IsEnabled = true;
        }
    }

    private void InsertDetailBodyImage_Click(object sender, RoutedEventArgs e)
    {
        if (InsertImage(DetailBodyEditor))
        {
            SyncDetailBodyDocument();
        }
    }

    private void NewTaskBodyEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        var range = new TextRange(NewTaskBodyEditor.Document.ContentStart, NewTaskBodyEditor.Document.ContentEnd);
        viewModel.NewTaskBody = range.Text.TrimEnd('\r', '\n');
    }

    private void InsertNewTaskBodyImage_Click(object sender, RoutedEventArgs e)
    {
        if (InsertImage(NewTaskBodyEditor))
        {
            SyncNewTaskBodyDocument();
        }
    }

    private void OpenAddTask_Click(object sender, RoutedEventArgs e)
    {
        if (!viewModel.OpenAddTaskCommand.CanExecute(null))
        {
            MessageBox.Show("먼저 상단 필터에서 프로젝트를 선택해 주세요.", "테스크 추가", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        viewModel.OpenAddTaskCommand.Execute(null);
        NewTaskBodyEditor.Document = new FlowDocument();
        pendingNewTaskComments = [];
    }

    private void AddTaskOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        viewModel.IsAddTaskOpen = false;
        e.Handled = true;
    }

    // ===== 프로젝트 문서 에디터 =====

    private void SaveDocument_Click(object sender, RoutedEventArgs e)
    {
        SyncDocumentEditor();
        if (viewModel.SaveDocumentCommand.CanExecute(null))
        {
            viewModel.SaveDocumentCommand.Execute(null);
        }
    }

    private void SyncDocumentEditor()
    {
        if (!viewModel.IsDocumentEditMode)
        {
            return;
        }

        var range = new TextRange(DocumentEditor.Document.ContentStart, DocumentEditor.Document.ContentEnd);
        viewModel.EditingDocumentBody = range.Text.TrimEnd('\r', '\n');

        using var stream = new MemoryStream();
        range.Save(stream, DataFormats.XamlPackage);
        viewModel.EditingDocumentBodyDocument = stream.ToArray();
    }

    private void DocBold_Click(object sender, RoutedEventArgs e)
    {
        EditingCommands.ToggleBold.Execute(null, DocumentEditor);
        DocumentEditor.Focus();
    }

    private void DocUnderline_Click(object sender, RoutedEventArgs e)
    {
        EditingCommands.ToggleUnderline.Execute(null, DocumentEditor);
        DocumentEditor.Focus();
    }

    private void DocFontSize_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (DocFontSizeBox.SelectedItem is double size && !DocumentEditor.Selection.IsEmpty)
        {
            DocumentEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, size);
        }
    }

    private void DocColor_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinFormsColorDialog { FullOpen = true };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        var brush = new SolidColorBrush(Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B));
        if (!DocumentEditor.Selection.IsEmpty)
        {
            DocumentEditor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, brush);
        }

        DocumentEditor.Focus();
    }

    private void DocLink_Click(object sender, RoutedEventArgs e)
    {
        if (DocumentEditor.Selection.IsEmpty)
        {
            MessageBox.Show("링크로 만들 텍스트를 먼저 선택해 주세요.", "링크", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var url = PromptDialog.Show(this, "링크 주소(URL)를 입력하세요.", "링크 추가");
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        if (!url.Contains("://"))
        {
            url = "https://" + url;
        }

        try
        {
            var link = new Hyperlink(DocumentEditor.Selection.Start, DocumentEditor.Selection.End)
            {
                NavigateUri = new Uri(url)
            };
            link.ToolTip = url;
        }
        catch (UriFormatException)
        {
            MessageBox.Show("올바른 URL이 아닙니다.", "링크", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        DocumentEditor.Focus();
    }

    private void DocImage_Click(object sender, RoutedEventArgs e)
    {
        InsertImage(DocumentEditor);
        DocumentEditor.Focus();
    }

    private void SyncNewTaskBodyDocument()
    {
        var range = new TextRange(NewTaskBodyEditor.Document.ContentStart, NewTaskBodyEditor.Document.ContentEnd);
        viewModel.NewTaskBody = range.Text.TrimEnd('\r', '\n');

        using var stream = new MemoryStream();
        range.Save(stream, DataFormats.XamlPackage);
        viewModel.NewTaskBodyDocument = stream.ToArray();
    }

    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        SyncNewTaskBodyDocument();

        var titleBeforeAdd = viewModel.NewTaskTitle;
        if (viewModel.AddTaskCommand.CanExecute(null))
        {
            viewModel.AddTaskCommand.Execute(null);
        }

        var created = !string.IsNullOrWhiteSpace(titleBeforeAdd) && string.IsNullOrEmpty(viewModel.NewTaskTitle);
        if (created)
        {
            if (pendingNewTaskComments.Count > 0)
            {
                viewModel.ApplyMattyComments(pendingNewTaskComments);
                pendingNewTaskComments = [];
            }

            NewTaskBodyEditor.Document = new FlowDocument();
        }
    }

    private async void ImportMattyNewTask_Click(object sender, RoutedEventArgs e)
    {
        if (!MainViewModel.TryParseMattyTaskId(viewModel.NewTaskMattyLink, out var taskId))
        {
            MessageBox.Show("올바른 메티 테스크 링크가 아닙니다.\n예: https://easymedia.matty.works:8443/Task/Go/71129", "메티 가져오기", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        NewTaskMattyImportButton.IsEnabled = false;
        try
        {
            var result = await MattyImportWindow.FetchAsync(this, taskId);
            if (result is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(viewModel.NewTaskTitle) && !string.IsNullOrWhiteSpace(result.Title))
            {
                viewModel.NewTaskTitle = result.Title;
            }

            NewTaskBodyEditor.Document = await BuildMattyBodyDocument(result);
            SyncNewTaskBodyDocument();
            pendingNewTaskComments = result.Comments;
            viewModel.NewTaskMattyLink = string.Empty;

            MessageBox.Show(
                $"본문과 댓글 {result.Comments.Count}개를 가져왔습니다.\n'테스크 추가'를 누르면 댓글도 함께 등록됩니다.",
                "메티 가져오기",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        finally
        {
            NewTaskMattyImportButton.IsEnabled = true;
        }
    }

    private static bool InsertImage(RichTextBox editor)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.gif;*.bmp",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        BitmapImage bitmap;
        try
        {
            bitmap = LoadScaledBitmap(dialog.FileName);
        }
        catch (Exception)
        {
            MessageBox.Show("이미지를 불러올 수 없습니다.", "이미지 삽입 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var image = new Image
        {
            Source = bitmap,
            MaxWidth = MaxInsertedImageDisplayWidth,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 4, 0, 4)
        };

        editor.Focus();
        var container = new InlineUIContainer(image, editor.CaretPosition);
        editor.CaretPosition = container.ElementEnd;
        return true;
    }

    private static BitmapImage LoadScaledBitmap(string path)
    {
        var uri = new Uri(path, UriKind.Absolute);
        var probe = new BitmapImage();
        probe.BeginInit();
        probe.CacheOption = BitmapCacheOption.OnLoad;
        probe.UriSource = uri;
        probe.EndInit();

        if (probe.PixelWidth <= MaxInsertedImagePixelWidth)
        {
            probe.Freeze();
            return probe;
        }

        var scaled = new BitmapImage();
        scaled.BeginInit();
        scaled.CacheOption = BitmapCacheOption.OnLoad;
        scaled.UriSource = uri;
        scaled.DecodePixelWidth = MaxInsertedImagePixelWidth;
        scaled.EndInit();
        scaled.Freeze();
        return scaled;
    }

    private void RestoreWindowState()
    {
        Width = viewModel.WindowWidth;
        Height = viewModel.WindowHeight;

        if (viewModel.WindowLeft is double left && viewModel.WindowTop is double top &&
            IsOnScreen(left, top, Width, Height))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }

        if (viewModel.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private static bool IsOnScreen(double left, double top, double width, double height)
    {
        var virtualBounds = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        return virtualBounds.IntersectsWith(new Rect(left, top, width, height));
    }

    protected override void OnClosed(EventArgs e)
    {
        viewModel.WindowMaximized = WindowState == WindowState.Maximized;
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        viewModel.WindowWidth = bounds.Width;
        viewModel.WindowHeight = bounds.Height;
        viewModel.WindowLeft = bounds.Left;
        viewModel.WindowTop = bounds.Top;
        viewModel.PersistWindowState();

        viewModel.Dispose();
        base.OnClosed(e);
    }

    private void ThemePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && tag.Split(';') is [var fg, var bg])
        {
            viewModel.ApplyThemePreset(fg, bg);
        }
    }

    private void PickForeground_Click(object sender, RoutedEventArgs e)
    {
        if (TryPickColor(viewModel.ForegroundColor, out var hex))
        {
            viewModel.ForegroundColor = hex;
        }
    }

    private void PickBackground_Click(object sender, RoutedEventArgs e)
    {
        if (TryPickColor(viewModel.BackgroundColor, out var hex))
        {
            viewModel.BackgroundColor = hex;
        }
    }

    private static bool TryPickColor(string current, out string hex)
    {
        hex = current;
        using var dialog = new WinFormsColorDialog { FullOpen = true };

        try
        {
            var media = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(current);
            dialog.Color = DrawingColor.FromArgb(media.R, media.G, media.B);
        }
        catch
        {
            // 잘못된 색 문자열이면 기본값으로 연다.
        }

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return false;
        }

        hex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        return true;
    }

    private void DetailOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        SyncDetailBodyDocument();
        if (viewModel.CloseTaskDetailCommand.CanExecute(null))
        {
            viewModel.CloseTaskDetailCommand.Execute(null);
        }

        e.Handled = true;
    }

    private void PopupContent_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void TaskButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        dragStartPoint = e.GetPosition(null);
    }

    private void TaskButton_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            dragStartPoint is null ||
            sender is not Button { DataContext: WorkTask task })
        {
            return;
        }

        var diff = dragStartPoint.Value - e.GetPosition(null);
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        dragStartPoint = null;
        DragDrop.DoDragDrop((DependencyObject)sender, task, DragDropEffects.Move);
    }

    private void StatusColumn_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(WorkTask)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void StatusColumn_Drop(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: BoardColumn column } ||
            !e.Data.GetDataPresent(typeof(WorkTask)) ||
            e.Data.GetData(typeof(WorkTask)) is not WorkTask task)
        {
            return;
        }

        viewModel.MoveTaskToStatus(task, column.StatusName);
        e.Handled = true;
    }
}
