using IMS.Data;
using IMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.Controllers
{
    [Authorize(Roles = RoleNamesConstants.Admin + "," + RoleNamesConstants.Pharmacist + "," + RoleNamesConstants.DataEntry)]
    public class InsurancesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InsurancesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Insurances
        public async Task<IActionResult> Index(string searchString, bool? isActive)
        {
            var insurances = _context.Insurances.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                insurances = insurances.Where(i => i.Name.Contains(searchString) ||
                                                   i.Code!.Contains(searchString));
            }

            if (isActive.HasValue)
            {
                insurances = insurances.Where(i => i.IsActive == isActive.Value);
            }

            ViewData["SearchString"] = searchString;
            ViewData["IsActive"] = isActive;

            return View(await insurances.OrderByDescending(i => i.CreatedDate).ToListAsync());
        }

        // GET: Insurances/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var insurance = await _context.Insurances
                .Include(i => i.Policies)
                    .ThenInclude(p => p.Customer)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (insurance == null)
            {
                return NotFound();
            }

            return View(insurance);
        }

        // GET: Insurances/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Insurances/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Code,Phone,Email,Address,DefaultCoveragePercentage,MaxCoverageAmount,IsActive,Notes")] Insurance insurance)
        {
            if (ModelState.IsValid)
            {
                insurance.CreatedDate = AppClock.UtcNow;
                _context.Add(insurance);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إضافة شركة التأمين بنجاح";
                return RedirectToAction(nameof(Index));
            }
            return View(insurance);
        }

        // GET: Insurances/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var insurance = await _context.Insurances.FindAsync(id);
            if (insurance == null)
            {
                return NotFound();
            }
            return View(insurance);
        }

        // POST: Insurances/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Code,Phone,Email,Address,DefaultCoveragePercentage,MaxCoverageAmount,IsActive,CreatedDate,Notes")] Insurance insurance)
        {
            if (id != insurance.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(insurance);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "تم تحديث بيانات شركة التأمين بنجاح";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InsuranceExists(insurance.Id))
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
            return View(insurance);
        }

        // GET: Insurances/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var insurance = await _context.Insurances
                .FirstOrDefaultAsync(m => m.Id == id);
            if (insurance == null)
            {
                return NotFound();
            }

            return View(insurance);
        }

        // POST: Insurances/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var insurance = await _context.Insurances.FindAsync(id);
            if (insurance != null)
            {
                _context.Insurances.Remove(insurance);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف شركة التأمين بنجاح";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool InsuranceExists(int id)
        {
            return _context.Insurances.Any(e => e.Id == id);
        }
    }
}
