using IMS.Data;
using IMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IMS.Controllers
{
    [Authorize]
    public class DrugInteractionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DrugInteractionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: DrugInteractions
        public async Task<IActionResult> Index(string searchString, string severityLevel, bool? isActive)
        {
            var interactions = _context.DrugInteractions
                .Include(d => d.Product1)
                .Include(d => d.Product2)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                interactions = interactions.Where(d => d.Product1.Name.Contains(searchString) ||
                                                      d.Product2.Name.Contains(searchString) ||
                                                      d.InteractionDescription.Contains(searchString));
            }

            if (!string.IsNullOrEmpty(severityLevel))
            {
                interactions = interactions.Where(d => d.SeverityLevel == severityLevel);
            }

            if (isActive.HasValue)
            {
                interactions = interactions.Where(d => d.IsActive == isActive.Value);
            }

            ViewData["SearchString"] = searchString;
            ViewData["SeverityLevel"] = severityLevel;
            ViewData["IsActive"] = isActive;

            return View(await interactions.OrderByDescending(d => d.CreatedDate).ToListAsync());
        }

        // GET: DrugInteractions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var interaction = await _context.DrugInteractions
                .Include(d => d.Product1)
                .Include(d => d.Product2)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (interaction == null)
            {
                return NotFound();
            }

            return View(interaction);
        }

        // GET: DrugInteractions/Create
        public IActionResult Create()
        {
            ViewData["Product1Id"] = new SelectList(_context.Products, "Id", "Name");
            ViewData["Product2Id"] = new SelectList(_context.Products, "Id", "Name");
            return View();
        }

        // POST: DrugInteractions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Product1Id,Product2Id,SeverityLevel,InteractionDescription,Recommendations,IsActive")] DrugInteraction interaction)
        {
            if (interaction.Product1Id == interaction.Product2Id)
            {
                ModelState.AddModelError("", "لا يمكن إضافة تفاعل للدواء مع نفسه");
            }

            // Check if interaction already exists
            var exists = await _context.DrugInteractions
                .AnyAsync(d => (d.Product1Id == interaction.Product1Id && d.Product2Id == interaction.Product2Id) ||
                              (d.Product1Id == interaction.Product2Id && d.Product2Id == interaction.Product1Id));

            if (exists)
            {
                ModelState.AddModelError("", "هذا التفاعل موجود بالفعل");
            }

            if (ModelState.IsValid)
            {
                interaction.CreatedDate = AppClock.UtcNow;
                _context.Add(interaction);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إضافة التفاعل الدوائي بنجاح";
                return RedirectToAction(nameof(Index));
            }
            ViewData["Product1Id"] = new SelectList(_context.Products, "Id", "Name", interaction.Product1Id);
            ViewData["Product2Id"] = new SelectList(_context.Products, "Id", "Name", interaction.Product2Id);
            return View(interaction);
        }

        // GET: DrugInteractions/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var interaction = await _context.DrugInteractions.FindAsync(id);
            if (interaction == null)
            {
                return NotFound();
            }
            ViewData["Product1Id"] = new SelectList(_context.Products, "Id", "Name", interaction.Product1Id);
            ViewData["Product2Id"] = new SelectList(_context.Products, "Id", "Name", interaction.Product2Id);
            return View(interaction);
        }

        // POST: DrugInteractions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Product1Id,Product2Id,SeverityLevel,InteractionDescription,Recommendations,IsActive,CreatedDate")] DrugInteraction interaction)
        {
            if (id != interaction.Id)
            {
                return NotFound();
            }

            if (interaction.Product1Id == interaction.Product2Id)
            {
                ModelState.AddModelError("", "لا يمكن إضافة تفاعل للدواء مع نفسه");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(interaction);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "تم تحديث التفاعل الدوائي بنجاح";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InteractionExists(interaction.Id))
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
            ViewData["Product1Id"] = new SelectList(_context.Products, "Id", "Name", interaction.Product1Id);
            ViewData["Product2Id"] = new SelectList(_context.Products, "Id", "Name", interaction.Product2Id);
            return View(interaction);
        }

        // GET: DrugInteractions/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var interaction = await _context.DrugInteractions
                .Include(d => d.Product1)
                .Include(d => d.Product2)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (interaction == null)
            {
                return NotFound();
            }

            return View(interaction);
        }

        // POST: DrugInteractions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var interaction = await _context.DrugInteractions.FindAsync(id);
            if (interaction != null)
            {
                _context.DrugInteractions.Remove(interaction);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف التفاعل الدوائي بنجاح";
            }

            return RedirectToAction(nameof(Index));
        }

        // API: Check interactions for a list of products
        [HttpPost]
        public async Task<IActionResult> CheckInteractions([FromBody] List<int> productIds)
        {
            var interactions = new List<DrugInteraction>();

            for (int i = 0; i < productIds.Count; i++)
            {
                for (int j = i + 1; j < productIds.Count; j++)
                {
                    var interaction = await _context.DrugInteractions
                        .Include(d => d.Product1)
                        .Include(d => d.Product2)
                        .FirstOrDefaultAsync(d => d.IsActive &&
                            ((d.Product1Id == productIds[i] && d.Product2Id == productIds[j]) ||
                             (d.Product1Id == productIds[j] && d.Product2Id == productIds[i])));

                    if (interaction != null)
                    {
                        interactions.Add(interaction);
                    }
                }
            }

            return Json(interactions);
        }

        private bool InteractionExists(int id)
        {
            return _context.DrugInteractions.Any(e => e.Id == id);
        }
    }
}
