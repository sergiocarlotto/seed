using System.Security.Claims;
using Seed.Application.Abstractions;

namespace Seed.Api;

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var id = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(id, out var g) ? g : null;
        }
    }
    public bool IsAuthenticated => UserId is not null;
}
