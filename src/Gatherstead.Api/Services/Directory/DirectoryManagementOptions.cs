namespace Gatherstead.Api.Services.Directory;

/// <summary>
/// Configuration for deleting external-identity accounts via Microsoft Graph. Bound from the
/// <c>ExternalIdentity:DirectoryManagement</c> section. Disabled by default: enabling it requires the
/// API's managed identity to hold the <c>User.DeleteRestricted.All</c> (or <c>User.ReadWrite.All</c>)
/// Graph application permission, admin-consented in the Entra tenant. Until then, self-service and
/// admin deletions still erase all application data; the directory account is reported as
/// <see cref="DirectoryDeletionOutcome.Skipped"/> for manual removal.
/// </summary>
public sealed class DirectoryManagementOptions
{
    public const string SectionName = "ExternalIdentity:DirectoryManagement";

    /// <summary>When false (default), no Graph call is made and deletions report <c>Skipped</c>.</summary>
    public bool Enabled { get; set; }

    /// <summary>Graph API base URL. Defaults to the public cloud v1.0 endpoint.</summary>
    public string GraphBaseUrl { get; set; } = "https://graph.microsoft.com/v1.0";

    /// <summary>Token scope for the managed identity when calling Graph.</summary>
    public string Scope { get; set; } = "https://graph.microsoft.com/.default";
}
