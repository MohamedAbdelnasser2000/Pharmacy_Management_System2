using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using IMS.Data;
using IMS.Models;
using Microsoft.AspNetCore.Authorization;

namespace IMS.Controllers
{
    [Authorize]
    public class WarehousesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public WarehousesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Warehouses
        public async Task<IActionResult> Index()
        {
            var warehouses = await _context.Warehouses
                .Include(w => w.Branch)
                .Include(w => w.ProductInventories)
                .ToListAsync();
            return View(warehouses);
        }

        // GET: Warehouses/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Load warehouse with branch only
            var warehouse = await _context.Warehouses
                .Include(w => w.Branch)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (warehouse == null)
            {
                return NotFound();
            }

            // Load ProductInventories separately
            var productInventories = await _context.ProductInventories
                .Include(pi => pi.Product)
                .Where(pi => pi.WarehouseId == id)
                .ToListAsync();

            warehouse.ProductInventories = productInventories;

            return View(warehouse);
        }

        // GET: Warehouses/Create
        public IActionResult Create()
        {
            ViewData["BranchId"] = new SelectList(_context.Branches, "Id", "Name");
            return View();
        }

        // POST: Warehouses/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,BranchId,Location")] Warehouse warehouse)
        {
            ModelState.Remove("Branch");
            
            if (ModelState.IsValid)
            {
                _context.Add(warehouse);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم إضافة المستودع بنجاح";
                return RedirectToAction(nameof(Index));
            }
            
            ViewData["BranchId"] = new SelectList(_context.Branches, "Id", "Name", warehouse.BranchId);
            return View(warehouse);
        }

        // GET: Warehouses/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var warehouse = await _context.Warehouses.FindAsync(id);
            if (warehouse == null)
            {
                return NotFound();
            }
            
            ViewData["BranchId"] = new SelectList(_context.Branches, "Id", "Name", warehouse.BranchId);
            return View(warehouse);
        }

        // POST: Warehouses/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,BranchId,Location")] Warehouse warehouse)
        {
            if (id != warehouse.Id)
            {
                return NotFound();
            }

            ModelState.Remove("Branch");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(warehouse);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم تحديث المستودع بنجاح";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!WarehouseExists(warehouse.Id))
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
            
            ViewData["BranchId"] = new SelectList(_context.Branches, "Id", "Name", warehouse.BranchId);
            return View(warehouse);
        }

        // GET: Warehouses/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var warehouse = await _context.Warehouses
                .Include(w => w.Branch)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (warehouse == null)
            {
                return NotFound();
            }

            return View(warehouse);
        }

        // POST: Warehouses/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var warehouse = await _context.Warehouses.FindAsync(id);
            if (warehouse != null)
            {
                _context.Warehouses.Remove(warehouse);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم حذف المستودع بنجاح";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool WarehouseExists(int id)
        {
            return _context.Warehouses.Any(e => e.Id == id);
        }
    }
}
