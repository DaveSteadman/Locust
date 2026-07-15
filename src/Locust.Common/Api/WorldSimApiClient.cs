using System.Net.Http.Json;

namespace Locust;

public sealed class WorldSimApiClient
{
    private readonly HttpClient _httpClient;

    public WorldSimApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<WorldSnapshotResponse> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("/world/snapshot", cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var model = await response.Content.ReadFromJsonAsync<WorldSnapshotResponse>(cancellationToken: cancellationToken);
            if (model is null)
            {
                throw new InvalidOperationException("The world simulator returned an empty snapshot response.");
            }

            return model;
        }

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken: cancellationToken);
        if (error is not null)
        {
            throw new InvalidOperationException($"/world/snapshot failed: {error.Error}");
        }

        response.EnsureSuccessStatusCode();
        throw new InvalidOperationException("/world/snapshot failed without an error payload.");
    }
}
