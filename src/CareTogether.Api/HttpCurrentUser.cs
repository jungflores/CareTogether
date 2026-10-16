using System.Security.Claims;
using CareTogether.Application;
namespace CareTogether.Api;
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser { public Guid UserId => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : throw new UnauthorizedAccessException(); }
