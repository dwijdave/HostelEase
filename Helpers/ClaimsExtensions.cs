using System.Security.Claims;

namespace HostelEase.Helpers;

public static class ClaimsExtensions
{
    public static int UserId(this ClaimsPrincipal user) => int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier));

    public static int StudentId(this ClaimsPrincipal user)
    {
        var v = user.FindFirstValue("StudentId");
        return v == null ? 0 : int.Parse(v);
    }

    public static string FullName(this ClaimsPrincipal user) => user.FindFirstValue("FullName") ?? user.Identity?.Name ?? "";
}
