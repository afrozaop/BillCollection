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
        private readonly ILogger<BREBService> _logger;


        public BREBService(
    HttpClient httpClient,
    IOptions<BREBOptions> options,
    ApplicationDbContext context,
    ILogger<BREBService> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _context = context;
            _logger = logger;

            _httpClient.BaseAddress =
                new Uri(_options.BaseUrl);

            _httpClient.Timeout =
                TimeSpan.FromSeconds(_options.TimeoutSeconds);

            _logger.LogInformation(
                "BREB Service initialized. Test Mode is active. API access is not configured.");
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
            _logger.LogInformation(
                "BREB TEST MODE: Bill query started. BranchId: {BranchId}",
                branchId);

            try
            {
                // Branch verify
                var branch = await _context.Branches
                    .FirstOrDefaultAsync(
                        x => x.Id == branchId,
                        cancellationToken);

                if (branch == null)
                {
                    _logger.LogWarning(
                        "BREB bill query failed. Branch not found. BranchId: {BranchId}",
                        branchId);

                    throw new Exception(
                        "Branch information could not be found.");
                }

                // SMS Account Number required
                if (string.IsNullOrWhiteSpace(request.SMSAccountNo))
                {
                    _logger.LogWarning(
                        "BREB bill query failed. SMS Account Number is missing. BranchId: {BranchId}",
                        branchId);

                    throw new Exception(
                        "SMS Account Number is required.");
                }

                // TEST MODE: No real BREB API call
                if (!TestAccounts.TryGetValue(
                        request.SMSAccountNo,
                        out var account))
                {
                    _logger.LogWarning(
                        "BREB TEST MODE: No test bill found. BranchId: {BranchId}, Account: {AccountNo}",
                        branchId,
                        MaskAccountNo(request.SMSAccountNo));

                    return null;
                }

                var billingInfo = new BREBBillingInfoResponse
                {
                    SMSAccountNo = request.SMSAccountNo,
                    AccountName = account.Name,
                    RefNo = "TEST-" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                    PaymentTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    DueType = "Normal",
                    BillMonth = DateTime.Now.ToString("MMM-yyyy"),
                    Amount = account.Amount,
                    Vat = account.Vat,
                    ExpiresIn = "5",
                    Message = "Test bill information retrieved successfully."
                };

                _logger.LogInformation(
                    "BREB TEST MODE: Bill information retrieved. BranchId: {BranchId}, Account: {AccountNo}, RefNo: {RefNo}, Amount: {Amount}",
                    branchId,
                    MaskAccountNo(request.SMSAccountNo),
                    billingInfo.RefNo,
                    billingInfo.Amount);

                return billingInfo;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "BREB bill query cancelled. BranchId: {BranchId}",
                    branchId);

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "BREB bill query error. BranchId: {BranchId}",
                    branchId);

                throw;
            }
        }
        private static string MaskAccountNo(string? accountNo)
        {
            if (string.IsNullOrWhiteSpace(accountNo))
                return "N/A";

            if (accountNo.Length <= 4)
                return "****";

            return new string('*', accountNo.Length - 4)
                   + accountNo[^4..];
        }





        public async Task<BREBPaymentSubmitResponse?> SubmitPaymentAsync(
            int branchId,
            int transactionId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "BREB Payment Submit started. BranchId: {BranchId}, TransactionId: {TransactionId}",
                branchId,
                transactionId);

            try
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
                    _logger.LogWarning(
                        "BREB Payment transaction not found. BranchId: {BranchId}, TransactionId: {TransactionId}",
                        branchId,
                        transactionId);

                    throw new Exception(
                        "Payment transaction could not be found.");
                }

                _logger.LogInformation(
                    "BREB Payment transaction found. TransactionId: {TransactionId}, SMSAccountNo: {SMSAccountNo}, CurrentStatus: {CurrentStatus}",
                    transactionId,
                    MaskAccountNo(transaction.SMSAccountNo),
                    transaction.TxnStatus);

                if (transaction.TxnStatus != "BillFound")
                {
                    _logger.LogWarning(
                        "BREB Payment rejected due to invalid status. TransactionId: {TransactionId}, Status: {Status}",
                        transactionId,
                        transaction.TxnStatus);

                    throw new Exception(
                        "This bill is not available for payment.");
                }

                // TEMPORARY TEST MODE
                // No real BREB API payment is performed.

                _logger.LogInformation(
                    "BREB TEST MODE payment submission processing. TransactionId: {TransactionId}",
                    transactionId);

                transaction.TxnStatus =
                    "WaitingForAcknowledgement";

                transaction.UpdatedBy =
                    "SYSTEM";

                transaction.UpdatedOn =
                    DateTime.Now;

                await _context.SaveChangesAsync(
                    cancellationToken);

                _logger.LogInformation(
                    "BREB Payment submitted successfully. BranchId: {BranchId}, TransactionId: {TransactionId}, Status: {Status}",
                    branchId,
                    transactionId,
                    transaction.TxnStatus);

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
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "BREB Payment Submit cancelled. BranchId: {BranchId}, TransactionId: {TransactionId}",
                    branchId,
                    transactionId);

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "BREB Payment Submit failed. BranchId: {BranchId}, TransactionId: {TransactionId}",
                    branchId,
                    transactionId);

                throw;
            }
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
            _logger.LogInformation(
                "BREB Payment Acknowledgement started. BranchId: {BranchId}, TransactionId: {TransactionId}",
                branchId,
                transactionId);

            try
            {
                var transaction = await _context.BREBTransactions
                    .FirstOrDefaultAsync(
                        x => x.Id == transactionId &&
                             x.BranchId == branchId,
                        cancellationToken);

                if (transaction == null)
                {
                    _logger.LogWarning(
                        "BREB Acknowledgement failed. Transaction not found. BranchId: {BranchId}, TransactionId: {TransactionId}",
                        branchId,
                        transactionId);

                    throw new Exception(
                        "Payment transaction could not be found.");
                }

                var previousStatus = transaction.TxnStatus;

                _logger.LogInformation(
                    "BREB Acknowledgement transaction found. BranchId: {BranchId}, TransactionId: {TransactionId}, PreviousStatus: {PreviousStatus}",
                    branchId,
                    transactionId,
                    previousStatus);

                if (transaction.TxnStatus == "Success")
                {
                    _logger.LogWarning(
                        "BREB Acknowledgement rejected. Payment already acknowledged. TransactionId: {TransactionId}",
                        transactionId);

                    throw new Exception(
                        "This payment has already been acknowledged.");
                }

                if (transaction.TxnStatus != "WaitingForAcknowledgement")
                {
                    _logger.LogWarning(
                        "BREB Acknowledgement rejected. Invalid status: {Status}. TransactionId: {TransactionId}",
                        transaction.TxnStatus,
                        transactionId);

                    throw new Exception(
                        "Only payments waiting for acknowledgement can be acknowledged.");
                }

                // TEST MODE ONLY: No real BREB API call.
                transaction.TxnStatus = "Success";
                transaction.UpdatedBy = username ?? "TEST_SYSTEM";
                transaction.UpdatedOn = DateTime.Now;

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "BREB Payment Acknowledgement successful. BranchId: {BranchId}, TransactionId: {TransactionId}, PreviousStatus: {PreviousStatus}, NewStatus: {NewStatus}, UpdatedBy: {UpdatedBy}",
                    branchId,
                    transactionId,
                    previousStatus,
                    transaction.TxnStatus,
                    transaction.UpdatedBy);

                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "BREB Payment Acknowledgement cancelled. BranchId: {BranchId}, TransactionId: {TransactionId}",
                    branchId,
                    transactionId);

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "BREB Payment Acknowledgement error. BranchId: {BranchId}, TransactionId: {TransactionId}",
                    branchId,
                    transactionId);

                throw;
            }
        }




        public async Task<List<BREBTransaction>> GetCollectionListAsync(
       int branchId,
       CancellationToken cancellationToken = default)
        {
            return await _context.BREBTransactions
                .Where(x => x.BranchId == branchId)
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

        private static readonly Dictionary<string, (string Name, decimal Amount, decimal Vat)>
    TestAccounts = new()
{
    { "1234567890", ("Test Customer 1", 1500m, 75m) },
    { "1234567891", ("Test Customer 2", 2000m, 100m) },
    { "1234567892", ("Test Customer 3", 2500m, 125m) },

    // নতুন SMS Account Number এখানে যোগ করবেন
    { "1234567893", ("Test Customer 4", 1800m, 90m) },
      { "1234567894", ("Test Customer 3", 2500m, 125m) },
        { "1234567895", ("Test Customer 3", 2500m, 125m) },
        { "1234567896", ("Test Customer 4", 1800m, 90m) },
      { "1234567897", ("Test Customer 3", 2500m, 125m) },
        { "1234567898", ("Test Customer 3", 2500m, 125m) }
};
    }








}