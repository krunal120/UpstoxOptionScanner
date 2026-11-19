using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NiftyQuant.Application.Common.Options;

namespace NiftyQuant.Infrastructure.Services;

public class UpstoxAuthenticationHandler : DelegatingHandler
{
    private readonly IOptions<UpstoxOptions> _options;
    private readonly ILogger<UpstoxAuthenticationHandler> _logger;

    public UpstoxAuthenticationHandler(IOptions<UpstoxOptions> options, ILogger<UpstoxAuthenticationHandler> logger)
    {
        _options = options;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = _options.Value.AccessToken;

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Upstox AccessToken is not configured in User Secrets or environment.");
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return await base.SendAsync(request, cancellationToken);
    }
}
