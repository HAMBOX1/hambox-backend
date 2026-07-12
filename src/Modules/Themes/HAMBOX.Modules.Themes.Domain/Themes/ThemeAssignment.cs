using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Themes.Domain.Themes;

public sealed class ThemeAssignment : BaseEntity
{
    private ThemeAssignment()
    {
    }

    private ThemeAssignment(Guid id, Guid themeId, ThemeAssignmentType assignmentType, string targetKey, int priority)
        : base(id)
    {
        ThemeId = themeId;
        AssignmentType = assignmentType;
        TargetKey = targetKey;
        Priority = priority;
        IsActive = true;
    }

    public Guid ThemeId { get; private set; }
    public ThemeAssignmentType AssignmentType { get; private set; }
    public string TargetKey { get; private set; } = string.Empty;
    public int Priority { get; private set; }
    public bool IsActive { get; private set; }

    public static ThemeAssignment Create(Guid themeId, ThemeAssignmentType type, string targetKey, int priority) =>
        new(Guid.NewGuid(), themeId, type, targetKey, priority);

    public void Deactivate() => IsActive = false;
}
