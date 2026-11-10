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

                //var transactionId =
                //    await _brebService.SaveBillingTransactionAsync(
                //        branchId,
                //        result,
                //        User.Identity?.Name,
                //        cancellationToken);

                //model.TransactionId = transactionId;

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
            string smsAccountNo,
            CancellationToken cancellationToken)
        {
            var branchIdClaim = User.FindFirst("BranchId")?.Value;

            if (!int.TryParse(branchIdClaim, out var branchId))
                return RedirectToAction("AccessDenied", "Account");

            if (string.IsNullOrWhiteSpace(smsAccountNo))
            {
                TempData["Error"] = "SMS Account Number is required.";
                return RedirectToAction(nameof(BillCollection));
            }

            try
            {
                // Step 1: Re-query bill information from server
                var request = new BREBBillingInfoRequest
                {
                    SMSAccountNo = smsAccountNo,
                    BillType = 50,
                    PayerTypeId = 2,
                    PayerId = null
                };

                var billingInfo = await _brebService.GetBillingInfoAsync(
                    branchId,
                    request,
                    cancellationToken);

                if (billingInfo == null)
                {
                    TempData["Error"] =
                        "Bill information could not be retrieved.";

                    return RedirectToAction(nameof(BillCollection));
                }

                // Step 2: Create transaction in database
                var transactionId =
                    await _brebService.SaveBillingTransactionAsync(
                        branchId,
                        billingInfo,
                        User.Identity?.Name,
                        cancellationToken);

                if (transactionId <= 0)
                {
                    TempData["Error"] =
                        "Payment transaction could not be created.";

                    return RedirectToAction(nameof(BillCollection));
                }

                // Step 3: Submit payment using newly created transaction ID
                var result = await _brebService.SubmitPaymentAsync(
                    branchId,
                    transactionId,
                    cancellationToken);

                if (result == null || !result.Success)
                {
                    TempData["Error"] =
                        result?.Message ?? "Test payment submission failed.";
                }
                else
                {
                    TempData["Success"] =
                        "TEST MODE: Payment submitted successfully. " +
                        "Waiting for test acknowledgment.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(CollectionList));
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcknowledgePayment(
     int transactionId,
     CancellationToken cancellationToken)
        {
            var branchIdClaim = User.FindFirst("BranchId")?.Value;

            if (!int.TryParse(branchIdClaim, out var branchId))
                return RedirectToAction("AccessDenied", "Account");

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
                        "TEST MODE: Test acknowledgment completed successfully.";
                }
                else
                {
                    TempData["Error"] =
                        "Test acknowledgment failed. Please check transaction status.";
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

    //    [HttpGet]
    //    public async Task<IActionResult> DisputeList(
    //CancellationToken cancellationToken)
    //    {
    //        var branchIdClaim = User.FindFirst("BranchId")?.Value;

    //        if (!int.TryParse(branchIdClaim, out var branchId))
    //        {
    //            return RedirectToAction("AccessDenied", "Account");
    //        }

    //        var transactions = await _brebService.GetDisputeListAsync(
    //            branchId,
    //            cancellationToken);

    //        return View(transactions);
    //    }

    //    [HttpGet]
    //    public async Task<IActionResult> ReconcileReport(
    //CancellationToken cancellationToken)
    //    {
    //        var branchIdClaim = User.FindFirst("BranchId")?.Value;

    //        if (!int.TryParse(branchIdClaim, out var branchId))
    //        {
    //            return RedirectToAction("AccessDenied", "Account");
    //        }

    //        var report = await _brebService.GetReconcileReportAsync(
    //            branchId,
    //            cancellationToken);

    //        return View(report);
    //    }


    }
}