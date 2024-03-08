using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityService.Pages.Logout;

[SecurityHeaders]
[AllowAnonymous]
public class LoggedOut : PageModel
{
  private readonly IIdentityServerInteractionService _interactionService;

  public LoggedOutViewModel View { get; set; }

  public LoggedOut(IIdentityServerInteractionService interactionService)
  {
        this._interactionService = interactionService;
  }

  public async Task OnGet(string logoutId)
  {
    // get context information (client name, post logout redirect URI and iframe for federated signout)
    var logout = await this._interactionService.GetLogoutContextAsync(logoutId);

        this.View = new LoggedOutViewModel
    {
      AutomaticRedirectAfterSignOut = LogoutOptions.AutomaticRedirectAfterSignOut,
      PostLogoutRedirectUri = logout?.PostLogoutRedirectUri,
      ClientName = string.IsNullOrEmpty(logout?.ClientName) ? logout?.ClientId : logout?.ClientName,
      SignOutIframeUrl = logout?.SignOutIFrameUrl
    };
  }
}
