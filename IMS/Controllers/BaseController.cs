using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using IMS.Data;
using IMS.Models;

namespace IMS.Controllers
{
    public class BaseController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BaseController(ApplicationDbContext context)
        {
            _context = context;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // تحميل إعدادات المخزن
            var settings = await _context.StoreSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new StoreSettings { StoreName = "نظام إدارة المخازن" };
            }
            
            ViewBag.StoreName = settings.StoreName;
            ViewBag.StoreSettings = settings;

            await base.OnActionExecutionAsync(context, next);
        }
    }
}
