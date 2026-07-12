using System.Net;
using System.Net.Http.Headers;
using Azure.Core;
using Azure.Identity;

namespace Gatherstead.Api.Services.Directory;

/// <summary>
/// Deletes an Entra External ID account via a direct Microsoft Graph REST call
/// (<c>DELETE /users/{id}</c>), authenticated with the API's managed identity through
/// <see cref="DefaultAzureCredential"/> — the same credential the app already uses for Key Vault /
/// telemetry. A deliberately lightweight alternative to the full Microsoft.Graph SDK for the single
/// call we need. Honours <see cref="DirectoryManagementOptions.Enabled"/> and never throws.
/// </summary>
public sealed class GraphDirectoryAccountService : IDirectoryAccountService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DirectoryManagementOptions _options;
    private readonly ILogger<GraphDirectoryAccountService> _logger;

    // DefaultAzureCredential is thread-safe and caches tokens internally; static so the token cache
    // survives across requests (the service itself is scoped).
    private static readonly Lazy<TokenCredential> _credential = new(() => new DefaultAzureCredential());

    public GraphDirectoryAccountService(
        IHttpClientFactory httpClientFactory,
        DirectoryManagementOptions options,
        ILogger<GraphDirectoryAccountService> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DirectoryDeletionOutcome> DeleteUserAsync(string externalId, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return DirectoryDeletionOutcome.Skipped;

        if (string.IsNullOrWhiteSpace(externalId))
            return DirectoryDeletionOutcome.NotFound;

        try
        {
            var token = await _credential.Value.GetTokenAsync(
                new TokenRequestContext([_options.Scope]), cancellationToken);

            var client = _httpClientFactory.CreateClient("graph");
            using var request = new HttpRequestMessage(
                HttpMethod.Delete,
                $"{_options.GraphBaseUrl.TrimEnd('/')}/users/{Uri.EscapeDataString(externalId)}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

            using var httpResponse = await client.SendAsync(request, cancellationToken);

            if (httpResponse.IsSuccessStatusCode)
                return DirectoryDeletionOutcome.Deleted;

            if (httpResponse.StatusCode == HttpStatusCode.NotFound)
                return DirectoryDeletionOutcome.NotFound;

            // Log the status only (never the token or PII) so an operator can follow up.
            _logger.LogError(
                "Graph account deletion failed with status {StatusCode} for external id {ExternalId}.",
                (int)httpResponse.StatusCode, externalId);
            return DirectoryDeletionOutcome.Failed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Graph account deletion threw for external id {ExternalId}.", externalId);
            return DirectoryDeletionOutcome.Failed;
        }
    }
}
