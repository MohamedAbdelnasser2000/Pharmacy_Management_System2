using IMS.Data;
using IMS.Models;
using IMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IMS.Controllers
{
    [Authorize(Roles = RoleNamesConstants.Admin + "," + RoleNamesConstants.Pharmacist)]
    public class PrescriptionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IInsuranceService _insuranceService;

        public PrescriptionsController(ApplicationDbContext context, IInsuranceService insuranceService)
        {
            _context = context;
            _insuranceService = insuranceService;
        }

        // GET: Prescriptions
        public async Task<IActionResult> Index(string searchString, bool? isDispensed, DateTime? fromDate, DateTime? toDate)
        {
            var prescriptions = _context.Prescriptions
                .Include(p => p.Doctor)
                .Include(p => p.Customer)
                .Include(p => p.Order)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                prescriptions = prescriptions.Where(p => p.PrescriptionNumber.Contains(searchString) ||
                                                        p.PatientName!.Contains(searchString));
            }

            if (isDispensed.HasValue)
            {
                prescriptions = prescriptions.Where(p => p.IsDispensed == isDispensed.Value);
            }

            if (fromDate.HasValue)
            {
                prescriptions = prescriptions.Where(p => p.PrescriptionDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                prescriptions = prescriptions.Where(p => p.PrescriptionDate <= toDate.Value);
            }

            ViewData["SearchString"] = searchString;
            ViewData["IsDispensed"] = isDispensed;
            ViewData["FromDate"] = fromDate?.ToString("yyyy-MM-dd");
            ViewData["ToDate"] = toDate?.ToString("yyyy-MM-dd");

            return View(await prescriptions.OrderByDescending(p => p.PrescriptionDate).ToListAsync());
        }

        // GET: Prescriptions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var prescription = await _context.Prescriptions
                .Include(p => p.Doctor)
                .Include(p => p.Customer)
                .Include(p => p.Order)
                .Include(p => p.InsurancePolicy)
                    .ThenInclude(ip => ip.Insurance)
                .Include(p => p.PrescriptionItems)
                    .ThenInclude(pi => pi.Product)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (prescription == null)
            {
                return NotFound();
            }

            return View(prescription);
        }

        // GET: Prescriptions/Create
        public IActionResult Create()
        {
            ViewData["DoctorId"] = new SelectList(_context.Doctors.Where(d => d.IsActive), "Id", "Name");
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Name");
            return View();
        }

        // POST: Prescriptions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,PrescriptionNumber,PrescriptionDate,DoctorId,CustomerId,PatientName,PatientAge,Diagnosis,DoctorNotes")] Prescription prescription)
        {
            if (ModelState.IsValid)
            {
                prescription.CreatedDate = AppClock.UtcNow;
                prescription.IsDispensed = false;
                _context.Add(prescription);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إضافة الوصفة الطبية بنجاح";
                return RedirectToAction("AddItems", new { id = prescription.Id });
            }
            ViewData["DoctorId"] = new SelectList(_context.Doctors.Where(d => d.IsActive), "Id", "Name", prescription.DoctorId);
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Name", prescription.CustomerId);
            return View(prescription);
        }

        // GET: Prescriptions/AddItems/5
        public async Task<IActionResult> AddItems(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var prescription = await _context.Prescriptions
                .Include(p => p.Doctor)
                .Include(p => p.Customer)
                .Include(p => p.PrescriptionItems)
                    .ThenInclude(pi => pi.Product)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prescription == null)
            {
                return NotFound();
            }

            ViewData["Products"] = new SelectList(_context.Products.Where(p => p.SalePrice > 0), "Id", "Name");
            return View(prescription);
        }

        // POST: Prescriptions/AddItem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddItem(int prescriptionId, int productId, int quantity, string? dosage, string? frequency, int? duration, string? instructions)
        {
            var prescription = await _context.Prescriptions.FindAsync(prescriptionId);
            var product = await _context.Products.FindAsync(productId);

            if (prescription == null || product == null)
            {
                return NotFound();
            }

            var prescriptionItem = new PrescriptionItem
            {
                PrescriptionId = prescriptionId,
                ProductId = productId,
                Quantity = quantity,
                Dosage = dosage,
                Frequency = frequency,
                Duration = duration,
                Instructions = instructions,
                Price = product.SalePrice,
                Total = (product.SalePrice ?? 0) * quantity
            };

            _context.PrescriptionItems.Add(prescriptionItem);
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم إضافة الدواء للوصفة بنجاح";
            return RedirectToAction("AddItems", new { id = prescriptionId });
        }

        // POST: Prescriptions/RemoveItem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveItem(int itemId, int prescriptionId)
        {
            var item = await _context.PrescriptionItems.FindAsync(itemId);
            if (item != null)
            {
                _context.PrescriptionItems.Remove(item);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف الدواء من الوصفة";
            }

            return RedirectToAction("AddItems", new { id = prescriptionId });
        }

        // POST: Prescriptions/Dispense/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Dispense(int id, int? insurancePolicyId, string? pharmacistNotes)
        {
            var prescription = await _context.Prescriptions
                .Include(p => p.PrescriptionItems)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prescription == null)
            {
                return NotFound();
            }

            prescription.IsDispensed = true;
            prescription.DispensedDate = AppClock.UtcNow;
            prescription.PharmacistNotes = pharmacistNotes;
            prescription.TotalAmount = prescription.PrescriptionItems.Sum(pi => pi.Total ?? 0);

            // Calculate insurance coverage if policy is selected
            if (insurancePolicyId.HasValue && prescription.TotalAmount.HasValue)
            {
                var insuranceResult = await _insuranceService.CalculateInsuranceCoverageAsync(
                    insurancePolicyId, prescription.TotalAmount.Value);

                if (insuranceResult.IsValid)
                {
                    prescription.InsurancePolicyId = insurancePolicyId;
                    prescription.InsuranceCoverageAmount = insuranceResult.InsuranceCoverageAmount;
                    prescription.CustomerPayableAmount = insuranceResult.CustomerPayableAmount;
                    prescription.AppliedCoveragePercentage = insuranceResult.AppliedCoveragePercentage;

                    // Update the policy used amount
                    await _insuranceService.UpdatePolicyUsedAmountAsync(
                        insurancePolicyId.Value,
                        prescription.InsuranceCoverageAmount);
                }
                else
                {
                    prescription.CustomerPayableAmount = prescription.TotalAmount.Value;
                    TempData["Warning"] = insuranceResult.ErrorMessage;
                }
            }
            else
            {
                prescription.CustomerPayableAmount = prescription.TotalAmount ?? 0;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "تم صرف الوصفة بنجاح";

            return RedirectToAction(nameof(Details), new { id = prescription.Id });
        }

        // GET: Prescriptions/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var prescription = await _context.Prescriptions.FindAsync(id);
            if (prescription == null)
            {
                return NotFound();
            }

            if (prescription.IsDispensed)
            {
                TempData["Error"] = "لا يمكن تعديل وصفة تم صرفها";
                return RedirectToAction(nameof(Details), new { id = prescription.Id });
            }

            ViewData["DoctorId"] = new SelectList(_context.Doctors.Where(d => d.IsActive), "Id", "Name", prescription.DoctorId);
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Name", prescription.CustomerId);
            return View(prescription);
        }

        // POST: Prescriptions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,PrescriptionNumber,PrescriptionDate,DoctorId,CustomerId,PatientName,PatientAge,Diagnosis,DoctorNotes,CreatedDate")] Prescription prescription)
        {
            if (id != prescription.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(prescription);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "تم تحديث الوصفة بنجاح";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PrescriptionExists(prescription.Id))
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
            ViewData["DoctorId"] = new SelectList(_context.Doctors.Where(d => d.IsActive), "Id", "Name", prescription.DoctorId);
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Name", prescription.CustomerId);
            return View(prescription);
        }

        // GET: Prescriptions/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var prescription = await _context.Prescriptions
                .Include(p => p.Doctor)
                .Include(p => p.Customer)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (prescription == null)
            {
                return NotFound();
            }

            if (prescription.IsDispensed)
            {
                TempData["Error"] = "لا يمكن حذف وصفة تم صرفها";
                return RedirectToAction(nameof(Details), new { id = prescription.Id });
            }

            return View(prescription);
        }

        // POST: Prescriptions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var prescription = await _context.Prescriptions
                .Include(p => p.PrescriptionItems)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prescription != null)
            {
                if (prescription.IsDispensed)
                {
                    TempData["Error"] = "لا يمكن حذف وصفة تم صرفها";
                    return RedirectToAction(nameof(Index));
                }

                _context.Prescriptions.Remove(prescription);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف الوصفة بنجاح";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool PrescriptionExists(int id)
        {
            return _context.Prescriptions.Any(e => e.Id == id);
        }
    }
}
