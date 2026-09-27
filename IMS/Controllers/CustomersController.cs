using System.Linq;
using System.Threading.Tasks;
using IMS.Data;
using IMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.Controllers
{
    [Authorize(Roles = RoleNamesConstants.Admin + "," + RoleNamesConstants.Pharmacist + "," + RoleNamesConstants.Cashier + "," + RoleNamesConstants.DataEntry)]
    public class CustomersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CustomersController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string searchString)
        {
            var customers = _context.Customers
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                customers = customers.Where(c => 
                    c.Name.Contains(searchString) || 
                    (c.NationalId != null && c.NationalId.Contains(searchString)) ||
                    (c.PhoneNumber != null && c.PhoneNumber.Contains(searchString)) ||
                    (c.Email != null && c.Email.Contains(searchString)));
            }

            ViewData["CurrentFilter"] = searchString;
            return View(await customers.OrderByDescending(c => c.Id).ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Load customer only
            var customer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (customer == null)
            {
                return NotFound();
            }

            // Load Orders separately
            var orders = await _context.Orders
                .AsNoTracking()
                .Where(o => o.CustomerId == id)
                .ToListAsync();

            // Load OrderItems for all orders
            var orderIds = orders.Select(o => o.Id).ToList();
            var orderItems = await _context.OrderItems
                .AsNoTracking()
                .Include(oi => oi.Product)
                .Where(oi => orderIds.Contains(oi.OrderId))
                .ToListAsync();

            // Assign items to their respective orders
            foreach (var order in orders)
            {
                order.OrderItems = orderItems
                    .Where(oi => oi.OrderId == order.Id)
                    .ToList();
            }

            customer.Orders = orders;

            // Load Prescriptions
            var prescriptions = await _context.Prescriptions
                .AsNoTracking()
                .Where(p => p.CustomerId == id || p.PatientName == customer.Name)
                .OrderByDescending(p => p.PrescriptionDate)
                .ToListAsync();
            
            ViewBag.Prescriptions = prescriptions;

            return View(customer);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            // Remove validation for navigation properties
            ModelState.Remove("Orders");

            if (ModelState.IsValid)
            {
                _context.Add(customer);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(customer);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Customer customer)
        {
            if (id != customer.Id)
            {
                return NotFound();
            }

            // Remove validation for navigation properties
            ModelState.Remove("Orders");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(customer);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CustomerExists(customer.Id))
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
            return View(customer);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(m => m.Id == id);
            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleNamesConstants.Admin)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer != null)
            {
                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool CustomerExists(int id)
        {
            return _context.Customers.Any(e => e.Id == id);
        }
    }
}
