using IMS.Data;
using IMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IMS.Controllers
{
    [Authorize]
    public class InsurancePoliciesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InsurancePoliciesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: InsurancePolicies
        public async Task<IActionResult> Index(string searchString, int? insuranceId, bool? isActive, bool? isValid)
        {
            var policies = _context.InsurancePolicies
                .Include(p => p.Insurance)
                .Include(p => p.Customer)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                policies = policies.Where(p => p.PolicyNumber.Contains(searchString) ||
                                              p.PolicyHolderName!.Contains(searchString));
            }

            if (insuranceId.HasValue)
            {
                policies = policies.Where(p => p.InsuranceId == insuranceId.Value);
            }

            if (isActive.HasValue)
            {
                policies = policies.Where(p => p.IsActive == isActive.Value);
            }

            if (isValid.HasValue && isValid.Value)
            {
                var now = AppClock.UtcNow;
                policies = policies.Where(p => p.IsActive && p.StartDate <= now && p.EndDate >= now);
            }

            ViewData["SearchString"] = searchString;
            ViewData["InsuranceId"] = new SelectList(_context.Insurances, "Id", "Name", insuranceId);
            ViewData["IsActive"] = isActive;
            ViewData["IsValid"] = isValid;

            return View(await policies.OrderByDescending(p => p.CreatedDate).ToListAsync());
        }

        // GET: InsurancePolicies/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var policy = await _context.InsurancePolicies
                .Include(p => p.Insurance)
                .Include(p => p.Customer)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (policy == null)
            {
                return NotFound();
            }

            return View(policy);
        }

        // GET: InsurancePolicies/Create
        public IActionResult Create()
        {
            ViewData["InsuranceId"] = new SelectList(_context.Insurances.Where(i => i.IsActive), "Id", "Name");
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Name");
            return View();
        }

        // POST: InsurancePolicies/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,PolicyNumber,InsuranceId,CustomerId,PolicyHolderName,StartDate,EndDate,CoveragePercentage,MaxCoverageAmount,IsActive,Notes")] InsurancePolicy policy)
        {
            if (ModelState.IsValid)
            {
                policy.CreatedDate = AppClock.UtcNow;
                policy.UsedAmount = 0;
                _context.Add(policy);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إضافة البوليصة بنجاح";
                return RedirectToAction(nameof(Index));
            }
            ViewData["InsuranceId"] = new SelectList(_context.Insurances.Where(i => i.IsActive), "Id", "Name", policy.InsuranceId);
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Name", policy.CustomerId);
            return View(policy);
        }

        // GET: InsurancePolicies/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var policy = await _context.InsurancePolicies.FindAsync(id);
            if (policy == null)
            {
                return NotFound();
            }
            ViewData["InsuranceId"] = new SelectList(_context.Insurances.Where(i => i.IsActive), "Id", "Name", policy.InsuranceId);
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Name", policy.CustomerId);
            return View(policy);
        }

        // POST: InsurancePolicies/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,PolicyNumber,InsuranceId,CustomerId,PolicyHolderName,StartDate,EndDate,CoveragePercentage,MaxCoverageAmount,UsedAmount,IsActive,Notes,CreatedDate")] InsurancePolicy policy)
        {
            if (id != policy.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(policy);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "تم تحديث البوليصة بنجاح";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PolicyExists(policy.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["InsuranceId"] = new SelectList(_context.Insurances.Where(i => i.IsActive), "Id", "Name", policy.InsuranceId);
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Name", policy.CustomerId);
            return View(policy);
        }

        // GET: InsurancePolicies/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var policy = await _context.InsurancePolicies
                .Include(p => p.Insurance)
                .Include(p => p.Customer)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (policy == null)
            {
                return NotFound();
            }

            return View(policy);
        }

        // POST: InsurancePolicies/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var policy = await _context.InsurancePolicies.FindAsync(id);
            if (policy != null)
            {
                _context.InsurancePolicies.Remove(policy);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف البوليصة بنجاح";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool PolicyExists(int id)
        {
            return _context.InsurancePolicies.Any(e => e.Id == id);
        }
    }
}
