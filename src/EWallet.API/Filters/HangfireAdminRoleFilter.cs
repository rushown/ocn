using Hangfire.Dashboard;

namespace EWallet.API.Filters;

/// <summary>
/// Restricts the Hangfire dashboard to authenticated users with the "Admin" role.
/// Used in non-Development environments.
/// </summary>
public class HangfireAdminRoleFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        return httpContext.User.Identity?.IsAuthenticated == true
               && httpContext.User.IsInRole("Admin");
    }
}
