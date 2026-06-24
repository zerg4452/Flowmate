namespace PersonalTaskManager.Core.Models;

public sealed class AppOptions
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public bool IsReminderEnabled { get; set; } = true;

    public TimeOnly ReminderTime { get; set; } = new(17, 30);

    public string FontFamily { get; set; } = "Malgun Gothic";

    public double FontSize { get; set; } = 14;

    public string ForegroundColor { get; set; } = "#172B4D";

    public string BackgroundColor { get; set; } = "#F4F5F7";

    public double WindowWidth { get; set; } = 1480;

    public double WindowHeight { get; set; } = 940;

    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    public bool WindowMaximized { get; set; }
}
