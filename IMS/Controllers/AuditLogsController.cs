using System.Linq;
using System.Threading.Tasks;
using IMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IMS.Data;

namespace IMS.Controllers
{
    [Authorize(Roles = RoleNamesConstants.Admin)]
    public class AuditLogsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuditLogsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? searchTerm, string? actionFilter, DateTime? fromDate, DateTime? toDate)
        {
            var logs = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                logs = logs.Where(l => l.UserName.Contains(searchTerm) || l.EntityName.Contains(searchTerm) || l.EntityId.Contains(searchTerm));
            }

            if (!string.IsNullOrEmpty(actionFilter))
            {
                logs = logs.Where(l => l.Action == actionFilter);
            }

            if (fromDate.HasValue)
            {
                logs = logs.Where(l => l.DateTime >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                logs = logs.Where(l => l.DateTime <= toDate.Value);
            }

            var auditLogs = await logs.OrderByDescending(l => l.DateTime).Take(500).ToListAsync();
            
            ViewBag.Actions = await _context.AuditLogs.Select(l => l.Action).Distinct().ToListAsync();
            
            return View(auditLogs);
        }

        public async Task<IActionResult> Details(int id)
        {
            var log = await _context.AuditLogs.FindAsync(id);
            if (log == null) return NotFound();
            return View(log);
        }
    }
}
