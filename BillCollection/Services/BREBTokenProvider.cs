using System.Net.Http.Json;
using BillCollection.Interfaces;
using BillCollection.Models.BREB;
using Microsoft.Extensions.Options;

namespace BillCollection.Services
{
    /// <summary>
    /// Singleton token cache. BREB limits how many times we may log in per day
    /// ("AuthenticationLimit": "2 of 100"), so we log in once, then renew with the
    /// refresh token, and only fall back to username/password when needed.
    /// </summary>
    public class BREBTokenProvider : IBREBTokenProvider
    {
        private readonly IHttpClientFactory _factory;
        private readonly BREBOptions _options;
        private readonly ILogger<BREBTokenProvider> _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);

        private string? _accessToken;
        private DateTime _accessExpiresUtc = DateTime.MinValue;
        private string? _refreshToken;
        private DateTime _refreshExpiresUtc = DateTime.MinValue;

        public BREBTokenProvider(
            IHttpClientFactory factory,
            IOptions<BREBOptions> options,
            ILogger<BREBTokenProvider> logger)
        {
            _factory = factory;
            _options = options.Value;
            _logger = logger;
        }

        public void InvalidateAccessToken()
        {
            _accessToken = null;
            _accessExpiresUtc = DateTime.MinValue;
        }

        public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            if (IsAccessValid()) return _accessToken!;

            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (IsAccessValid()) return _accessToken!;

                // 1) cheap path: refresh token
                if (!string.IsNullOrEmpty(_refreshToken) && DateTime.UtcNow < _refreshExpiresUtc)
                {
                    var refreshed = await TryAuthenticateAsync(
                        new { RefreshToken = _refreshToken }, "refresh token", cancellationToken);

                    if (refreshed != null) return Store(refreshed, keepRefresh: true);
                }

                // 2) full login (counts against the daily authentication limit)
                var data = await TryAuthenticateAsync(
                    new { Username = _options.Username, Password = _options.Password },
                    "username/password", cancellationToken);

                if (data == null)
                    throw new BREBApiException("BREB authentication failed.");

                return Store(data, keepRefresh: false);
            }
            finally
            {
                _lock.Release();
            }
        }

        private bool IsAccessValid() =>
            !string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _accessExpiresUtc;

        private string Store(BREBTokenResponse data, bool keepRefresh)
        {
            var margin = TimeSpan.FromSeconds(Math.Max(0, _options.TokenSafetyMarginSeconds));
            var lifetime = TimeSpan.FromMinutes(data.ExpiresIn > 0 ? data.ExpiresIn : 5);

            _accessToken = data.AccessToken;
            _accessExpiresUtc = DateTime.UtcNow + lifetime - margin;

            if (!keepRefresh || !string.IsNullOrEmpty(data.RefreshToken))
            {
                _refreshToken = data.RefreshToken;

                // Use (RefreshTokenExpiresAt - CreateTimestamp) as lifetime so BREB's
                // server clock / time zone does not matter.
                if (DateTime.TryParse(data.RefreshTokenExpiresAt, out var exp) &&
                    DateTime.TryParse(data.CreateTimestamp, out var created) && exp > created)
                {
                    _refreshExpiresUtc = DateTime.UtcNow + (exp - created) - margin;
                }
                else
                {
                    _refreshExpiresUtc = DateTime.MinValue; // unknown -> never use refresh
                }
            }

            _logger.LogInformation(
                "BREB token stored. ExpiresIn: {Minutes} min, AuthenticationLimit: {Limit}",
                data.ExpiresIn, data.AuthenticationLimit ?? "n/a");

            return _accessToken!;
        }

        private async Task<BREBTokenResponse?> TryAuthenticateAsync(
            object body, string method, CancellationToken ct)
        {
            using var client = _factory.CreateClient("BREB");
            using var request = new HttpRequestMessage(HttpMethod.Post, "authenticate")
            {
                Content = JsonContent.Create(body, options: BREBJson.Options)
            };
            request.Headers.Add("apikey", _options.ApiKey);

            try
            {
                using var response = await client.SendAsync(request, ct);
                var text = await response.Content.ReadAsStringAsync(ct);

                BREBApiResponse<BREBTokenResponse>? parsed = null;
                try { parsed = System.Text.Json.JsonSerializer.Deserialize<BREBApiResponse<BREBTokenResponse>>(text, BREBJson.Options); }
                catch (System.Text.Json.JsonException) { /* handled below */ }

                if (parsed != null && parsed.IsSuccess && !string.IsNullOrEmpty(parsed.Data?.AccessToken))
                    return parsed.Data;

                // NEVER log the request body here (contains password / refresh token).
                _logger.LogWarning(
                    "BREB authenticate ({Method}) failed. HttpStatus: {Http}, Errors: {Errors}",
                    method, (int)response.StatusCode, parsed?.ErrorText ?? "unparseable response");

                if (method == "username/password")
                    throw new BREBApiException(
                        "BREB authentication failed: " + (parsed?.ErrorText ?? $"HTTP {(int)response.StatusCode}"),
                        parsed?.Status ?? (int)response.StatusCode, parsed?.Errors);

                return null; // refresh failed -> caller falls back to full login
            }
            catch (HttpRequestException ex)
            {
                throw new BREBApiException(
                    "BREB server is currently unreachable. Please check VPN/network connectivity.",
                    outcomeUnknown: false, inner: ex);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                throw new BREBApiException(
                    "BREB server connection timed out. Please check VPN/network connectivity.",
                    outcomeUnknown: false, inner: ex);
            }
        }
    }
}
