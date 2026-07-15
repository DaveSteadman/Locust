using System.Net.Http.Json;

namespace Locust;

public sealed class SensorSimulatorApiClient
{
    private readonly HttpClient _httpClient;

    public SensorSimulatorApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<SensorArcResponse> GetSensorAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("/sensor", cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var model = await response.Content.ReadFromJsonAsync<SensorArcResponse>(cancellationToken: cancellationToken);
            if (model is null)
            {
                throw new InvalidOperationException("The sensor simulator returned an empty sensor response.");
            }

            return model;
        }

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken: cancellationToken);
        if (error is not null)
        {
            throw new InvalidOperationException($"/sensor failed: {error.Error}");
        }

        response.EnsureSuccessStatusCode();
        throw new InvalidOperationException("/sensor failed without an error payload.");
    }
}
