using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityService.Pages.Redirect;

[AllowAnonymous]
public class IndexModel : PageModel
{
    public string RedirectUri { get; set; }

    public IActionResult OnGet(string redirectUri)
    {
        if (!this.Url.IsLocalUrl(redirectUri))
        {
            return this.RedirectToPage("/Home/Error/Index");
        }

        this.RedirectUri = redirectUri;
        return this.Page();
    }
}
