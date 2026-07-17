namespace HAMBOX.Modules.Legal.Domain.Legal;

public enum LegalSectionAuditAction
{
    Created = 0,
    Updated = 1,
    Published = 2,
    Archived = 3,
    Unarchived = 4,
    Restored = 5,
    Duplicated = 6,
    Deleted = 7,
    VisibilityChanged = 8,
}
