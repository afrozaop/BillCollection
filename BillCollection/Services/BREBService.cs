using BillCollection.Interfaces;
using BillCollection.Models;
using BillCollection.Models.BREB;
using BillCollection.ViewModels.BREB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;


namespace BillCollection.Services
{
    public class BREBService : IBREBService
    {
        private readonly HttpClient _httpClient;
        private readonly BREBOptions _options;
        private readonly ApplicationDbContext _context;

        public BREBService(
            HttpClient httpClient,
            IOptions<BREBOptions> options,
            ApplicationDbContext context)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _context = context;

            _httpClient.BaseAddress =
                new Uri(_options.BaseUrl);

            _httpClient.Timeout =
                TimeSpan.FromSeconds(_options.TimeoutSeconds);
        }

        public async Task<BREBTokenResponse?> AuthenticateAsync(
            CancellationToken cancellationToken = default)
        {
            var request = new
            {
                Username = _options.Username,
                Password = _options.Password
            };

            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                "authenticate");

            httpRequest.Headers.Add(
                "apikey",
                _options.ApiKey);

            httpRequest.Content =
                JsonContent.Create(request);

            try
            {
                using var response = await _httpClient.SendAsync(
                    httpRequest,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return await response.Content
                    .ReadFromJsonAsync<BREBTokenResponse>(
                        cancellationToken: cancellationToken);
            }
            catch (HttpRequestException)
            {
                throw new Exception(
                    "BREB server is currently unreachable. " +
                    "Please check VPN/network connectivity.");
            }
            catch (TaskCanceledException)
            {
                throw new Exception(
                    "BREB server connection timed out. " +
                    "Please check VPN/network connectivity.");
            }
        }

        public async Task<BREBBillingInfoResponse?> GetBillingInfoAsync(
     int branchId,
     BREBBillingInfoRequest request,
     CancellationToken cancellationToken = default)
        {
            // Branch verify
            var branch = await _context.Branches
                .FirstOrDefaultAsync(
                    x => x.Id == branchId,
                    cancellationToken);

            if (branch == null)
            {
                throw new Exception(
                    "Branch information could not be found.");
            }

            // SMS Account Number required
            if (string.IsNullOrWhiteSpace(request.SMSAccountNo))
            {
                throw new Exception(
                    "SMS Account Number is required.");
            }

            /*
             * TEMPORARY TEST MODE
             * --------------------
             * BREB API is currently unavailable.
             * So we are returning test bill data directly.
             *
             * Later only this section will be replaced
             * with actual BREB API call.
             */

            var response = new BREBBillingInfoResponse
            {
                SMSAccountNo = request.SMSAccountNo,
                RefNo = "TEST-" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                PaymentTimestamp =
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),

                DueType = "Normal",
                BillMonth = DateTime.Now.ToString("MMM-yyyy"),

                Amount = 161.00m,
                Vat = 8.00m,

                ExpiresIn = "5",

                Message = "Bill information retrieved successfully."
            };

            return response;
        }
        public async Task<BREBPaymentSubmitResponse?> SubmitPaymentAsync(
    int branchId,
    int transactionId,
    CancellationToken cancellationToken = default)
        {
            var transaction =
                await _context.BREBTransactions
                    .FirstOrDefaultAsync(
                        x =>
                            x.Id == transactionId &&
                            x.BranchId == branchId,
                        cancellationToken);

            if (transaction == null)
            {
                throw new Exception(
                    "Payment transaction could not be found.");
            }

            if (transaction.TxnStatus != "BillFound")
            {
                throw new Exception(
                    "This bill is not available for payment.");
            }

            /*
             * TEMPORARY TEST MODE
             * --------------------
             * BREB API is currently unavailable.
             *
             * We are simulating successful payment submission.
             */

            transaction.TxnStatus =
                "WaitingForAcknowledgement";

            transaction.UpdatedBy =
                "SYSTEM";

            transaction.UpdatedOn =
                DateTime.Now;

            await _context.SaveChangesAsync(
                cancellationToken);

            return new BREBPaymentSubmitResponse
            {
                SMSAccountNo =
                    transaction.SMSAccountNo,

                RefNo =
                    transaction.RefNo,

                PayerTransactionId =
                    transaction.PayerTransactionId,

                ExpiresAt =
                    DateTime.Now
                        .AddMinutes(5)
                        .ToString("yyyy-MM-dd HH:mm:ss"),

                Message =
                    "Payment submitted successfully. " +
                    "Waiting for acknowledgment.",

                Success = true
            };
        }
        public async Task<int> SaveBillingTransactionAsync(
    int branchId,
    BREBBillingInfoResponse billingInfo,
    string? username,
    CancellationToken cancellationToken = default)
        {
            if (billingInfo == null)
            {
                throw new Exception(
                    "Bill information is required.");
            }

            if (string.IsNullOrWhiteSpace(
                    billingInfo.SMSAccountNo))
            {
                throw new Exception(
                    "SMS Account Number is required.");
            }

            if (string.IsNullOrWhiteSpace(
                    billingInfo.RefNo))
            {
                throw new Exception(
                    "BREB Reference Number is required.");
            }

            // Generate our internal transaction ID
            var payerTransactionId =
                Guid.NewGuid().ToString("N");

            DateTime? paymentTimestamp = null;

            if (DateTime.TryParse(
                    billingInfo.PaymentTimestamp,
                    out var parsedDate))
            {
                paymentTimestamp = parsedDate;
            }

            var transaction = new BREBTransaction
            {
                BranchId = branchId,

                PayerTransactionId =
                    payerTransactionId,

                RefNo = billingInfo.RefNo,

                PaymentTimestamp =
                    paymentTimestamp,

                SMSAccountNo =
                    billingInfo.SMSAccountNo,

                DueType =
                    billingInfo.DueType,

                BillMonth =
                    billingInfo.BillMonth,

                Amount =
                    billingInfo.Amount,

                Vat =
                    billingInfo.Vat,

                Message =
                    billingInfo.Message,

                ExpiresIn =
                    billingInfo.ExpiresIn,

                TxnStatus =
                    "BillFound",

                CreatedBy =
                    username,

                CreatedOn =
                    DateTime.Now
            };

            _context.BREBTransactions.Add(transaction);

            await _context.SaveChangesAsync(
                cancellationToken);

            return transaction.Id;
        }
        public async Task<bool> AcknowledgePaymentAsync(
    int branchId,
    int transactionId,
    string? username,
    CancellationToken cancellationToken = default)
        {
            var transaction = await _context.BREBTransactions
                .FirstOrDefaultAsync(
                    x => x.Id == transactionId &&
                         x.BranchId == branchId,
                    cancellationToken);

            if (transaction == null)
            {
                throw new Exception(
                    "Payment transaction could not be found.");
            }

            if (transaction.TxnStatus != "WaitingForAcknowledgement")
            {
                throw new Exception(
                    "Only payments waiting for acknowledgement can be acknowledged.");
            }

            // TEMPORARY TEST MODE: No real BREB API call.
            transaction.TxnStatus = "Success";
            transaction.UpdatedBy = username;
            transaction.UpdatedOn = DateTime.Now;

            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }
        public async Task<List<BREBTransaction>> GetCollectionListAsync(
    int branchId,
    CancellationToken cancellationToken = default)
        {
            return await _context.BREBTransactions
                .Where(x =>
                    x.BranchId == branchId &&
                    x.TxnStatus == "Success")
                .OrderByDescending(x => x.CreatedOn)
                .ToListAsync(cancellationToken);
        }
        public async Task<List<BREBTransaction>> GetUnsuccessfulListAsync(
    int branchId,
    CancellationToken cancellationToken = default)
        {
            var unsuccessfulStatuses = new[]
            {
        "Failed",
        "Pending",
        "WaitingForAcknowledgement"
    };

            return await _context.BREBTransactions
                .Where(x =>
                    x.BranchId == branchId &&
                    x.TxnStatus != null &&
                    unsuccessfulStatuses.Contains(x.TxnStatus))
                .OrderByDescending(x => x.CreatedOn)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<BREBTransaction>> GetDisputeListAsync(
    int branchId,
    CancellationToken cancellationToken = default)
        {
            return await _context.BREBTransactions
                .Where(x =>
                    x.BranchId == branchId &&
                    x.TxnStatus != null &&
                    x.TxnStatus == "Dispute")
                .OrderByDescending(x => x.CreatedOn)
                .ToListAsync(cancellationToken);
        }

        public async Task<BREBReconcileReportViewModel> GetReconcileReportAsync(
     int branchId,
     CancellationToken cancellationToken = default)
        {
            var transactions = await _context.BREBTransactions
                .Where(x => x.BranchId == branchId)
                .OrderByDescending(x => x.CreatedOn)
                .ToListAsync(cancellationToken);

            return new BREBReconcileReportViewModel
            {
                TotalTransactions = transactions.Count,

                SuccessfulPayments = transactions.Count(x =>
                    x.TxnStatus == "Success"),

                PendingPayments = transactions.Count(x =>
                    x.TxnStatus == "WaitingForAcknowledgement" ||
                    x.TxnStatus == "Pending"),

                FailedPayments = transactions.Count(x =>
                    x.TxnStatus == "Failed"),

                TotalSuccessfulAmount = transactions
                    .Where(x => x.TxnStatus == "Success")
                    .Sum(x => x.Amount),

                Transactions = transactions
            };
        }
    }
}