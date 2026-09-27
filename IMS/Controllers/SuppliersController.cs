using System.Linq;
using System.Threading.Tasks;
using IMS.Data;
using IMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IMS.Controllers
{
    [Authorize(Roles = RoleNamesConstants.Admin + "," + RoleNamesConstants.Accountant)]
    public class SuppliersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SuppliersController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string searchString)
        {
            var suppliers = _context.Suppliers
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                suppliers = suppliers.Where(s => 
                    s.Name.Contains(searchString) || 
                    (s.PhoneNumber != null && s.PhoneNumber.Contains(searchString)) ||
                    (s.Email != null && s.Email.Contains(searchString)));
            }

            ViewData["CurrentFilter"] = searchString;
            return View(await suppliers.OrderByDescending(s => s.Id).ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Load supplier only
            var supplier = await _context.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (supplier == null)
            {
                return NotFound();
            }

            // Load Products separately
            var products = await _context.Products
                .AsNoTracking()
                .Where(p => p.SupplierId == id)
                .ToListAsync();

            // Load PurchaseOrders separately
            var purchaseOrders = await _context.PurchaseOrders
                .AsNoTracking()
                .Where(po => po.SupplierId == id)
                .ToListAsync();

            // Load PurchaseOrderItems for each order
            var orderIds = purchaseOrders.Select(po => po.Id).ToList();
            var purchaseOrderItems = await _context.PurchaseOrderItems
                .AsNoTracking()
                .Where(poi => orderIds.Contains(poi.PurchaseOrderId))
                .ToListAsync();

            // Assign items to their respective orders
            foreach (var order in purchaseOrders)
            {
                order.PurchaseOrderItems = purchaseOrderItems
                    .Where(poi => poi.PurchaseOrderId == order.Id)
                    .ToList();
            }

            supplier.Products = products;
            supplier.PurchaseOrders = purchaseOrders;

            return View(supplier);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Supplier supplier)
        {
            // Remove validation for navigation properties
            ModelState.Remove("Products");
            ModelState.Remove("PurchaseOrders");
            ModelState.Remove("Account");
            ModelState.Remove("LedgerEntries");

            if (ModelState.IsValid)
            {
                _context.Add(supplier);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم إضافة المورد بنجاح";
                return RedirectToAction(nameof(Index));
            }

            return View(supplier);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Supplier supplier)
        {
            if (id != supplier.Id)
            {
                return NotFound();
            }

            // Remove validation for navigation properties
            ModelState.Remove("Products");
            ModelState.Remove("PurchaseOrders");
            ModelState.Remove("Account");
            ModelState.Remove("LedgerEntries");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(supplier);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SupplierExists(supplier.Id))
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

            return View(supplier);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(m => m.Id == id);

            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleNamesConstants.Admin)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier != null)
            {
                _context.Suppliers.Remove(supplier);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool SupplierExists(int id)
        {
            return _context.Suppliers.Any(e => e.Id == id);
        }
    }
}