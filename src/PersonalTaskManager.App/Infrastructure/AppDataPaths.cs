using System.IO;

namespace PersonalTaskManager.App.Infrastructure;

public static class AppDataPaths
{
    public static string BaseDirectory
    {
        get
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PersonalTaskManager");
            Directory.CreateDirectory(directory);
            return directory;
        }
    }

    public static string DatabasePath => Path.Combine(BaseDirectory, "personal-task-manager.db");

    public static string WebView2Folder
    {
        get
        {
            var directory = Path.Combine(BaseDirectory, "WebView2");
            Directory.CreateDirectory(directory);
            return directory;
        }
    }
}
