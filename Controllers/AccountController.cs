using System.Security.Claims;
using HostelEase.Data;
using HostelEase.Helpers;
using HostelEase.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HostelEase.Controllers;

public class AccountController : Controller
{
    private readonly UserRepository _users;
    private readonly StudentRepository _students;

    public AccountController(UserRepository users, StudentRepository students)
    {
        _users = users; _students = students;
    }

    // ---------- Login / Logout ----------
    [HttpGet, AllowAnonymous]
    public IActionResult Login(string returnUrl = null)
    {
        if (User.Identity.IsAuthenticated) return RedirectToAction("Index", "Dashboard");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel m)
    {
        if (!ModelState.IsValid) return View(m);

        var user = _users.GetByEmail(m.Email.Trim().ToLowerInvariant());
        if (user == null || !PasswordHasher.Verify(m.Password, user.PasswordHash))
        {
            ModelState.AddModelError("", "Invalid email or password.");
            return View(m);
        }
        if (!user.IsActive)
        {
            ModelState.AddModelError("", "This account has been deactivated. Please contact the hostel office.");
            return View(m);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserID.ToString()),
            new(ClaimTypes.Name, user.Email),
            new(ClaimTypes.Role, user.Role)
        };
        if (user.Role == "Student")
        {
            var student = _students.GetByUserId(user.UserID);
            if (student != null)
            {
                claims.Add(new Claim("StudentId", student.StudentID.ToString()));
                claims.Add(new Claim("FullName", student.FullName));
            }
        }
        else claims.Add(new Claim("FullName", "Administrator"));

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

        if (!string.IsNullOrEmpty(m.ReturnUrl) && Url.IsLocalUrl(m.ReturnUrl)) return LocalRedirect(m.ReturnUrl);
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    // ---------- Student self-registration ----------
    [HttpGet, AllowAnonymous]
    public IActionResult Register() => View(new RegisterViewModel { Semester = 1 });

    [HttpPost, AllowAnonymous]
    public IActionResult Register(RegisterViewModel m)
    {
        m.Email = (m.Email ?? "").Trim().ToLowerInvariant();
        if (ModelState.IsValid)
        {
            if (_users.EmailExists(m.Email)) ModelState.AddModelError(nameof(m.Email), "This email is already registered");
            if (_users.StudentNumberExists(m.StudentNumber.Trim())) ModelState.AddModelError(nameof(m.StudentNumber), "This student ID is already registered");
        }
        if (!ModelState.IsValid) return View(m);

        _users.CreateStudent(m, PasswordHasher.Hash(m.Password));
        TempData["Success"] = "Registration successful. You can now sign in.";
        return RedirectToAction(nameof(Login));
    }

    // ---------- Admin creates another admin ----------
    [HttpGet, Authorize(Roles = "Admin")]
    public IActionResult CreateAdmin() => View(new CreateAdminViewModel());

    [HttpPost, Authorize(Roles = "Admin")]
    public IActionResult CreateAdmin(CreateAdminViewModel m)
    {
        m.Email = (m.Email ?? "").Trim().ToLowerInvariant();
        if (ModelState.IsValid && _users.EmailExists(m.Email))
            ModelState.AddModelError(nameof(m.Email), "This email is already registered");
        if (!ModelState.IsValid) return View(m);

        _users.CreateAdmin(m.Email, PasswordHasher.Hash(m.Password));
        TempData["Success"] = $"Administrator account created for {m.Email}.";
        return RedirectToAction("Index", "Dashboard");
    }

    // ---------- Change password (any signed-in user) ----------
    [HttpGet, Authorize]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost, Authorize]
    public IActionResult ChangePassword(ChangePasswordViewModel m)
    {
        if (!ModelState.IsValid) return View(m);
        var user = _users.GetByEmail(User.Identity.Name);
        if (user == null || !PasswordHasher.Verify(m.CurrentPassword, user.PasswordHash))
        {
            ModelState.AddModelError(nameof(m.CurrentPassword), "Current password is incorrect");
            return View(m);
        }
        _users.UpdatePassword(user.UserID, PasswordHasher.Hash(m.NewPassword));
        TempData["Success"] = "Your password was changed.";
        return RedirectToAction("Index", "Dashboard");
    }

    // ---------- Student profile ----------
    [HttpGet, Authorize(Roles = "Student")]
    public IActionResult Profile() => View(_students.GetById(User.StudentId()));

    [HttpPost, Authorize(Roles = "Student")]
    public IActionResult Profile(Student s)
    {
        s.StudentID = User.StudentId();     // never trust the id from the form
        if (!ModelState.IsValid)
        {
            var original = _students.GetById(s.StudentID);
            s.StudentNumber = original.StudentNumber; s.Email = original.Email;
            return View(s);
        }
        _students.Update(s);
        TempData["Success"] = "Profile updated.";
        return RedirectToAction(nameof(Profile));
    }
}
