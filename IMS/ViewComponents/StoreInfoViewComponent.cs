using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IMS.Data;
using IMS.Models;

namespace IMS.ViewComponents
{
    public class StoreInfoViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public StoreInfoViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var settings = await _context.StoreSettings.FirstOrDefaultAsync();
            
            if (settings == null)
            {
                settings = new StoreSettings
                {
                    StoreName = "نظام إدارة المخازن"
                };
            }

            return View(settings);
        }
    }
}
