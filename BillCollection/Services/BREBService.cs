using BillCollection.Interfaces;
using BillCollection.Models;
using BillCollection.Models.BREB;
using BillCollection.ViewModels.BREB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BillCollection.Services
{
    public class BREBService : IBREBService
    {
        // Local transaction statuses
        private const string StBillFound = "BillFound";
        private const string StWaiting = "WaitingForAcknowledgement";
        private const string StSuccess = "Success";
        private const string StFailed = "Failed";
        // Outcome unknown / acknowledgement window missed -> must be reconciled (BREB Dispute API)
        private const string StPending = "Pending";

        // PayerTypeIds that identify an agent (AgentId is then mandatory)
        private static readonly HashSet<int> AgentPayerTypes = new() { 1, 2, 8, 10, 11 };

        private readonly IBREBApiClient _api;
        private readonly BREBOptions _options;
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<BREBService> _logger;

        public BREBService(
            IBREBApiClient api,
            IOptions<BREBOptions> options,
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<BREBService> logger)
        {
            _api = api;
            _options = options.Value;
            _context = context;
            _cache = cache;
            _logger = logger;

            _logger.LogInformation(
                _options.UseMock
                    ? "BREB Service initialized in MOCK mode. No call will be made to BREB."
                    : "BREB Service initialized in LIVE mode. BaseUrl: {BaseUrl}, PayerTypeId: {PayerTypeId}",
                _options.BaseUrl, _options.PayerTypeId);
        }

        public bool IsMockMode => _options.UseMock;

        // =====================================================================
        // 1. BILL QUERY  (/billinginfo)
        // =====================================================================
        public async Task<BREBBillingInfoResponse?> GetBillingInfoAsync(
            int branchId,
            BREBBillingInfoRequest request,
            CancellationToken cancellationToken = default)
        {
            var branch = await _context.Branches
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == branchId, cancellationToken)
                ?? throw new Exception("Branch information could not be found.");

            if (string.IsNullOrWhiteSpace(request.SMSAccountNo))
                throw new Exception("SMS Account Number is required.");

            request.SMSAccountNo = request.SMSAccountNo.Trim();

            BREBBillingInfoResponse? bill;

            if (_options.UseMock)
            {
                bill = BuildMockBill(request.SMSAccountNo);
                if (bill == null)
                {
                    _logger.LogWarning("BREB MOCK: no test bill. BranchId: {BranchId}, Account: {AccountNo}",
                        branchId, MaskAccountNo(request.SMSAccountNo));
                    return null;
                }
            }
            else
            {
                // Values that must NOT come from the browser
                request.PayerTypeId = _options.PayerTypeId;
                request.BranchRoutingNo = branch.RoutingNumber;
                request.AgentId = AgentPayerTypes.Contains(_options.PayerTypeId) ? _options.AgentId : null;

                if (string.IsNullOrWhiteSpace(request.PayerId))
                    throw new Exception("Payer Id (logged-in user) is required.");
                if (string.IsNullOrWhiteSpace(request.BranchRoutingNo))
                    throw new Exception("Routing number is not configured for this branch.");
                if (AgentPayerTypes.Contains(request.PayerTypeId) && string.IsNullOrWhiteSpace(request.AgentId))
                    throw new Exception("Agent Id is required for the configured Payer Type.");

                var response = await _api.BillingInfoAsync(request, cancellationToken);

                if (!response.IsSuccess || response.Data == null)
                {
                    _logger.LogWarning("BREB billinginfo rejected. BranchId: {BranchId}, Account: {AccountNo}, Reason: {Reason}",
                        branchId, MaskAccountNo(request.SMSAccountNo), response.ErrorText);
                    throw new BREBApiException(response.ErrorText, response.Status, response.Errors);
                }

                bill = response.Data;
                bill.SMSAccountNo ??= request.SMSAccountNo;
            }

            // Shown on the bill page and used when the payment is saved (like the old BKB project).
            bill.PayerTransactionId = await GeneratePayerTransactionIdAsync(cancellationToken);

            // Remember the bill for the "Pay" click: BREB locks a bill for ExpiresIn minutes,
            // so we must not call /billinginfo a second time for the same payment.
            var minutes = int.TryParse(bill.ExpiresIn, out var m) && m > 0 ? m : 5;
            _cache.Set(BillCacheKey(branchId, request.PayerId, request.SMSAccountNo),
                bill, TimeSpan.FromMinutes(minutes));

            _logger.LogInformation(
                "BREB bill retrieved. BranchId: {BranchId}, Account: {AccountNo}, RefNo: {RefNo}, Amount: {Amount}",
                branchId, MaskAccountNo(request.SMSAccountNo), bill.RefNo, bill.Amount);

            return bill;
        }

        // =====================================================================
        // 2. ONE-CLICK PAY  (save -> paymentsubmit -> paymentacknowledgment)
        // =====================================================================
        public async Task<BREBPayResult> PayBillAsync(
            int branchId,
            string smsAccountNo,
            string? username,
            CancellationToken cancellationToken = default)
        {
            var key = BillCacheKey(branchId, username, smsAccountNo?.Trim());

            if (!_cache.TryGetValue(key, out BREBBillingInfoResponse? bill) || bill == null)
                throw new Exception("The bill session has expired. Please query the bill again.");

            var transactionId = await SaveBillingTransactionAsync(branchId, bill, username, cancellationToken);

            // Whatever happens next, this bill must not be paid twice from the same cached copy.
            _cache.Remove(key);

            var tx = await _context.BREBTransactions.AsNoTracking()
                .FirstAsync(x => x.Id == transactionId, cancellationToken);

            var result = new BREBPayResult
            {
                TransactionId = transactionId,
                PayerTransactionId = tx.PayerTransactionId
            };

            var submit = await SubmitPaymentAsync(branchId, transactionId, cancellationToken);
            result.Submitted = submit is { Success: true };

            if (!result.Submitted)
            {
                result.Message = submit?.Message ?? "Payment submission failed.";
                return result;
            }

            if (!_options.AutoAcknowledge)
            {
                // Two-step flow: the teller acknowledges after the cash is received.
                result.AwaitingAcknowledge = true;
                result.Message =
                    "Payment submitted. Waiting for acknowledgement" +
                    (string.IsNullOrEmpty(submit!.ExpiresAt)
                        ? ". Please acknowledge from the Collection List."
                        : " - please acknowledge before " + submit.ExpiresAt + ".");
                return result;
            }

            try
            {
                result.Acknowledged = await AcknowledgePaymentAsync(branchId, transactionId, username, cancellationToken);
                result.Message = "Payment completed successfully.";
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Payment IS submitted at BREB; only the acknowledgement is missing.
                // The user can retry "Acknowledge" from the Collection List until ExpiresAt.
                _logger.LogError(ex, "BREB acknowledge failed after submit. TransactionId: {TransactionId}", transactionId);
                result.Acknowledged = false;
                result.Message =
                    "Payment was submitted but NOT acknowledged yet (" + ex.Message + "). " +
                    "Please retry Acknowledge from the Collection List" +
                    (string.IsNullOrEmpty(submit!.ExpiresAt) ? "." : " before " + submit.ExpiresAt + ".");
            }

            return result;
        }

        // =====================================================================
        // 3. PAYMENT SUBMIT  (/billinginfo/paymentsubmit)
        // =====================================================================
        public async Task<BREBPaymentSubmitResponse?> SubmitPaymentAsync(
            int branchId,
            int transactionId,
            CancellationToken cancellationToken = default)
        {
            var transaction = await _context.BREBTransactions
                .FirstOrDefaultAsync(x => x.Id == transactionId && x.BranchId == branchId, cancellationToken)
                ?? throw new Exception("Payment transaction could not be found.");

            if (transaction.TxnStatus != StBillFound)
                throw new Exception("This bill is not available for payment.");

            if (_options.UseMock)
            {
                transaction.TxnStatus = StWaiting;
                transaction.UpdatedBy = "SYSTEM";
                transaction.UpdatedOn = DateTime.Now;
                await _context.SaveChangesAsync(cancellationToken);

                return new BREBPaymentSubmitResponse
                {
                    SMSAccountNo = transaction.SMSAccountNo,
                    RefNo = transaction.RefNo,
                    PayerTransactionId = transaction.PayerTransactionId,
                    ExpiresAt = DateTime.Now.AddMinutes(5).ToString("yyyy-MM-dd HH:mm:ss"),
                    Message = "Payment submitted successfully. Waiting for acknowledgment.",
                    Success = true
                };
            }

            var request = new BREBPaymentSubmitRequest
            {
                SMSAccountNo = transaction.SMSAccountNo ?? string.Empty,
                RefNo = transaction.RefNo ?? string.Empty,
                PayerTransactionId = transaction.PayerTransactionId,
                Amount = transaction.Amount
            };

            BREBApiResponse<BREBPaymentSubmitResponse> response;
            try
            {
                response = await _api.PaymentSubmitAsync(request, cancellationToken);
            }
            catch (BREBApiException ex) when (ex.OutcomeUnknown)
            {
                // Timeout / 5xx: BREB may have recorded the payment. Do NOT mark it failed
                // and do NOT retry blindly -> it must be checked with /paymentstatus.
                transaction.TxnStatus = StPending;
                transaction.Message = Trim("Submit outcome unknown: " + ex.Message, 1000);
                transaction.UpdatedBy = transaction.CreatedBy;
                transaction.UpdatedOn = DateTime.Now;
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogError(ex, "BREB submit outcome unknown. TransactionId: {TransactionId}", transactionId);
                throw new Exception(
                    "No confirmation received from BREB. The payment is marked Pending and must be verified " +
                    "before any retry.");
            }

            if (response.IsSuccess && response.Data != null)
            {
                transaction.TxnStatus = StWaiting;
                transaction.Message = Trim(response.Data.Message, 1000);
                transaction.UpdatedBy = transaction.CreatedBy;
                transaction.UpdatedOn = DateTime.Now;
                await _context.SaveChangesAsync(cancellationToken);

                response.Data.Success = true;
                return response.Data;
            }

            // Business rejection: 202.1 already paid, 202.2 already submitted, 400.2 amount mismatch,
            // 400.3 duplicate PayerTransactionId, 404.1 RefNo expired ...
            transaction.TxnStatus = StFailed;
            transaction.Message = Trim(response.ErrorText, 1000);
            transaction.UpdatedBy = transaction.CreatedBy;
            transaction.UpdatedOn = DateTime.Now;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("BREB submit rejected. TransactionId: {TransactionId}, Reason: {Reason}",
                transactionId, response.ErrorText);

            return new BREBPaymentSubmitResponse { Success = false, Message = response.ErrorText };
        }

        // =====================================================================
        // 4. SAVE LOCAL TRANSACTION
        // =====================================================================
        public async Task<int> SaveBillingTransactionAsync(
            int branchId,
            BREBBillingInfoResponse billingInfo,
            string? username,
            CancellationToken cancellationToken = default)
        {
            if (billingInfo == null) throw new Exception("Bill information is required.");
            if (string.IsNullOrWhiteSpace(billingInfo.SMSAccountNo)) throw new Exception("SMS Account Number is required.");
            if (string.IsNullOrWhiteSpace(billingInfo.RefNo)) throw new Exception("BREB Reference Number is required.");

            DateTime? paymentTimestamp = DateTime.TryParse(billingInfo.PaymentTimestamp, out var parsed)
                ? parsed : null;

            var payerTxnId = billingInfo.PayerTransactionId;
            if (string.IsNullOrWhiteSpace(payerTxnId) ||
                await _context.BREBTransactions.AnyAsync(x => x.PayerTransactionId == payerTxnId, cancellationToken))
                payerTxnId = await GeneratePayerTransactionIdAsync(cancellationToken);

            var transaction = new BREBTransaction
            {
                BranchId = branchId,
                PayerTransactionId = payerTxnId,
                RefNo = billingInfo.RefNo,                 // opaque string, NOT necessarily a GUID
                PaymentTimestamp = paymentTimestamp,
                SMSAccountNo = billingInfo.SMSAccountNo,
                AccountName = billingInfo.AccountName,
                DueType = billingInfo.DueType,
                BillMonth = billingInfo.BillMonth,
                Amount = billingInfo.Amount,
                Vat = billingInfo.Vat,
                Message = billingInfo.Message,
                ExpiresIn = billingInfo.ExpiresIn,
                TxnStatus = StBillFound,
                CreatedBy = username,
                CreatedOn = DateTime.Now
            };

            _context.BREBTransactions.Add(transaction);
            await _context.SaveChangesAsync(cancellationToken);
            return transaction.Id;
        }

        /// <summary>
        /// Same format BKBUtility has been using successfully with BREB:
        /// ddMMyyHHmmssfff (15 digits), e.g. 121222101436251.
        /// Must be unique (BREB answers 400.3 otherwise) -> checked against the DB;
        /// add the unique index from breb_live_migration.sql for full protection.
        /// </summary>
        private async Task<string> GeneratePayerTransactionIdAsync(CancellationToken ct)
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var id = DateTime.Now.ToString("ddMMyyHHmmssfff");
                var exists = await _context.BREBTransactions
                    .AnyAsync(x => x.PayerTransactionId == id, ct);
                if (!exists) return id;
                await Task.Delay(3, ct);
            }
            throw new Exception("Could not generate a unique Payer Transaction Id. Please try again.");
        }

        // =====================================================================
        // 5. ACKNOWLEDGE  (/billinginfo/paymentacknowledgment)
        // =====================================================================
        public async Task<bool> AcknowledgePaymentAsync(
            int branchId,
            int transactionId,
            string? username,
            CancellationToken cancellationToken = default)
        {
            var transaction = await _context.BREBTransactions
                .FirstOrDefaultAsync(x => x.Id == transactionId && x.BranchId == branchId, cancellationToken)
                ?? throw new Exception("Payment transaction could not be found.");

            var previousStatus = transaction.TxnStatus;

            if (transaction.TxnStatus == StSuccess)
                throw new Exception("This payment has already been acknowledged.");

            if (transaction.TxnStatus != StWaiting)
                throw new Exception("Only payments waiting for acknowledgement can be acknowledged.");

            if (_options.UseMock)
            {
                transaction.TxnStatus = StSuccess;
                transaction.UpdatedBy = username ?? "TEST_SYSTEM";
                transaction.UpdatedOn = DateTime.Now;
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            }

            var request = new BREBPaymentAcknowledgmentRequest
            {
                SMSAccountNo = transaction.SMSAccountNo ?? string.Empty,
                RefNo = transaction.RefNo ?? string.Empty,
                PayerTransactionId = transaction.PayerTransactionId
            };

            BREBApiResponse<BREBPaymentAcknowledgmentData> response;
            try
            {
                response = await _api.PaymentAcknowledgeAsync(request, cancellationToken);
            }
            catch (BREBApiException ex) when (ex.OutcomeUnknown)
            {
                // keep "Waiting": user may retry acknowledge (it is idempotent on BREB side: 202.1 if done)
                _logger.LogError(ex, "BREB acknowledge outcome unknown. TransactionId: {TransactionId}", transactionId);
                throw new Exception("No confirmation received from BREB. Please retry Acknowledge shortly.");
            }

            if (response.IsSuccess)
            {
                transaction.TxnStatus = StSuccess;
                transaction.Message = Trim(response.Data?.Message ?? "Success", 1000);
                transaction.UpdatedBy = username ?? transaction.CreatedBy;
                transaction.UpdatedOn = DateTime.Now;
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "BREB acknowledged. TransactionId: {TransactionId}, {Prev} -> {New}",
                    transactionId, previousStatus, transaction.TxnStatus);
                return true;
            }

            if (response.HasError("202.2"))
            {
                // Acknowledgement window is over -> only the Dispute API can settle it now.
                transaction.TxnStatus = StPending;
                transaction.Message = Trim(response.ErrorText, 1000);
                transaction.UpdatedBy = username ?? transaction.CreatedBy;
                transaction.UpdatedOn = DateTime.Now;
                await _context.SaveChangesAsync(cancellationToken);

                throw new Exception(
                    "Acknowledgement time is over. The payment is marked Pending and needs manual reconciliation.");
            }

            throw new BREBApiException(response.ErrorText, response.Status, response.Errors);
        }

        // =====================================================================
        // MOCK DATA + HELPERS
        // =====================================================================
        private static BREBBillingInfoResponse? BuildMockBill(string smsAccountNo)
        {
            if (!TestAccounts.TryGetValue(smsAccountNo, out var account)) return null;

            return new BREBBillingInfoResponse
            {
                SMSAccountNo = smsAccountNo,
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
        }

        private static string BillCacheKey(int branchId, string? payerId, string? smsAccountNo) =>
            $"breb-bill|{branchId}|{payerId}|{smsAccountNo}";

        private static string Trim(string? value, int max) =>
            string.IsNullOrEmpty(value) ? string.Empty : (value.Length <= max ? value : value[..max]);

        private static string MaskAccountNo(string? accountNo)
        {
            if (string.IsNullOrWhiteSpace(accountNo)) return "N/A";
            if (accountNo.Length <= 4) return "****";
            return new string('*', accountNo.Length - 4) + accountNo[^4..];
        }

        // =====================================================================
        // 6. PAYMENT STATUS + SYNC  (/billinginfo/paymentstatus)
        // =====================================================================
        private const string StDispute = "Dispute";

        private static string BrebStatusText(string? code) => code switch
        {
            "8" => "Reset Successful",
            "9" => "Submitted for Dispute",
            "10" => "Pending",
            "30" => "Waiting for Acknowledge",
            "50" => "Paid Acknowledged",
            _ => string.IsNullOrWhiteSpace(code) ? "Unknown" : code
        };

        private async Task<BREBTransaction> GetOwnTransactionAsync(
            int branchId, int transactionId, CancellationToken ct) =>
            await _context.BREBTransactions
                .FirstOrDefaultAsync(x => x.Id == transactionId && x.BranchId == branchId, ct)
            ?? throw new Exception("Payment transaction could not be found.");

        private static void SetStatus(BREBTransaction tx, string status, string message, string? username)
        {
            tx.TxnStatus = status;
            tx.Message = Trim(message, 1000);
            tx.UpdatedBy = Trim(username ?? tx.CreatedBy ?? "SYSTEM", 100);
            tx.UpdatedOn = DateTime.Now;
        }

        private async Task<BREBPaymentStatusData> QueryPaymentStatusAsync(
            BREBTransaction tx, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(tx.RefNo))
                throw new Exception("This transaction has no BREB reference number.");

            if (_options.UseMock)
            {
                var code = tx.TxnStatus switch
                {
                    StSuccess => "50",
                    StWaiting => "30",
                    StDispute => "9",
                    _ => "10"
                };

                return new BREBPaymentStatusData
                {
                    SMSAccountNo = tx.SMSAccountNo,
                    RefNo = tx.RefNo,
                    PayerTransactionId = tx.PayerTransactionId,
                    Status = code
                };
            }

            var response = await _api.PaymentStatusAsync(new BREBPaymentStatusRequest
            {
                SMSAccountNo = tx.SMSAccountNo ?? string.Empty,
                RefNo = tx.RefNo,
                PayerTransactionId = tx.PayerTransactionId
            }, ct);

            if (!response.IsSuccess || response.Data == null)
                throw new BREBApiException(response.ErrorText, response.Status, response.Errors);

            return response.Data;
        }

        public async Task<string> SyncPaymentStatusAsync(
            int branchId, int transactionId, string? username, CancellationToken cancellationToken = default)
        {
            var tx = await GetOwnTransactionAsync(branchId, transactionId, cancellationToken);
            var before = tx.TxnStatus;

            BREBPaymentStatusData data;
            try
            {
                data = await QueryPaymentStatusAsync(tx, cancellationToken);
            }
            catch (BREBApiException ex) when (ex.Status == 404 && tx.TxnStatus == StPending)
            {
                // Outcome-unknown payment that BREB has never heard of -> it did not go through.
                SetStatus(tx, StFailed, "Not found at BREB: " + ex.Message, username);
                await _context.SaveChangesAsync(cancellationToken);
                return "BREB has no record of this payment, so it is marked Failed. " +
                       "The bill can be queried and paid again.";
            }

            var code = data.Status?.Trim();
            var text = BrebStatusText(code);
            string? newStatus = null;
            string advice;

            switch (code)
            {
                case "50":
                    if (tx.TxnStatus != StSuccess) newStatus = StSuccess;
                    advice = tx.TxnStatus == StSuccess ? "Both sides agree." : "Local record updated to match BREB.";
                    break;

                case "30":
                    if (tx.TxnStatus == StPending) newStatus = StWaiting;
                    advice = "Acknowledge it from the Collection List. If the acknowledgement time is over, " +
                             "submit a Dispute (Acknowledge) from the Reconcile Report.";
                    break;

                case "9":
                    if (tx.TxnStatus != StSuccess && tx.TxnStatus != StDispute) newStatus = StDispute;
                    advice = "BREB is processing the dispute. Check again later.";
                    break;

                case "8":
                    if (tx.TxnStatus is StPending or StWaiting or StDispute) newStatus = StFailed;
                    advice = "The payment was reset at BREB. The bill can be queried and paid again.";
                    break;

                case "10":
                    if (tx.TxnStatus == StPending) newStatus = StFailed;
                    advice = "BREB has not received a payment for this bill.";
                    break;

                default:
                    advice = "Unrecognised status code. No change was made.";
                    break;
            }

            if (newStatus != null)
            {
                SetStatus(tx, newStatus, "Synced with BREB: " + text + ".", username);
                await _context.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation(
                "BREB status sync. TransactionId: {TransactionId}, BREB: {Brebs}, Local: {Before} -> {After}",
                transactionId, text, before, tx.TxnStatus);

            var local = newStatus == null ? $"{before} (unchanged)" : $"{before} -> {tx.TxnStatus}";
            return $"BREB status: {text}. Local: {local}. {advice}";
        }

        // =====================================================================
        // 7. DISPUTE  (/billinginfo/dispute)   5 = Acknowledge, 10 = Reset
        // =====================================================================
        public async Task<string> SubmitDisputeAsync(
            int branchId, int transactionId, int disputeType, string? username,
            CancellationToken cancellationToken = default)
        {
            if (disputeType != 5 && disputeType != 10)
                throw new Exception("Invalid dispute type.");

            var tx = await GetOwnTransactionAsync(branchId, transactionId, cancellationToken);

            if (tx.TxnStatus == StDispute)
                throw new Exception("A dispute has already been submitted for this payment.");
            if (tx.TxnStatus == StBillFound)
                throw new Exception("This bill was never submitted, so there is nothing to dispute.");
            if (string.IsNullOrWhiteSpace(tx.RefNo))
                throw new Exception("This transaction has no BREB reference number.");

            string message;

            if (_options.UseMock)
            {
                message = "Received Successfully (test mode).";
            }
            else
            {
                BREBApiResponse<BREBDisputeData> response;
                try
                {
                    response = await _api.DisputeAsync(new BREBDisputeRequest
                    {
                        SMSAccountNo = tx.SMSAccountNo ?? string.Empty,
                        RefNo = tx.RefNo,
                        PayerTransactionId = tx.PayerTransactionId,
                        DisputeType = disputeType
                    }, cancellationToken);
                }
                catch (BREBApiException ex) when (ex.OutcomeUnknown)
                {
                    throw new Exception(
                        "No confirmation received from BREB. Use Check & Sync before submitting the dispute again.");
                }

                if (response.IsSuccess)
                {
                    message = response.Data?.Message ?? "Received Successfully.";
                }
                else if (response.HasError("202.1"))
                {
                    message = "BREB already has a dispute for this payment.";
                }
                else if (response.ErrorText.Contains("Already Acknowledged", StringComparison.OrdinalIgnoreCase))
                {
                    SetStatus(tx, StSuccess, "BREB reports: Already Acknowledged.", username);
                    await _context.SaveChangesAsync(cancellationToken);
                    return "BREB says this payment is already acknowledged. Local record updated to Success.";
                }
                else
                {
                    // 202.4 "Bill Payment is in progress. Please try after: ...", 400.2 "Request can't be
                    // done after payment date", 404.1 ... -> shown to the user as is.
                    throw new BREBApiException(response.ErrorText, response.Status, response.Errors);
                }
            }

            tx.DisputeType = disputeType.ToString();
            tx.DisputedBy = Trim(username, 100);
            tx.DisputedOn = DateTime.Now;
            SetStatus(tx, StDispute,
                $"Dispute ({(disputeType == 5 ? "Acknowledge" : "Reset")}) submitted: {message}", username);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "BREB dispute submitted. TransactionId: {TransactionId}, Type: {Type}, By: {User}",
                transactionId, disputeType, username);

            return $"Dispute ({(disputeType == 5 ? "Acknowledge" : "Reset")}) submitted. {message}";
        }

        // =====================================================================
        // 8. RECONCILE  (local day  vs  BREB /report/detail)
        // =====================================================================
        public async Task<BREBReconcileReportViewModel> GetReconcileReportAsync(
            int branchId, DateTime date, CancellationToken cancellationToken = default)
        {
            var day = date.Date;

            var locals = await _context.BREBTransactions
                .AsNoTracking()
                .Where(x => x.BranchId == branchId && x.CreatedOn >= day && x.CreatedOn < day.AddDays(1))
                .OrderByDescending(x => x.CreatedOn)
                .ToListAsync(cancellationToken);

            var vm = new BREBReconcileReportViewModel
            {
                ReportDate = day,
                IsMockMode = _options.UseMock,
                Transactions = locals,
                TotalTransactions = locals.Count,
                SuccessfulPayments = locals.Count(x => x.TxnStatus == StSuccess),
                PendingPayments = locals.Count(x => x.TxnStatus == StWaiting || x.TxnStatus == StPending),
                FailedPayments = locals.Count(x => x.TxnStatus == StFailed),
                TotalSuccessfulAmount = locals.Where(x => x.TxnStatus == StSuccess).Sum(x => x.Amount)
            };

            var brebPaid = new List<BREBReportDetailEntity>();
            var brebWaiting = new List<BREBReportDetailEntity>();

            if (_options.UseMock)
            {
                BREBReportDetailEntity ToEntity(BREBTransaction t) => new()
                {
                    SMSAccountNo = t.SMSAccountNo,
                    RefNo = t.RefNo,
                    PayerTransactionId = t.PayerTransactionId,
                    BillType = "50",
                    Amount = t.Amount,
                    Vat = t.Vat
                };

                brebPaid = locals.Where(x => x.TxnStatus == StSuccess).Select(ToEntity).ToList();
                brebWaiting = locals.Where(x => x.TxnStatus == StWaiting).Select(ToEntity).ToList();
            }
            else
            {
                try
                {
                    var routing = await _context.Branches.AsNoTracking()
                        .Where(x => x.Id == branchId)
                        .Select(x => x.RoutingNumber)
                        .FirstOrDefaultAsync(cancellationToken);

                    brebPaid = await GetBrebDetailAsync(day, 50, routing, cancellationToken);
                    brebWaiting = await GetBrebDetailAsync(day, 30, routing, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    vm.BrebError = ex.Message;
                    _logger.LogError(ex, "BREB reconcile: could not load BREB report. BranchId: {BranchId}", branchId);
                }
            }

            vm.BrebPaidCount = brebPaid.Count;
            vm.BrebPaidAmount = brebPaid.Sum(x => x.Amount);
            vm.BrebWaitingCount = brebWaiting.Count;

            if (vm.BrebError != null) return vm;

            var remote = brebPaid.Select(e => (Entity: e, Code: "50"))
                .Concat(brebWaiting.Select(e => (Entity: e, Code: "30")))
                .ToList();

            var used = new HashSet<int>();

            foreach (var local in locals)
            {
                var index = remote.FindIndex(r =>
                    (!string.IsNullOrEmpty(local.PayerTransactionId) &&
                     r.Entity.PayerTransactionId == local.PayerTransactionId) ||
                    (!string.IsNullOrEmpty(local.RefNo) && r.Entity.RefNo == local.RefNo));

                string? code = null;
                if (index >= 0)
                {
                    used.Add(index);
                    code = remote[index].Code;
                }

                vm.Rows.Add(BuildReconcileRow(local, code));
            }

            for (var i = 0; i < remote.Count; i++)
            {
                if (used.Contains(i)) continue;
                var e = remote[i].Entity;

                vm.Rows.Add(new BREBReconcileRow
                {
                    SMSAccountNo = e.SMSAccountNo,
                    RefNo = e.RefNo,
                    PayerTransactionId = e.PayerTransactionId,
                    Amount = e.Amount,
                    Vat = e.Vat,
                    LocalStatus = null,
                    BrebStatus = BrebStatusText(remote[i].Code),
                    Result = "Only at BREB",
                    Note = "BREB has this payment but this branch has no matching local record for the day " +
                           "(another user/channel, or a payment made around midnight)."
                });
            }

            vm.MatchedCount = vm.Rows.Count(r => r.Result == "Matched");
            vm.MismatchCount = vm.Rows.Count - vm.MatchedCount;
            return vm;
        }

        private async Task<List<BREBReportDetailEntity>> GetBrebDetailAsync(
            DateTime day, int status, string? routingNo, CancellationToken ct)
        {
            var response = await _api.ReportDetailAsync(new BREBReportDetailRequest
            {
                CollectionDate = day.ToString("yyyy-MM-dd"),
                CollectionStatus = status,
                BranchRoutingNo = routingNo
            }, ct);

            if (response.Status == 404) return new List<BREBReportDetailEntity>();   // "Information not found."

            if (!response.IsSuccess || response.Data == null)
                throw new BREBApiException(response.ErrorText, response.Status, response.Errors);

            return response.Data.Entities ?? new List<BREBReportDetailEntity>();
        }

        private static BREBReconcileRow BuildReconcileRow(BREBTransaction l, string? code)
        {
            var row = new BREBReconcileRow
            {
                TransactionId = l.Id,
                SMSAccountNo = l.SMSAccountNo,
                RefNo = l.RefNo,
                PayerTransactionId = l.PayerTransactionId,
                Amount = l.Amount,
                Vat = l.Vat,
                LocalStatus = l.TxnStatus,
                BrebStatus = code == null ? "Not found" : BrebStatusText(code)
            };

            var s = l.TxnStatus;

            if (code == "50" && s == StSuccess)
            {
                row.Result = "Matched";
                return row;
            }

            if (code == null && (s == StFailed || s == StBillFound))
            {
                row.Result = "Matched";
                row.Note = s == StBillFound ? "Bill queried only, never submitted." : "Not paid on either side.";
                return row;
            }

            row.Result = "Mismatch";
            row.CanSync = true;
            row.CanDispute = code == "30" && s != StDispute;

            row.Note = (s, code) switch
            {
                (StSuccess, "30") => "Local says paid, but BREB is still waiting for the acknowledgement. Submit a Dispute (Acknowledge).",
                (StSuccess, null) => "Local says paid, but BREB has no payment for this date. Check & Sync, and verify the date.",
                (StWaiting or StPending, "50") => "BREB shows it as paid. Check & Sync updates the local record.",
                (StWaiting or StPending, "30") => "Waiting at BREB. Acknowledge it, or submit a Dispute (Acknowledge) if the time is over.",
                (StWaiting or StPending, null) => "BREB has no record of this payment. Check & Sync.",
                (StDispute, _) => "Dispute in progress. Check & Sync to see BREB's decision.",
                (StFailed, "50") => "Local says failed, but BREB shows it as paid. Check & Sync.",
                (StFailed, "30") => "Local says failed, but BREB holds it. If the customer's cash was collected, submit a Dispute (Acknowledge); otherwise Reset.",
                _ => "Statuses differ. Use Check & Sync."
            };

            return row;
        }

        // =====================================================================
        // 9. SUMMARY  (local daily totals  vs  BREB /report/summary)
        // =====================================================================
        public async Task<BREBSummaryReportViewModel> GetSummaryReportAsync(
            int branchId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            from = from.Date;
            to = to.Date;
            if (to < from) (from, to) = (to, from);
            var end = to.AddDays(1);

            var localSuccess = await _context.BREBTransactions
                .AsNoTracking()
                .Where(x => x.BranchId == branchId && x.TxnStatus == StSuccess &&
                            x.CreatedOn >= from && x.CreatedOn < end)
                .ToListAsync(cancellationToken);

            var localByDay = localSuccess
                .GroupBy(x => x.CreatedOn.Date)
                .ToDictionary(g => g.Key, g => (Count: g.Count(), Amount: g.Sum(x => x.Amount), Vat: g.Sum(x => x.Vat)));

            var brebByDay = new Dictionary<DateTime, (int Count, decimal Amount, decimal Vat)>();

            var vm = new BREBSummaryReportViewModel { From = from, To = to, IsMockMode = _options.UseMock };

            if (_options.UseMock)
            {
                brebByDay = new Dictionary<DateTime, (int Count, decimal Amount, decimal Vat)>(localByDay);
            }
            else
            {
                try
                {
                    var routing = await _context.Branches.AsNoTracking()
                        .Where(x => x.Id == branchId)
                        .Select(x => x.RoutingNumber)
                        .FirstOrDefaultAsync(cancellationToken);

                    var response = await _api.ReportSummaryAsync(new BREBReportSummaryRequest
                    {
                        StartDate = from.ToString("yyyy-MM-dd"),
                        EndDate = to.ToString("yyyy-MM-dd"),
                        BranchRoutingNo = routing
                    }, cancellationToken);

                    if (response.Status != 404)
                    {
                        if (!response.IsSuccess || response.Data == null)
                            throw new BREBApiException(response.ErrorText, response.Status, response.Errors);

                        foreach (var e in response.Data.Entities)
                        {
                            if (!DateTime.TryParse(e.PaymentDate, out var d)) continue;
                            d = d.Date;

                            brebByDay[d] = brebByDay.TryGetValue(d, out var cur)
                                ? (cur.Count + e.NoOfBill, cur.Amount + e.Amount, cur.Vat + e.Vat)
                                : (e.NoOfBill, e.Amount, e.Vat);
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    vm.BrebError = ex.Message;
                    _logger.LogError(ex, "BREB summary: could not load BREB report. BranchId: {BranchId}", branchId);
                }
            }

            foreach (var d in localByDay.Keys.Union(brebByDay.Keys).OrderBy(x => x))
            {
                localByDay.TryGetValue(d, out var l);
                brebByDay.TryGetValue(d, out var b);

                vm.Rows.Add(new BREBSummaryRow
                {
                    Date = d,
                    LocalCount = l.Count,
                    LocalAmount = l.Amount,
                    LocalVat = l.Vat,
                    BrebCount = b.Count,
                    BrebAmount = b.Amount,
                    BrebVat = b.Vat
                });
            }

            return vm;
        }

        public async Task<BREBCollectionReportViewModel> GetCollectionReportAsync(
            int branchId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            from = from.Date;
            to = to.Date;
            if (to < from) (from, to) = (to, from);
            var end = to.AddDays(1);

            var items = await _context.BREBTransactions
                .AsNoTracking()
                .Where(x => x.BranchId == branchId && x.TxnStatus == StSuccess &&
                            x.CreatedOn >= from && x.CreatedOn < end)
                .OrderByDescending(x => x.CreatedOn)
                .ToListAsync(cancellationToken);

            return new BREBCollectionReportViewModel { From = from, To = to, Items = items };
        }

        public async Task<List<BREBTransaction>> GetCollectionListAsync(
            int branchId, DateTime date, CancellationToken cancellationToken = default)
        {
            var day = date.Date;
            var next = day.AddDays(1);

            return await _context.BREBTransactions
                .AsNoTracking()
                .Where(x => x.BranchId == branchId && x.CreatedOn >= day && x.CreatedOn < next)
                .OrderByDescending(x => x.CreatedOn)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<BREBTransaction>> GetUnsuccessfulListAsync(
            int branchId, DateTime? date, CancellationToken cancellationToken = default)
        {
            var unsuccessfulStatuses = new[]
            {
                StFailed, "Unsuccessful", StPending, StWaiting
            };

            var query = _context.BREBTransactions
                .AsNoTracking()
                .Where(x =>
                    x.BranchId == branchId &&
                    x.TxnStatus != null &&
                    unsuccessfulStatuses.Contains(x.TxnStatus));

            if (date.HasValue)
            {
                var day = date.Value.Date;
                var next = day.AddDays(1);
                query = query.Where(x => x.CreatedOn >= day && x.CreatedOn < next);
            }

            return await query.OrderByDescending(x => x.CreatedOn).ToListAsync(cancellationToken);
        }

        public async Task<List<BREBTransaction>> GetDisputeListAsync(
            int branchId, DateTime? date, CancellationToken cancellationToken = default)
        {
            var query = _context.BREBTransactions
                .AsNoTracking()
                .Where(x => x.BranchId == branchId && x.TxnStatus == StDispute);

            if (date.HasValue)
            {
                var day = date.Value.Date;
                var next = day.AddDays(1);
                query = query.Where(x => x.CreatedOn >= day && x.CreatedOn < next);
            }

            return await query.OrderByDescending(x => x.CreatedOn).ToListAsync(cancellationToken);
        }

        // Dispute form of the old project (Payer Transaction Id + SMS account + Acknowledge/Reset)
        public async Task<string> SubmitDisputeAsync(
            int branchId, string payerTransactionId, string smsAccountNo, int disputeType, string? username,
            CancellationToken cancellationToken = default)
        {
            var ptid = payerTransactionId?.Trim();
            var sms = smsAccountNo?.Trim();

            if (string.IsNullOrEmpty(ptid)) throw new Exception("Payer Transaction Id is required.");
            if (string.IsNullOrEmpty(sms)) throw new Exception("SMS Account Number is required.");

            var id = await _context.BREBTransactions
                .AsNoTracking()
                .Where(x => x.BranchId == branchId && x.PayerTransactionId == ptid && x.SMSAccountNo == sms)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (id == null)
                throw new Exception("No payment with this Transaction Id and SMS account was found in this branch.");

            return await SubmitDisputeAsync(branchId, id.Value, disputeType, username, cancellationToken);
        }

        public async Task<BREBReceiptViewModel?> GetReceiptAsync(
            int branchId, int transactionId, CancellationToken cancellationToken = default)
        {
            var tx = await _context.BREBTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == transactionId && x.BranchId == branchId && x.TxnStatus == StSuccess,
                    cancellationToken);
            if (tx == null) return null;

            var branch = await _context.Branches
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == branchId, cancellationToken);

            return new BREBReceiptViewModel
            {
                Transaction = tx,
                BranchName = branch?.Name ?? string.Empty,
                BranchCode = branch?.BranchCode ?? string.Empty
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