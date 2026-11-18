using BillCollection.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BillCollection.Controllers
{
    [Authorize(Roles = "Branch")]
    public class BranchController : Controller
    {
        private readonly IBranchService _branchService;

        public BranchController(
            IBranchService branchService)
        {
            _branchService = branchService;
        }

        // =========================================================
        // BRANCH DASHBOARD
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var branchIdValue =
                User.FindFirst("BranchId")?.Value;

            if (!int.TryParse(
                    branchIdValue,
                    out int branchId))
            {
                return Forbid();
            }

            var model =
                await _branchService
                    .GetDashboardAsync(branchId);

            if (model == null)
            {
                return NotFound(
                    "Branch not found.");
            }

            return View(model);
        }
    }
}