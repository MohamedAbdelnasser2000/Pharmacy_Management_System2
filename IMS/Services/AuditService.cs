using System.Text.Json;
using IMS.Data;
using IMS.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace IMS.Services
{
    public interface IAuditService
    {
        Task LogAsync(string action, string entityName, string entityId, object? oldValues = null, object? newValues = null);
    }

    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserManager<ApplicationUser> _userManager;

        public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _userManager = userManager;
        }

        public async Task LogAsync(string action, string entityName, string entityId, object? oldValues = null, object? newValues = null)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var userId = "System";
            var userName = "System";

            if (httpContext?.User?.Identity?.IsAuthenticated == true)
            {
                userId = _userManager.GetUserId(httpContext.User) ?? "Unknown";
                userName = httpContext.User.Identity.Name ?? "Unknown";
            }

            var auditLog = new AuditLog
            {
                UserId = userId,
                UserName = userName,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
                NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
                DateTime = AppClock.UtcNow,
                IpAddress = httpContext?.Connection?.RemoteIpAddress?.ToString()
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }
    }
}
