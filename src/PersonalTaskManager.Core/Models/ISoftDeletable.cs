namespace PersonalTaskManager.Core.Models;

public interface ISoftDeletable
{
    bool IsDeleted { get; set; }

    DateTime? DeletedAt { get; set; }

    DeleteOrigin? DeleteOrigin { get; set; }
}
