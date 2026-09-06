using System.Security.Claims;

namespace CodeMaster.Web.Middleware;

public class ForwardAuthMiddleware
{
    private readonly RequestDelegate _next;

    public ForwardAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("Remote-User", out var remoteUser) && !string.IsNullOrWhiteSpace(remoteUser))
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, remoteUser.ToString()),
                new("preferred_username", remoteUser.ToString())
            };

            if (context.Request.Headers.TryGetValue("Remote-Email", out var remoteEmail) && !string.IsNullOrWhiteSpace(remoteEmail))
            {
                claims.Add(new(ClaimTypes.Email, remoteEmail.ToString()));
            }

            if (context.Request.Headers.TryGetValue("Remote-Groups", out var remoteGroups) && !string.IsNullOrWhiteSpace(remoteGroups))
            {
                var groups = remoteGroups.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var group in groups)
                {
                    claims.Add(new(ClaimTypes.Role, group));
                }
            }

            var identity = new ClaimsIdentity(claims, "ForwardAuth");
            context.User = new ClaimsPrincipal(identity);
        }

        await _next(context);
    }
}
