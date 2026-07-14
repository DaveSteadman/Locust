using System.Net.Http.Json;

namespace Locust;

public sealed class LocustApiClient
{
    private readonly HttpClient _httpClient;

    public LocustApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<RegisterPingResponse> RegisterPingAsync(RegisterPingRequest request, CancellationToken cancellationToken = default)
    {
        return await PostAsync<RegisterPingRequest, RegisterPingResponse>("/pings", request, cancellationToken);
    }

    public async Task<RegisterPingsResponse> RegisterPingsAsync(RegisterPingsRequest request, CancellationToken cancellationToken = default)
    {
        return await PostAsync<RegisterPingsRequest, RegisterPingsResponse>("/pings/batch", request, cancellationToken);
    }

    public async Task<RegisterQuadTreeValueResponse> RegisterQuadTreeValueAsync(RegisterQuadTreeValueRequest request, CancellationToken cancellationToken = default)
    {
        return await PostAsync<RegisterQuadTreeValueRequest, RegisterQuadTreeValueResponse>("/values/quadtree", request, cancellationToken);
    }

    public async Task<GridQueryResponse> QueryGridAsync(GridQueryRequest request, CancellationToken cancellationToken = default)
    {
        return await PostAsync<GridQueryRequest, GridQueryResponse>("/queries/grid", request, cancellationToken);
    }

    public async Task<TreeRectQueryResponse> QueryRectAsync(TreeRectQueryRequest request, CancellationToken cancellationToken = default)
    {
        return await PostAsync<TreeRectQueryRequest, TreeRectQueryResponse>("/queries/rect", request, cancellationToken);
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(path, request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var model = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken);
            if (model is null)
            {
                throw new InvalidOperationException($"The API returned an empty response body for '{path}'.");
            }

            return model;
        }

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken: cancellationToken);
        if (error is not null)
        {
            throw new InvalidOperationException($"{path} failed: {error.Error}");
        }

        response.EnsureSuccessStatusCode();
        throw new InvalidOperationException($"{path} failed without an error payload.");
    }
}
