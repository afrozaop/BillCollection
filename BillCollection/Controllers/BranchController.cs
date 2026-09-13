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

            var branch =
                await _branchService
                    .GetBranchAsync(branchId);

            if (branch == null)
            {
                return NotFound(
                    "Branch not found.");
            }

            var providers =
                await _branchService
                    .GetAssignedProvidersAsync(
                        branchId);

            ViewBag.Branch = branch;

            return View(providers);
        }
    }
}