using LiteDB;
using PersonalTaskManager.Core.Models;

namespace PersonalTaskManager.Core.Persistence;

public sealed class LiteDbTaskStore : IDisposable
{
    public const int CurrentSchemaVersion = 1;

    private readonly LiteDatabase database;

    public LiteDbTaskStore(string databasePath)
    {
        RegisterCustomTypes();
        database = new LiteDatabase(databasePath);
    }

    public void Initialize()
    {
        var schema = SchemaCollection.FindById("schema");
        if (schema is null)
        {
            SchemaCollection.Upsert(new SchemaInfo { Version = CurrentSchemaVersion });
        }
        else if (schema.Version < CurrentSchemaVersion)
        {
            schema.Version = CurrentSchemaVersion;
            SchemaCollection.Upsert(schema);
        }

        if (OptionsCollection.Count() == 0)
        {
            OptionsCollection.Insert(new AppOptions());
        }
    }

    public int GetSchemaVersion()
    {
        return SchemaCollection.FindById("schema")?.Version ?? 0;
    }

    public AppOptions GetOptions()
    {
        Initialize();
        return OptionsCollection.FindAll().First();
    }

    public void SaveOptions(AppOptions options)
    {
        OptionsCollection.Upsert(options);
    }

    public IReadOnlyList<Project> GetProjects(bool includeDeleted)
    {
        var projects = ProjectsCollection.FindAll();
        if (!includeDeleted)
        {
            projects = projects.Where(project => !project.IsDeleted);
        }

        return projects
            .OrderBy(project => project.Name)
            .ToList();
    }

    public void UpsertProject(Project project)
    {
        ProjectsCollection.Upsert(project);
    }

    public void DeleteProjectPermanently(Guid projectId)
    {
        ProjectsCollection.Delete(projectId);
    }

    public void Dispose()
    {
        database.Dispose();
    }

    private ILiteCollection<SchemaInfo> SchemaCollection =>
        database.GetCollection<SchemaInfo>("schema");

    private ILiteCollection<AppOptions> OptionsCollection =>
        database.GetCollection<AppOptions>("options");

    private ILiteCollection<Project> ProjectsCollection =>
        database.GetCollection<Project>("projects");

    private static void RegisterCustomTypes()
    {
        BsonMapper.Global.RegisterType(
            serialize: value => new BsonValue(((DateOnly)value).ToDateTime(TimeOnly.MinValue)),
            deserialize: bson => DateOnly.FromDateTime(bson.AsDateTime));

        BsonMapper.Global.RegisterType(
            serialize: value => new BsonValue(((TimeOnly)value).ToString("HH:mm")),
            deserialize: bson => TimeOnly.Parse(bson.AsString));
    }
}
