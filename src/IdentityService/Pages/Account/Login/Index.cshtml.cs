using Duende.IdentityServer.Events;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using IdentityService.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityService.Pages.Login;

[SecurityHeaders]
[AllowAnonymous]
public class Index : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IEventService _events;
    private readonly IAuthenticationSchemeProvider _schemeProvider;
    private readonly IIdentityProviderStore _identityProviderStore;

    public ViewModel View { get; set; }

    [BindProperty]
    public InputModel Input { get; set; }

    public Index(
        IIdentityServerInteractionService interaction,
        IAuthenticationSchemeProvider schemeProvider,
        IIdentityProviderStore identityProviderStore,
        IEventService events,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        this._userManager = userManager;
        this._signInManager = signInManager;
        this._interaction = interaction;
        this._schemeProvider = schemeProvider;
        this._identityProviderStore = identityProviderStore;
        this._events = events;
    }

    public async Task<IActionResult> OnGet(string returnUrl)
    {
        await this.BuildModelAsync(returnUrl);

        if (this.View.IsExternalLoginOnly)
        {
            // we only have one option for logging in and it's an external provider
            return this.RedirectToPage("/ExternalLogin/Challenge", new { scheme = this.View.ExternalLoginScheme, returnUrl });
        }

        return this.Page();
    }

    public async Task<IActionResult> OnPost()
    {
        // check if we are in the context of an authorization request
        var context = await this._interaction.GetAuthorizationContextAsync(this.Input.ReturnUrl);

        // the user clicked the "cancel" button
        if (this.Input.Button != "login")
        {
            if (context != null)
            {
                // if the user cancels, send a result back into IdentityServer as if they
                // denied the consent (even if this client does not require consent).
                // this will send back an access denied OIDC error response to the client.
                await this._interaction.DenyAuthorizationAsync(context, AuthorizationError.AccessDenied);

                // we can trust model.ReturnUrl since GetAuthorizationContextAsync returned non-null
                if (context.IsNativeClient())
                {
                    // The client is native, so this change in how to
                    // return the response is for better UX for the end user.
                    return this.LoadingPage(this.Input.ReturnUrl);
                }

                return this.Redirect(this.Input.ReturnUrl);
            }
            else
            {
                // since we don't have a valid context, then we just go back to the home page
                return this.Redirect("~/");
            }
        }

        if (this.ModelState.IsValid)
        {
            var result = await this._signInManager.PasswordSignInAsync(this.Input.Username, this.Input.Password, this.Input.RememberLogin, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                var user = await this._userManager.FindByNameAsync(this.Input.Username);
                await this._events.RaiseAsync(new UserLoginSuccessEvent(user.UserName, user.Id, user.UserName, clientId: context?.Client.ClientId));

                if (context != null)
                {
                    if (context.IsNativeClient())
                    {
                        // The client is native, so this change in how to
                        // return the response is for better UX for the end user.
                        return this.LoadingPage(this.Input.ReturnUrl);
                    }

                    // we can trust model.ReturnUrl since GetAuthorizationContextAsync returned non-null
                    return this.Redirect(this.Input.ReturnUrl);
                }

                // request for a local page
                if (this.Url.IsLocalUrl(this.Input.ReturnUrl))
                {
                    return this.Redirect(this.Input.ReturnUrl);
                }
                else if (string.IsNullOrEmpty(this.Input.ReturnUrl))
                {
                    return this.Redirect("~/");
                }
                else
                {
                    // user might have clicked on a malicious link - should be logged
                    throw new Exception("invalid return URL");
                }
            }

            await this._events.RaiseAsync(new UserLoginFailureEvent(this.Input.Username, "invalid credentials", clientId: context?.Client.ClientId));
            this.ModelState.AddModelError(string.Empty, LoginOptions.InvalidCredentialsErrorMessage);
        }

        // something went wrong, show form with error
        await this.BuildModelAsync(this.Input.ReturnUrl);
        return this.Page();
    }

    private async Task BuildModelAsync(string returnUrl)
    {
        this.Input = new InputModel
        {
            ReturnUrl = returnUrl
        };

        var context = await this._interaction.GetAuthorizationContextAsync(returnUrl);
        if (context?.IdP != null && await this._schemeProvider.GetSchemeAsync(context.IdP) != null)
        {
            var local = context.IdP == Duende.IdentityServer.IdentityServerConstants.LocalIdentityProvider;

            // this is meant to short circuit the UI and only trigger the one external IdP
            this.View = new ViewModel
            {
                EnableLocalLogin = local,
            };

            this.Input.Username = context?.LoginHint;

            if (!local)
            {
                this.View.ExternalProviders = new[] { new ViewModel.ExternalProvider { AuthenticationScheme = context.IdP } };
            }

            return;
        }

        var schemes = await this._schemeProvider.GetAllSchemesAsync();

        var providers = schemes
            .Where(x => x.DisplayName != null)
            .Select(x => new ViewModel.ExternalProvider
            {
                DisplayName = x.DisplayName ?? x.Name,
                AuthenticationScheme = x.Name
            }).ToList();

        var dyanmicSchemes = (await this._identityProviderStore.GetAllSchemeNamesAsync())
            .Where(x => x.Enabled)
            .Select(x => new ViewModel.ExternalProvider
            {
                AuthenticationScheme = x.Scheme,
                DisplayName = x.DisplayName
            });
        providers.AddRange(dyanmicSchemes);


        var allowLocal = true;
        var client = context?.Client;
        if (client != null)
        {
            allowLocal = client.EnableLocalLogin;
            if (client.IdentityProviderRestrictions != null && client.IdentityProviderRestrictions.Any())
            {
                providers = providers.Where(provider => client.IdentityProviderRestrictions.Contains(provider.AuthenticationScheme)).ToList();
            }
        }

        this.View = new ViewModel
        {
            AllowRememberLogin = LoginOptions.AllowRememberLogin,
            EnableLocalLogin = allowLocal && LoginOptions.AllowLocalLogin,
            ExternalProviders = providers.ToArray()
        };
    }
}
