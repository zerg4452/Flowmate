using System.IO;

namespace PersonalTaskManager.App.Infrastructure;

public static class AppDataPaths
{
    public static string DatabasePath
    {
        get
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PersonalTaskManager");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "personal-task-manager.db");
        }
    }
}
