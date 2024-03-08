using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;

namespace IdentityService.Pages.Diagnostics;

[SecurityHeaders]
[Authorize]
public class Index : PageModel
{
    public ViewModel View { get; set; }

    public async Task<IActionResult> OnGet()
    {
        var localAddresses = new string[] { "::ffff:192.168.65.1", "127.0.0.1", "::1", this.HttpContext.Connection.LocalIpAddress.ToString() };
        if (!localAddresses.Contains(this.HttpContext.Connection.RemoteIpAddress.ToString()))
        {
            return this.NotFound();
        }

        this.View = new ViewModel(await this.HttpContext.AuthenticateAsync());

        return this.Page();
    }
}
