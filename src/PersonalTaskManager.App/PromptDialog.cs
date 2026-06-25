using System.Windows;
using System.Windows.Controls;

namespace PersonalTaskManager.App;

// 간단한 한 줄 입력 대화상자(WPF 기본 InputBox가 없어 직접 구현).
public static class PromptDialog
{
    public static string? Show(Window owner, string message, string title)
    {
        var window = new Window
        {
            Title = title,
            Width = 420,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow
        };

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = message, Margin = new Thickness(0, 0, 0, 10) });

        var input = new TextBox { Padding = new Thickness(6, 4, 6, 4) };
        panel.Children.Add(input);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        string? result = null;
        var ok = new Button { Content = "확인", Width = 80, IsDefault = true };
        var cancel = new Button { Content = "취소", Width = 80, Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
        ok.Click += (_, _) =>
        {
            result = input.Text;
            window.DialogResult = true;
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        window.Content = panel;
        input.Focus();

        return window.ShowDialog() == true ? result : null;
    }
}
