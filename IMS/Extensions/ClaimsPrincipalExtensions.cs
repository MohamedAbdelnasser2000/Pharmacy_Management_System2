using System.Security.Claims;

namespace IMS
{
    public static class ClaimsPrincipalExtensions
    {
        public static string GetFullName(this ClaimsPrincipal user)
        {
            if (user == null) return string.Empty;
            return user.FindFirst("FullName")?.Value ?? user.Identity?.Name ?? string.Empty;
        }
    }
}
