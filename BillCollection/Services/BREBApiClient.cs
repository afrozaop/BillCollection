using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BillCollection.Interfaces;
using BillCollection.Models.BREB;

namespace BillCollection.Services
{
    public class BREBApiClient : IBREBApiClient
    {
        private const int MaxLoggedBody = 2000;

        private readonly IHttpClientFactory _factory;
        private readonly IBREBTokenProvider _tokens;
        private readonly ILogger<BREBApiClient> _logger;

        public BREBApiClient(
            IHttpClientFactory factory,
            IBREBTokenProvider tokens,
            ILogger<BREBApiClient> logger)
        {
            _factory = factory;
            _tokens = tokens;
            _logger = logger;
        }

        public Task<BREBApiResponse<BREBBillingInfoResponse>> BillingInfoAsync(BREBBillingInfoRequest r, CancellationToken ct = default)
            => PostAsync<BREBBillingInfoResponse>("billinginfo", r, ct, mayHaveSideEffects: false);

        public Task<BREBApiResponse<BREBPaymentSubmitResponse>> PaymentSubmitAsync(BREBPaymentSubmitRequest r, CancellationToken ct = default)
            => PostAsync<BREBPaymentSubmitResponse>("billinginfo/paymentsubmit", r, ct, mayHaveSideEffects: true);

        public Task<BREBApiResponse<BREBPaymentAcknowledgmentData>> PaymentAcknowledgeAsync(BREBPaymentAcknowledgmentRequest r, CancellationToken ct = default)
            => PostAsync<BREBPaymentAcknowledgmentData>("billinginfo/paymentacknowledgment", r, ct, mayHaveSideEffects: true);

        public Task<BREBApiResponse<BREBPaymentStatusData>> PaymentStatusAsync(BREBPaymentStatusRequest r, CancellationToken ct = default)
            => PostAsync<BREBPaymentStatusData>("billinginfo/paymentstatus", r, ct, mayHaveSideEffects: false);

        public Task<BREBApiResponse<BREBDisputeData>> DisputeAsync(BREBDisputeRequest r, CancellationToken ct = default)
            => PostAsync<BREBDisputeData>("billinginfo/dispute", r, ct, mayHaveSideEffects: true);

        public Task<BREBApiResponse<BREBReportDetailData>> ReportDetailAsync(BREBReportDetailRequest r, CancellationToken ct = default)
            => PostAsync<BREBReportDetailData>("report/detail", r, ct, mayHaveSideEffects: false);

        public Task<BREBApiResponse<BREBReportSummaryData>> ReportSummaryAsync(BREBReportSummaryRequest r, CancellationToken ct = default)
            => PostAsync<BREBReportSummaryData>("report/summary", r, ct, mayHaveSideEffects: false);

        // ------------------------------------------------------------------

        private async Task<BREBApiResponse<T>> PostAsync<T>(
            string path, object body, CancellationToken ct, bool mayHaveSideEffects, bool retryOn401 = true)
        {
            var token = await _tokens.GetAccessTokenAsync(ct);

            using var client = _factory.CreateClient("BREB");
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(body, options: BREBJson.Options)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Request bodies of these endpoints contain no secrets (token is only in the header).
            _logger.LogInformation("BREB >> {Path} {Body}", path,
                JsonSerializer.Serialize(body, BREBJson.Options));

            string text;
            int http;
            try
            {
                using var response = await client.SendAsync(request, ct);
                http = (int)response.StatusCode;
                text = await response.Content.ReadAsStringAsync(ct);
            }
            catch (HttpRequestException ex)
            {
                throw new BREBApiException(
                    "BREB server is currently unreachable. Please check VPN/network connectivity.",
                    outcomeUnknown: mayHaveSideEffects, inner: ex);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                throw new BREBApiException(
                    "BREB server did not respond in time.",
                    outcomeUnknown: mayHaveSideEffects, inner: ex);
            }

            _logger.LogInformation("BREB << {Path} HTTP {Http} {Body}", path, http,
                text.Length > MaxLoggedBody ? text[..MaxLoggedBody] + "..." : text);

            BREBApiResponse<T>? parsed = null;
            try { parsed = JsonSerializer.Deserialize<BREBApiResponse<T>>(text, BREBJson.Options); }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "BREB response could not be parsed. Path: {Path}", path);
            }

            if (parsed == null)
            {
                throw new BREBApiException(
                    $"Unexpected response from BREB (HTTP {http}).",
                    status: http, outcomeUnknown: mayHaveSideEffects && http >= 500);
            }

            // Token rejected (expired early / revoked): renew once and retry.
            // A 401 means the request was NOT processed, so retrying is safe for every endpoint.
            if (retryOn401 && parsed.Status == 401 &&
                parsed.Errors.Any(e => e.Code == "401" || e.Code == "401.1"))
            {
                _tokens.InvalidateAccessToken();
                return await PostAsync<T>(path, body, ct, mayHaveSideEffects, retryOn401: false);
            }

            // A 5xx on a state-changing call: BREB may or may not have processed it.
            if (parsed.Status >= 500 && mayHaveSideEffects)
            {
                throw new BREBApiException(parsed.ErrorText, parsed.Status, parsed.Errors, outcomeUnknown: true);
            }

            return parsed;
        }
    }
}
