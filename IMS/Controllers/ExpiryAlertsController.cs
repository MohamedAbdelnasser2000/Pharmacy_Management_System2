using IMS.Data;
using IMS.Models;
using IMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IMS.Controllers
{
    [Authorize]
    public class ExpiryAlertsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ExpiryAlertService _expiryAlertService;

        public ExpiryAlertsController(ApplicationDbContext context, ExpiryAlertService expiryAlertService)
        {
            _context = context;
            _expiryAlertService = expiryAlertService;
        }

        // GET: ExpiryAlerts
        public async Task<IActionResult> Index(string alertStatus, bool? isHandled, int? warehouseId)
        {
            var alerts = _context.ExpiryAlerts
                .Include(e => e.Product)
                .Include(e => e.Warehouse)
                .AsQueryable();

            if (!string.IsNullOrEmpty(alertStatus))
            {
                alerts = alerts.Where(e => e.AlertStatus == alertStatus);
            }

            if (isHandled.HasValue)
            {
                alerts = alerts.Where(e => e.IsHandled == isHandled.Value);
            }

            if (warehouseId.HasValue)
            {
                alerts = alerts.Where(e => e.WarehouseId == warehouseId.Value);
            }

            ViewData["AlertStatus"] = alertStatus;
            ViewData["IsHandled"] = isHandled;
            ViewData["WarehouseId"] = new SelectList(_context.Warehouses, "Id", "Name", warehouseId);

            return View(await alerts.OrderBy(e => e.ExpiryDate).ToListAsync());
        }

        // GET: ExpiryAlerts/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var alert = await _context.ExpiryAlerts
                .Include(e => e.Product)
                .Include(e => e.Warehouse)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (alert == null)
            {
                return NotFound();
            }

            return View(alert);
        }

        // GET: ExpiryAlerts/Create
        public IActionResult Create()
        {
            ViewData["ProductId"] = new SelectList(_context.Products, "Id", "Name");
            ViewData["WarehouseId"] = new SelectList(_context.Warehouses, "Id", "Name");
            return View();
        }

        // POST: ExpiryAlerts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,ProductId,WarehouseId,ExpiryDate,Quantity,AlertStatus,Notes")] ExpiryAlert alert)
        {
            if (ModelState.IsValid)
            {
                alert.AlertDate = AppClock.UtcNow;
                alert.IsHandled = false;
                _context.Add(alert);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إضافة التنبيه بنجاح";
                return RedirectToAction(nameof(Index));
            }
            ViewData["ProductId"] = new SelectList(_context.Products, "Id", "Name", alert.ProductId);
            ViewData["WarehouseId"] = new SelectList(_context.Warehouses, "Id", "Name", alert.WarehouseId);
            return View(alert);
        }

        // POST: ExpiryAlerts/MarkAsHandled/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsHandled(int id, string actionTaken, string? notes)
        {
            var alert = await _context.ExpiryAlerts.FindAsync(id);
            if (alert == null)
            {
                return NotFound();
            }

            alert.IsHandled = true;
            alert.HandledDate = AppClock.UtcNow;
            alert.ActionTaken = actionTaken;
            if (!string.IsNullOrEmpty(notes))
            {
                alert.Notes = notes;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "تم تحديث حالة التنبيه بنجاح";

            return RedirectToAction(nameof(Index));
        }

        // GET: ExpiryAlerts/GenerateAlerts
        public async Task<IActionResult> GenerateAlerts()
        {
            var addedCount = await _expiryAlertService.GenerateAlertsAsync();

            TempData["Success"] = addedCount > 0
                ? $"تم إنشاء {addedCount} تنبيه جديد"
                : "لا توجد تنبيهات صلاحية جديدة";

            return RedirectToAction(nameof(Index));
        }

        // GET: ExpiryAlerts/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var alert = await _context.ExpiryAlerts
                .Include(e => e.Product)
                .Include(e => e.Warehouse)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (alert == null)
            {
                return NotFound();
            }

            return View(alert);
        }

        // POST: ExpiryAlerts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var alert = await _context.ExpiryAlerts.FindAsync(id);
            if (alert != null)
            {
                _context.ExpiryAlerts.Remove(alert);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف التنبيه بنجاح";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool AlertExists(int id)
        {
            return _context.ExpiryAlerts.Any(e => e.Id == id);
        }
    }
}

