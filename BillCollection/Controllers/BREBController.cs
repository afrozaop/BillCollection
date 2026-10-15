using BillCollection.Interfaces;
using BillCollection.Models.BREB;
using BillCollection.ViewModels.BREB;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult BillCollection()
        {
            var model = new BREBBillCollectionViewModel
            {
                BillType = 50,
                PayerTypeId = 2
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BillCollection(
            BREBBillCollectionViewModel model,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var branchIdClaim =
                User.FindFirst("BranchId")?.Value;

            if (!int.TryParse(branchIdClaim, out var branchId))
            {
                return RedirectToAction(
                    "AccessDenied",
                    "Account");
            }

            try
            {
                var request = new BREBBillingInfoRequest
                {
                    SMSAccountNo = model.SMSAccountNo,

                    // Fixed values
                    BillType = 50,
                    PayerTypeId = 2,

                    PayerId = null
                };

                var result =
                    await _brebService.GetBillingInfoAsync(
                        branchId,
                        request,
                        cancellationToken);

                if (result == null)
                {
                    ModelState.AddModelError(
                        "",
                        "No response received from BREB.");

                    return View(model);
                }

                var transactionId =
                    await _brebService.SaveBillingTransactionAsync(
                        branchId,
                        result,
                        User.Identity?.Name,
                        cancellationToken);

                model.TransactionId = transactionId;

                ViewBag.BillResult = result;

                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(
                    "",
                    ex.Message);

                return View(model);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
       
        public async Task<IActionResult> PayBill(
    int transactionId,
    CancellationToken cancellationToken)
        {
            var branchIdClaim =
                User.FindFirst("BranchId")?.Value;

            if (!int.TryParse(
                    branchIdClaim,
                    out var branchId))
            {
                return RedirectToAction(
                    "AccessDenied",
                    "Account");
            }

            if (transactionId <= 0)
            {
                TempData["Error"] =
                    "Invalid payment transaction.";

                return RedirectToAction(
                    nameof(BillCollection));
            }

            try
            {
                var result =
                    await _brebService.SubmitPaymentAsync(
                        branchId,
                        transactionId,
                        cancellationToken);

                if (result == null)
                {
                    TempData["Error"] =
                        "No response received.";

                    return RedirectToAction(
                        nameof(BillCollection));
                }

                if (result.Success)
                {
                    var acknowledged =
                        await _brebService.AcknowledgePaymentAsync(
                            branchId,
                            transactionId,
                            User.Identity?.Name,
                            cancellationToken);

                    if (acknowledged)
                    {
                        TempData["Success"] =
                            "Test payment completed successfully. " +
                            "Acknowledgment completed.";
                    }
                    else
                    {
                        TempData["Error"] =
                            "Payment submitted, but acknowledgment failed.";
                    }
                }
                else
                {
                    TempData["Error"] =
                        result.Message ?? "Payment submission failed.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    ex.Message;
            }

            return RedirectToAction(
                nameof(BillCollection));
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcknowledgePayment(
    int transactionId,
    CancellationToken cancellationToken)
        {
            var branchIdClaim = User.FindFirst("BranchId")?.Value;

            if (!int.TryParse(branchIdClaim, out var branchId))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            try
            {
                var success = await _brebService.AcknowledgePaymentAsync(
                    branchId,
                    transactionId,
                    User.Identity?.Name,
                    cancellationToken);

                if (success)
                {
                    TempData["Success"] =
                        "Test acknowledgment completed successfully.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(CollectionList));
        }
        [HttpGet]
        public async Task<IActionResult> CollectionList(
    CancellationToken cancellationToken)
        {
            var branchIdClaim = User.FindFirst("BranchId")?.Value;

            if (!int.TryParse(branchIdClaim, out var branchId))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var transactions = await _brebService.GetCollectionListAsync(
                branchId,
                cancellationToken);

            return View(transactions);
        }
        [HttpGet]
        public async Task<IActionResult> UnsuccessfulList(
    CancellationToken cancellationToken)
        {
            var branchIdClaim = User.FindFirst("BranchId")?.Value;

            if (!int.TryParse(branchIdClaim, out var branchId))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var transactions =
                await _brebService.GetUnsuccessfulListAsync(
                    branchId,
                    cancellationToken);

            return View(transactions);
        }

        [HttpGet]
        public async Task<IActionResult> DisputeList(
    CancellationToken cancellationToken)
        {
            var branchIdClaim = User.FindFirst("BranchId")?.Value;

            if (!int.TryParse(branchIdClaim, out var branchId))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var transactions = await _brebService.GetDisputeListAsync(
                branchId,
                cancellationToken);

            return View(transactions);
        }

        [HttpGet]
        public async Task<IActionResult> ReconcileReport(
    CancellationToken cancellationToken)
        {
            var branchIdClaim = User.FindFirst("BranchId")?.Value;

            if (!int.TryParse(branchIdClaim, out var branchId))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var report = await _brebService.GetReconcileReportAsync(
                branchId,
                cancellationToken);

            return View(report);
        }


    }
}