using IMS.Data;
using IMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace IMS.Controllers
{
    [Authorize]
    public class MedicationRequestsController : BaseController
    {
        private readonly ApplicationDbContext _context;

        public MedicationRequestsController(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        // GET: MedicationRequests
        public async Task<IActionResult> Index(string searchString, RequestStatus? status, Priority? priority)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["StatusFilter"] = status;
            ViewData["PriorityFilter"] = priority;

            var requests = _context.MedicationRequests
                .Include(m => m.Branch)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                requests = requests.Where(r => 
                    r.MedicationName.Contains(searchString) ||
                    r.ScientificName.Contains(searchString) ||
                    r.PatientName.Contains(searchString) ||
                    r.PatientPhone.Contains(searchString));
            }

            if (status.HasValue)
            {
                requests = requests.Where(r => r.Status == status.Value);
            }

            if (priority.HasValue)
            {
                requests = requests.Where(r => r.Priority == priority.Value);
            }

            // Order by priority (Emergency first) and then by date
            requests = requests
                .OrderByDescending(r => r.Priority)
                .ThenByDescending(r => r.RequestDate);

            return View(await requests.ToListAsync());
        }

        // GET: MedicationRequests/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var medicationRequest = await _context.MedicationRequests
                .Include(m => m.Branch)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (medicationRequest == null)
            {
                return NotFound();
            }

            return View(medicationRequest);
        }

        // GET: MedicationRequests/Create
        public IActionResult Create()
        {
            ViewData["BranchId"] = new SelectList(_context.Branches, "Id", "Name");
            return View();
        }

        // POST: MedicationRequests/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,MedicationName,ScientificName,Strength,DosageForm,PatientName,PatientPhone,Notes,RequestedQuantity,Priority,BranchId")] MedicationRequest medicationRequest)
        {
            if (ModelState.IsValid)
            {
                medicationRequest.RequestDate = AppClock.UtcNow;
                medicationRequest.Status = RequestStatus.Pending;
                medicationRequest.CreatedBy = User.Identity.Name;

                _context.Add(medicationRequest);
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "تم إضافة طلب الدواء بنجاح";
                return RedirectToAction(nameof(Index));
            }

            ViewData["BranchId"] = new SelectList(_context.Branches, "Id", "Name", medicationRequest.BranchId);
            return View(medicationRequest);
        }

        // GET: MedicationRequests/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var medicationRequest = await _context.MedicationRequests.FindAsync(id);
            if (medicationRequest == null)
            {
                return NotFound();
            }

            ViewData["BranchId"] = new SelectList(_context.Branches, "Id", "Name", medicationRequest.BranchId);
            return View(medicationRequest);
        }

        // POST: MedicationRequests/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,MedicationName,ScientificName,Strength,DosageForm,PatientName,PatientPhone,Notes,RequestedQuantity,Status,RequestDate,OrderedDate,ReceivedDate,PatientNotified,Priority,CreatedBy,BranchId")] MedicationRequest medicationRequest)
        {
            if (id != medicationRequest.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Auto-set dates based on status changes
                    if (medicationRequest.Status == RequestStatus.Ordered && !medicationRequest.OrderedDate.HasValue)
                    {
                        medicationRequest.OrderedDate = AppClock.UtcNow;
                    }
                    
                    if (medicationRequest.Status == RequestStatus.Received && !medicationRequest.ReceivedDate.HasValue)
                    {
                        medicationRequest.ReceivedDate = AppClock.UtcNow;
                    }

                    _context.Update(medicationRequest);
                    await _context.SaveChangesAsync();
                    
                    TempData["SuccessMessage"] = "تم تحديث طلب الدواء بنجاح";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MedicationRequestExists(medicationRequest.Id))
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

            ViewData["BranchId"] = new SelectList(_context.Branches, "Id", "Name", medicationRequest.BranchId);
            return View(medicationRequest);
        }

        // GET: MedicationRequests/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var medicationRequest = await _context.MedicationRequests
                .Include(m => m.Branch)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (medicationRequest == null)
            {
                return NotFound();
            }

            return View(medicationRequest);
        }

        // POST: MedicationRequests/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var medicationRequest = await _context.MedicationRequests.FindAsync(id);
            if (medicationRequest != null)
            {
                _context.MedicationRequests.Remove(medicationRequest);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم حذف طلب الدواء بنجاح";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: MedicationRequests/UpdateStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, RequestStatus status)
        {
            var medicationRequest = await _context.MedicationRequests.FindAsync(id);
            if (medicationRequest == null)
            {
                return NotFound();
            }

            medicationRequest.Status = status;

            if (status == RequestStatus.Ordered && !medicationRequest.OrderedDate.HasValue)
            {
                medicationRequest.OrderedDate = AppClock.UtcNow;
            }
            else if (status == RequestStatus.Received && !medicationRequest.ReceivedDate.HasValue)
            {
                medicationRequest.ReceivedDate = AppClock.UtcNow;
            }

            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "تم تحديث حالة الطلب بنجاح";
            return RedirectToAction(nameof(Index));
        }

        // POST: MedicationRequests/ToggleNotification/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleNotification(int id)
        {
            var medicationRequest = await _context.MedicationRequests.FindAsync(id);
            if (medicationRequest == null)
            {
                return NotFound();
            }

            medicationRequest.PatientNotified = !medicationRequest.PatientNotified;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool MedicationRequestExists(int id)
        {
            return _context.MedicationRequests.Any(e => e.Id == id);
        }
    }
}
