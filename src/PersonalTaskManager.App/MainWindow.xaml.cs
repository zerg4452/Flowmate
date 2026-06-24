using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PersonalTaskManager.Core.Models;
using PersonalTaskManager.Core.Services;
using PersonalTaskManager.App.ViewModels;
using WinFormsColorDialog = System.Windows.Forms.ColorDialog;
using DrawingColor = System.Drawing.Color;

namespace PersonalTaskManager.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;
    private Point? dragStartPoint;

    public MainWindow()
    {
        InitializeComponent();
        viewModel = new MainViewModel();
        DataContext = viewModel;
        RestoreWindowState();
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
