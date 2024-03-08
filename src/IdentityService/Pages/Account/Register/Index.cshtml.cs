using System.Security.Claims;
using IdentityModel;
using IdentityService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityService.Pages.Account.Register
{
    [SecurityHeaders]
    [AllowAnonymous]
    public class Index : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public Index(UserManager<ApplicationUser> userManager)
        {
            this._userManager = userManager;
        }

        [BindProperty]
        public RegisterViewModel Input { get; set; }

        [BindProperty]
        public bool RegisterSuccess { get; set; }

        public IActionResult OnGet(string returnUrl)
        {
            this.Input = new RegisterViewModel
            {
                ReturnUrl = returnUrl,
            };

            return this.Page();
        }

        public async Task<IActionResult> OnPost()
        {
            if (this.Input.Button != "register")
                return this.Redirect("~/");

            if (this.ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = this.Input.Username,
                    Email = this.Input.Email,
                    EmailConfirmed = true
                };

                var result = await this._userManager.CreateAsync(user, this.Input.Password);

                if (result.Succeeded)
                {
                    await this._userManager.AddClaimsAsync(user, new Claim[]
                    {
                        new(JwtClaimTypes.Name, this.Input.FullName)
                    });

                    this.RegisterSuccess = true;
                }
            }

            return this.Page();
        }
    }
}
