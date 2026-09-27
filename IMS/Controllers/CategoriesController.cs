using System.Linq;
using System.Threading.Tasks;
using IMS.Data;
using IMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.Controllers
{
    [Authorize(Roles = RoleNamesConstants.Admin + "," + RoleNamesConstants.WarehouseStaff)]
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Categories
        public async Task<IActionResult> Index(string searchString)
        {
            var categories = _context.Categories.Include(c => c.Products).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                categories = categories.Where(c => 
                    c.Name.Contains(searchString) || 
                    (c.Description != null && c.Description.Contains(searchString)));
            }

            ViewData["CurrentFilter"] = searchString;
            return View(await categories.OrderBy(c => c.Name).ToListAsync());
        }

        // GET: Categories/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(m => m.Id == id);
            
            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // GET: Categories/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Categories/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description,IsActive")] Category category)
        {
            if (ModelState.IsValid)
            {
                category.CreatedDate = AppClock.UtcNow;
                _context.Add(category);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم إنشاء الفئة بنجاح";
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        // GET: Categories/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return NotFound();
            }
            return View(category);
        }

        // POST: Categories/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,CreatedDate,IsActive")] Category category)
        {
            if (id != category.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(category);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم تحديث الفئة بنجاح";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CategoryExists(category.Id))
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
            return View(category);
        }

        // GET: Categories/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(m => m.Id == id);
            
            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // POST: Categories/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Id == id);
            
            if (category != null)
            {
                if (category.Products.Any())
                {
                    TempData["ErrorMessage"] = "لا يمكن حذف الفئة لأنها مرتبطة بمنتجات. يرجى نقل المنتجات لفئة أخرى أولاً.";
                    return RedirectToAction(nameof(Delete), new { id });
                }

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم حذف الفئة بنجاح";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Categories/CreateQuick (AJAX)
        [HttpPost]
        // [ValidateAntiForgeryToken] // Temporarily disabled for debugging
        public async Task<IActionResult> CreateQuick([Bind("Name,Description,IsActive")] Category category)
        {
            try
            {
                // Debug logging
                System.Diagnostics.Debug.WriteLine($"CreateQuick called with Name: {category.Name}, Description: {category.Description}, IsActive: {category.IsActive}");
                
                // Log ModelState
                foreach (var modelState in ModelState)
                {
                    System.Diagnostics.Debug.WriteLine($"ModelState Key: {modelState.Key}, Errors: {string.Join(", ", modelState.Value.Errors.Select(e => e.ErrorMessage))}");
                }
                
                if (ModelState.IsValid)
                {
                    category.CreatedDate = AppClock.UtcNow;
                    _context.Add(category);
                    await _context.SaveChangesAsync();
                    
                    System.Diagnostics.Debug.WriteLine($"Category saved successfully with ID: {category.Id}");
                    
                    return Json(new { 
                        success = true, 
                        category = new { 
                            id = category.Id, 
                            name = category.Name 
                        } 
                    });
                }

                // Return validation errors
                var errors = ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );

                System.Diagnostics.Debug.WriteLine($"ModelState invalid. Errors: {string.Join(", ", errors.SelectMany(e => e.Value))}");
                return Json(new { success = false, errors = errors });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception in CreateQuick: {ex.Message}");
                return Json(new { 
                    success = false, 
                    message = "حدث خطأ أثناء حفظ الفئة: " + ex.Message 
                });
            }
        }

        // GET: Categories/TestQuickAdd
        public IActionResult TestQuickAdd()
        {
            return View();
        }

        private bool CategoryExists(int id)
        {
            return _context.Categories.Any(e => e.Id == id);
        }
    }
}
