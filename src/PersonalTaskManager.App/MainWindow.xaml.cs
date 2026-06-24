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

    public MainWindow()
    {
        InitializeComponent();
        viewModel = new MainViewModel();
        DataContext = viewModel;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
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

        if (!string.IsNullOrWhiteSpace(titleBeforeAdd) && string.IsNullOrEmpty(viewModel.NewTaskTitle))
        {
            NewTaskBodyEditor.Document = new FlowDocument();
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
