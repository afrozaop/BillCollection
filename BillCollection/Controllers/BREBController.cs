using BillCollection.Interfaces;
using BillCollection.Models.BREB;
using BillCollection.ViewModels.BREB;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BillCollection.Controllers
{
    [Authorize(Roles = "Branch")]
    public class BREBController : Controller
    {
        private readonly IBREBService _brebService;

        public BREBController(IBREBService brebService)
        {
            _brebService = brebService;
        }

        private string ModePrefix => _brebService.IsMockMode ? "TEST MODE: " : string.Empty;

        private bool TryGetBranchId(out int branchId) =>
            int.TryParse(User.FindFirst("BranchId")?.Value, out branchId);

        public IActionResult Index()
        {
            ViewBag.IsMockMode = _brebService.IsMockMode;
            return View();
        }

        // ---------------------------------------------------------------
        // BILL QUERY
        // ---------------------------------------------------------------
        [HttpGet]
        public IActionResult BillCollection()
        {
            ViewBag.IsMockMode = _brebService.IsMockMode;
            return View(new BREBBillCollectionViewModel { BillType = 50 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BillCollection(
            BREBBillCollectionViewModel model,
            CancellationToken cancellationToken)
        {
            ViewBag.IsMockMode = _brebService.IsMockMode;

            if (!ModelState.IsValid) return View(model);

            if (!TryGetBranchId(out var branchId))
                return RedirectToAction("AccessDenied", "Account");

            try
            {
                // BillType is fixed to Electricity Bill (50).
                // PayerTypeId / BranchRoutingNo / AgentId are filled by the service
                // from configuration + branch, never from the browser.
                var request = new BREBBillingInfoRequest
                {
                    SMSAccountNo = model.SMSAccountNo,
                    BillType = 50,
                    PayerId = User.Identity?.Name
                };

                var result = await _brebService.GetBillingInfoAsync(branchId, request, cancellationToken);

                if (result == null)
                {
                    ModelState.AddModelError("", "Billing information not found.");
                    return View(model);
                }

                ViewBag.BillResult = result;
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        // ---------------------------------------------------------------
        // PAY: save -> paymentsubmit -> paymentacknowledgment
        // (uses the bill queried a moment ago; no second /billinginfo call)
        // ---------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayBill(
            string smsAccountNo,
            CancellationToken cancellationToken)
        {
            if (!TryGetBranchId(out var branchId))
                return RedirectToAction("AccessDenied", "Account");

            if (string.IsNullOrWhiteSpace(smsAccountNo))
            {
                TempData["Error"] = "SMS Account Number is required.";
                return RedirectToAction(nameof(BillCollection));
            }

            try
            {
                var result = await _brebService.PayBillAsync(
                    branchId, smsAccountNo, User.Identity?.Name, cancellationToken);

                if (result.Submitted && result.Acknowledged)
                    TempData["Success"] = ModePrefix +
                        $"Payment completed. Transaction Id: {result.PayerTransactionId}";
                else if (result.Submitted && result.AwaitingAcknowledge)
                    TempData["Success"] = ModePrefix + result.Message;   // two-step flow: acknowledge next
                else if (result.Submitted)
                    TempData["Error"] = result.Message;   // submitted, auto-acknowledge failed
                else
                    TempData["Error"] = result.Message ?? "Payment submission failed.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(CollectionList));
        }

        // ---------------------------------------------------------------
        // MANUAL ACKNOWLEDGE (retry when auto-acknowledge failed)
        // ---------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcknowledgePayment(
            int transactionId,
            CancellationToken cancellationToken)
        {
            if (!TryGetBranchId(out var branchId))
                return RedirectToAction("AccessDenied", "Account");

            try
            {
                var success = await _brebService.AcknowledgePaymentAsync(
                    branchId, transactionId, User.Identity?.Name, cancellationToken);

                if (success)
                    TempData["Success"] = ModePrefix + "Acknowledgment completed successfully.";
                else
                    TempData["Error"] = "Acknowledgment failed. Please check the transaction status.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(CollectionList));
        }

        // ---------------------------------------------------------------
        // LISTS
        // ---------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> CollectionList(DateTime? date, CancellationToken cancellationToken)
        {
            if (!TryGetBranchId(out var branchId))
                return RedirectToAction("AccessDenied", "Account");

            var day = date ?? DateTime.Today;
            ViewBag.Date = day.ToString("yyyy-MM-dd");

            return View(await _brebService.GetCollectionListAsync(branchId, day, cancellationToken));
        }

        [HttpGet]
        public async Task<IActionResult> UnsuccessfulList(DateTime? date, CancellationToken cancellationToken)
        {
            if (!TryGetBranchId(out var branchId))
                return RedirectToAction("AccessDenied", "Account");

            ViewBag.Date = date?.ToString("yyyy-MM-dd");

            return View(await _brebService.GetUnsuccessfulListAsync(branchId, date, cancellationToken));
        }

        [HttpGet]
        public async Task<IActionResult> DisputeList(DateTime? date, CancellationToken cancellationToken)
        {
            if (!TryGetBranchId(out var branchId))
                return RedirectToAction("AccessDenied", "Account");

            ViewBag.Date = date?.ToString("yyyy-MM-dd");

            return View(await _brebService.GetDisputeListAsync(branchId, date, cancellationToken));
        }

        // ---------------------------------------------------------------
        // DISPUTE FORM (old project's "Dispute Bill"): Transaction Id + SMS account + type
        // ---------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisputeBill(
            string payerTransactionId,
            string smsAccountNo,
            int disputeType,
            CancellationToken cancellationToken)
        {
            if (!TryGetBranchId(out var branchId))
                return RedirectToAction("AccessDenied", "Account");

            try
            {
                TempData["Success"] = ModePrefix + await _brebService.SubmitDisputeAsync(
                    branchId, payerTransactionId, smsAccountNo, disputeType, User.Identity?.Name, cancellationToken);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(DisputeList));
        }

        // ---------------------------------------------------------------
        // BILL RECEIPT (printable, successful payments only)
        // ---------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> BillReceipt(int id, CancellationToken cancellationToken)
        {
            if (!TryGetBranchId(out var branchId))
                return RedirectToAction("AccessDenied", "Account");

            var receipt = await _brebService.GetReceiptAsync(branchId, id, cancellationToken);
            if (receipt == null)
            {
                TempData["Error"] = "A receipt is available only for successful (acknowledged) payments of this branch.";
                return RedirectToAction(nameof(CollectionList));
            }

            return View(receipt);
        }

        // ---------------------------------------------------------------
        // RECONCILE: local day vs BREB /report/detail
        // ---------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> ReconcileReport(
            DateTime? date,
            CancellationToken cancellationToken)
        {
            if (!TryGetBranchId(out var branchId))
                return RedirectToAction("AccessDenied", "Account");

            var report = await _brebService.GetReconcileReportAsync(
                branchId, date ?? DateTime.Today, cancellationToken);

            return View(report);
        }

        // ---------------------------------------------------------------
        // CHECK & SYNC one payment with BREB (/paymentstatus)
        // ---------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckStatus(
            int transactionId,
            string? returnTo,
            DateTime? date,
            CancellationToken cancellationToken)
        {
            if (!TryGetBranchId(out var branchId))
                return RedirectToAction("AccessDenied", "Account");

            try
            {
                TempData["Success"] = ModePrefix + await _brebService.SyncPaymentStatusAsync(
                    branchId, transactionId, User.Identity?.Name, cancellationToken);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectBack(returnTo, date);
        }

        // ---------------------------------------------------------------
        // DISPUTE (/billinginfo/dispute): 5 = Acknowledge, 10 = Reset
        // ---------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitDispute(
            int transactionId,
            int disputeType,
            string? returnTo,
            DateTime? date,
            CancellationToken cancellationToken)
        {
            if (!TryGetBranchId(out var branchId))
                return RedirectToAction("AccessDenied", "Account");

            try
            {
                TempData["Success"] = ModePrefix + await _brebService.SubmitDisputeAsync(
                    branchId, transactionId, disputeType, User.Identity?.Name, cancellationToken);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectBack(returnTo, date);
        }

        // ---------------------------------------------------------------
        // REPORTS
        // ---------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> SummaryReport(
            DateTime? from,
            DateTime? to,
            CancellationToken cancellationToken)
        {
            if (!TryGetBranchId(out var branchId))
                return RedirectToAction("AccessDenied", "Account");

            var start = from ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var end = to ?? DateTime.Today;

            return View(await _brebService.GetSummaryReportAsync(branchId, start, end, cancellationToken));
        }

        [HttpGet]
        public async Task<IActionResult> CollectionReport(
            DateTime? from,
            DateTime? to,
            CancellationToken cancellationToken)
        {
            if (!TryGetBranchId(out var branchId))
                return RedirectToAction("AccessDenied", "Account");

            var start = from ?? DateTime.Today;
            var end = to ?? DateTime.Today;

            return View(await _brebService.GetCollectionReportAsync(branchId, start, end, cancellationToken));
        }

        // Only these pages may be used as "return to" target (no open redirect).
        private static readonly string[] ReturnTargets =
            { "ReconcileReport", "UnsuccessfulList", "DisputeList", "CollectionList" };

        private IActionResult RedirectBack(string? returnTo, DateTime? date)
        {
            var target = ReturnTargets.Contains(returnTo) ? returnTo! : nameof(CollectionList);

            return target == "ReconcileReport"
                ? RedirectToAction(target, new { date = date?.ToString("yyyy-MM-dd") })
                : RedirectToAction(target);
        }
    }
}
