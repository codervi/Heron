using Heron.Web.Models;
using Heron.Web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Heron.Web.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<AppUser> _signIn;
    private readonly UserManager<AppUser> _users;

    public AccountController(SignInManager<AppUser> signIn, UserManager<AppUser> users)
    {
        _signIn = signIn;
        _users = users;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginForm());
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginForm form, string? returnUrl)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(form);

        var result = await _signIn.PasswordSignInAsync(form.Email, form.Password, isPersistent: false, lockoutOnFailure: true);
        if (result.Succeeded)
            return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction("Index", "Home");

        // the same message whether the email is unknown, the password is wrong or the account is locked
        ModelState.AddModelError("", result.IsLockedOut ? "Too many attempts. Try again in a few minutes." : "Email or password is wrong.");
        return View(form);
    }

    [HttpGet]
    public IActionResult Register() => View(new RegisterForm());

    [HttpPost]
    public async Task<IActionResult> Register(RegisterForm form)
    {
        if (!ModelState.IsValid) return View(form);

        var user = new AppUser { UserName = form.Email, Email = form.Email, FullName = form.FullName.Trim() };
        var result = await _users.CreateAsync(user, form.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View(form);
        }

        await _users.AddToRoleAsync(user, Roles.Customer);
        await _signIn.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult Denied() => View();
}
