namespace HAMBOX.Modules.Suppliers.Domain.Suppliers;

public enum SupplierStatus
{
    Active = 0,
    Inactive = 1,
    Suspended = 2,
}

/// <summary>
/// Credential shape a provider expects. Drives which credential fields the admin UI exposes;
/// does not imply a specific transport (REST/GraphQL/SOAP/etc. all authenticate the same way).
/// </summary>
public enum SupplierAuthenticationType
{
    None = 0,
    ApiKey = 1,
    BasicAuth = 2,
    BearerToken = 3,
    OAuth2 = 4,
}

public enum SupplierMappingStatus
{
    Active = 0,
    Inactive = 1,
}

public enum SupplierAuditAction
{
    Created = 0,
    Updated = 1,
    Enabled = 2,
    Disabled = 3,
    Deleted = 4,
    PriorityChanged = 5,
    CredentialsUpdated = 6,
    ConnectionTested = 7,
    MappingCreated = 8,
    MappingUpdated = 9,
    MappingDeleted = 10,
    AvailabilitySynced = 11,
}
