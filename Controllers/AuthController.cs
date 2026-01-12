using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Models;
using Project.Services;
using System.Security.Claims;
using OtpNet;
using System.Net.Http;
using System.IO;
using System.Threading.Tasks;

namespace Project.Controllers
{
    public class AuthController : Controller
    {
        private readonly mycontext db;
        private readonly IEmailService emailService;
        private readonly IHttpClientFactory _httpClientFactory;

        public AuthController(mycontext context, IEmailService email, IHttpClientFactory httpClientFactory)
        {
            emailService = email;
            db = context;
            _httpClientFactory = httpClientFactory;
        }

        public IActionResult Index()
        {
            return RedirectToAction("Login");
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string email, string pass) 
        {
            var user = db.users.FirstOrDefault(x => x.Email == email && x.Password == pass);
            if (user != null)
            {
                if (user.IsTwoFactorEnabled) {
                     HttpContext.Session.Clear();
                     HttpContext.Session.SetInt32("2fa_UserId", user.UserId);
                     return RedirectToAction("VerifyTwoFactor");
                }

                HttpContext.Session.SetString("Role", user.Role);
                HttpContext.Session.SetString("Name", user.Name); 
                HttpContext.Session.SetInt32("UserId", user.UserId);
                HttpContext.Session.SetString("Email", user.Email);
                
                if (!string.IsNullOrEmpty(user.Avatar))
                {
                    HttpContext.Session.SetString("Avatar", user.Avatar);
                }
                
                if (string.IsNullOrEmpty(user.Theme))
                {
                    user.Theme = "dark";
                    db.users.Update(user);
                    db.SaveChanges();
                }

                HttpContext.Session.SetString("Theme", user.Theme);
                HttpContext.Session.SetString("UseCustomCursor", user.UseCustomCursor.ToString().ToLower());
                
                return GetLoginRedirect(user);
            }
            ViewBag.Error = "Kuch to Garbad hai";
            return View();
        }

        public IActionResult Register()
        {
            ViewBag.ShowRegister = true;
            return View("Login");
        }

        [HttpPost]
        public IActionResult Register(User user)
        {
            user.Role = "User";
            db.users.Add(user);
            try
            {
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.InnerException?.Message);
                throw;
            }
            db.SaveChanges();
            return RedirectToAction("Login");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                ViewBag.Message = "Please enter your email.";
                return View();
            }

            var user = db.users.FirstOrDefault(u => u.Email == email);

            if (user != null)
            {
                user.ResetToken = Guid.NewGuid().ToString();
                user.ResetTokenExpiry = DateTime.Now.AddHours(1);
                db.Update(user);
                db.SaveChanges();

                var resetLink = Url.Action(
                    "ResetPassword",
                    "Auth",
                    new { email = user.Email, token = user.ResetToken },
                    Request.Scheme);

                string subject = "Reset your password";
                string body = $"Click the link to reset your password: <a href='{resetLink}'>Reset Password</a>";

                emailService.SendEmailAsync(user.Email, subject, body).GetAwaiter().GetResult();
            }

            ViewBag.Message = "If an account exists with this email, a reset link has been sent.";
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
                return RedirectToAction("ForgotPassword");

            var model = new ChangePasswordViewModel
            {
                Email = email,
                Token = token
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ResetPassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = db.users.FirstOrDefault(u =>
                u.Email == model.Email &&
                u.ResetToken == model.Token &&
                u.ResetTokenExpiry > DateTime.Now);

            if (user == null)
            {
                ModelState.AddModelError("", "Invalid or expired token.");
                return View(model);
            }

            user.Password = model.NewPassword;
            user.ResetToken = null;
            user.ResetTokenExpiry = null;

            db.Update(user);
            db.SaveChanges();

            return RedirectToAction("ResetPasswordSuccess");
        }

        private IActionResult GetLoginRedirect(User user)
        {
            bool isDataMissing = user.Age == null || string.IsNullOrEmpty(user.Sex) || string.IsNullOrEmpty(user.Interests);

            if ((!user.IsOnboarded || isDataMissing) && user.Role != "Admin")
            {
                return RedirectToAction("Onboarding");
            }

            if (user.Role == "Admin") return RedirectToAction("Index", "Admin");
            return RedirectToAction("Index", "Home");
        }

        public IActionResult Onboarding()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");
            
            var user = db.users.Find(userId);
            bool isDataMissing = user != null && (user.Age == null || string.IsNullOrEmpty(user.Sex) || string.IsNullOrEmpty(user.Interests));

            if (user == null || (user.IsOnboarded && !isDataMissing)) return RedirectToAction("Index", "Home");
            
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> Onboarding(int Age, string Sex, string Interests, string Role)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            var user = db.users.Find(userId);
            if (user != null)
            {
                user.Age = Age;
                user.Sex = Sex;
                user.Interests = Interests;
                // Force Role to User if it's not Admin (though onboarding usually only happens for Users)
                user.Role = "User"; 
                user.IsOnboarded = true;

                db.users.Update(user);
                await db.SaveChangesAsync();

                HttpContext.Session.SetString("Role", user.Role);

                if (user.Role == "Admin") return RedirectToAction("Index", "Admin");
                return RedirectToAction("Index", "Home");
            }

            return RedirectToAction("Login");
        }

        public IActionResult ResetPasswordSuccess()
        {
            return View();
        }

        public IActionResult VerifyTwoFactor()
        {
            if (HttpContext.Session.GetInt32("2fa_UserId") == null)
            {
                return RedirectToAction("Login");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> VerifyTwoFactor(VerifyTwoFactorViewModel model)
        {
            await HttpContext.Session.LoadAsync();
            var userId = HttpContext.Session.GetInt32("2fa_UserId");
            if (userId == null) return RedirectToAction("Login");

            var user = db.users.Find(userId);
            if (user == null) return RedirectToAction("Login");

            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Keys
                    .Select(k => $"{k}: {string.Join(", ", ModelState[k].Errors.Select(e => e.ErrorMessage))}"));
                TempData["Error"] = $"Validation Error: {errors}";
                return View(model);
            }

            if (string.IsNullOrEmpty(user.TwoFactorSecret))
            {
                 ModelState.AddModelError("", "2FA setup is incomplete for this user.");
                 return View(model);
            }

            var secretBytes = Base32Encoding.ToBytes(user.TwoFactorSecret);
            var totp = new Totp(secretBytes);
            
            bool valid = totp.VerifyTotp(model.Code, out long timeStepMatched, new VerificationWindow(30, 30));

            if (!valid)
            {
                TempData["Error"] = "Invalid verification code. Please try again.";
                ModelState.AddModelError("", "Invalid verification code.");
                return View(model);
            }

            HttpContext.Session.SetString("Role", user.Role);
            HttpContext.Session.SetString("Name", user.Name); 
            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("Email", user.Email);

            HttpContext.Session.Remove("2fa_UserId");
            
            if (!string.IsNullOrEmpty(user.Avatar))
            {
                HttpContext.Session.SetString("Avatar", user.Avatar);
            }
            
            if (string.IsNullOrEmpty(user.Theme))
            {
                user.Theme = "dark";
                db.users.Update(user);
                await db.SaveChangesAsync();
            }

            HttpContext.Session.SetString("UseCustomCursor", user.UseCustomCursor.ToString().ToLower());
            
            await HttpContext.Session.CommitAsync();

            return GetLoginRedirect(user);
        }

        public IActionResult GoogleLogin()
        {
            var properties = new AuthenticationProperties { RedirectUri = Url.Action("GoogleResponse") };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        public IActionResult DiscordLogin()
        {
            var properties = new AuthenticationProperties { RedirectUri = Url.Action("DiscordResponse") };
            return Challenge(properties, "Discord");
        }

        public IActionResult GithubLogin()
        {
            var properties = new AuthenticationProperties { RedirectUri = Url.Action("GithubResponse") };
            return Challenge(properties, "GitHub");
        }

        public IActionResult GithubResponse()
        {
            var result = HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme).GetAwaiter().GetResult();
            if (result.Principal == null) return RedirectToAction("Login");

            var email = result.Principal.FindFirstValue(ClaimTypes.Email);
            var name = result.Principal.FindFirstValue(ClaimTypes.Name);
            var githubAvatarUrl = result.Principal.FindFirstValue("urn:github:avatar:url") 
                                 ?? result.Principal.FindFirstValue("avatar_url");

            var localAvatarPath = string.Empty;
            if (!string.IsNullOrEmpty(githubAvatarUrl))
            {
                localAvatarPath = DownloadImage(githubAvatarUrl);
            }

            var user = db.users.FirstOrDefault(u => u.Email == email);

            if (user == null)
            {
                user = new User
                {
                    Email = email,
                    Name = name,
                    Role = "User",
                    Avatar = localAvatarPath,
                    Password = null
                };
                db.users.Add(user);
            }
            else
            {
                user.Name = name;
                if (!string.IsNullOrEmpty(localAvatarPath))
                {
                    user.Avatar = localAvatarPath;
                }
                db.users.Update(user);
            }

            db.SaveChanges();

            HttpContext.Session.SetString("Role", user.Role);
            HttpContext.Session.SetString("Name", user.Name);
            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("Email", user.Email);
            if (!string.IsNullOrEmpty(user.Avatar))
            {
                HttpContext.Session.SetString("Avatar", user.Avatar);
            }
            if (string.IsNullOrEmpty(user.Theme))
            {
                user.Theme = "dark";
                db.users.Update(user);
                db.SaveChanges();
            }
            HttpContext.Session.SetString("Theme", user.Theme);
            HttpContext.Session.SetString("UseCustomCursor", user.UseCustomCursor.ToString().ToLower());

            return GetLoginRedirect(user);
        }

        public IActionResult DiscordResponse()
        {
            var result = HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme).GetAwaiter().GetResult();
            if (result.Principal == null) return RedirectToAction("Login");

            var email = result.Principal.FindFirstValue(ClaimTypes.Email);
            var name = result.Principal.FindFirstValue(ClaimTypes.Name);
            
            var userId = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var avatarHash = result.Principal.FindFirstValue("urn:discord:avatar:hash");
            var localAvatarPath = string.Empty;

            if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(avatarHash))
            {
                var discordAvatarUrl = $"https://cdn.discordapp.com/avatars/{userId}/{avatarHash}.png";
                localAvatarPath = DownloadImage(discordAvatarUrl);
            }

            var user = db.users.FirstOrDefault(u => u.Email == email);

            if (user == null)
            {
                user = new User
                {
                    Email = email,
                    Name = name,
                    Role = "User",
                    Avatar = localAvatarPath,
                    Password = null
                };
                db.users.Add(user);
            }
            else
            {
                user.Name = name;
                if (!string.IsNullOrEmpty(localAvatarPath))
                {
                    user.Avatar = localAvatarPath;
                }
                db.users.Update(user);
            }

            db.SaveChanges();

            HttpContext.Session.SetString("Role", user.Role);
            HttpContext.Session.SetString("Name", user.Name);
            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("Email", user.Email);
            if (!string.IsNullOrEmpty(user.Avatar))
            {
                HttpContext.Session.SetString("Avatar", user.Avatar);
            }
            
            if (string.IsNullOrEmpty(user.Theme))
            {
                user.Theme = "dark";
                db.users.Update(user);
                db.SaveChanges();
            }
            HttpContext.Session.SetString("Theme", user.Theme);
            HttpContext.Session.SetString("UseCustomCursor", user.UseCustomCursor.ToString().ToLower());

            return GetLoginRedirect(user);
        }

        public IActionResult GoogleResponse()
        {
            var result = HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme).GetAwaiter().GetResult();
            if (result.Principal == null) return RedirectToAction("Login");

            var email = result.Principal.FindFirstValue(ClaimTypes.Email);
            var name = result.Principal.FindFirstValue(ClaimTypes.Name);
            var googleAvatarUrl = result.Principal.FindFirstValue("urn:google:picture");

            var localAvatarPath = string.Empty;
            if (!string.IsNullOrEmpty(googleAvatarUrl))
            {
                localAvatarPath = DownloadImage(googleAvatarUrl);
            }

            var user = db.users.FirstOrDefault(u => u.Email == email);

            if (user == null)
            {
                user = new User
                {
                    Email = email,
                    Name = name,
                    Role = "User",
                    Avatar = localAvatarPath,
                    Password = null
                };
                db.users.Add(user);
            }
            else
            {
                user.Name = name;
                if (!string.IsNullOrEmpty(localAvatarPath))
                {
                    user.Avatar = localAvatarPath;
                }
                db.users.Update(user);
            }

            db.SaveChanges();

            HttpContext.Session.SetString("Role", user.Role);
            HttpContext.Session.SetString("Name", user.Name);
            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("Email", user.Email);
            if (!string.IsNullOrEmpty(user.Avatar))
            {
                HttpContext.Session.SetString("Avatar", user.Avatar);
            }

            if (string.IsNullOrEmpty(user.Theme))
            {
                user.Theme = "dark";
                db.users.Update(user);
                db.SaveChanges();
            }
            HttpContext.Session.SetString("Theme", user.Theme);
            HttpContext.Session.SetString("UseCustomCursor", user.UseCustomCursor.ToString().ToLower());

            return GetLoginRedirect(user);
        }

        private string DownloadImage(string url)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var fileExtension = ".jpg"; 
                var fileName = Guid.NewGuid().ToString() + fileExtension;
                var directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "avatars");

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                var filePath = Path.Combine(directoryPath, fileName);
                
                var response = Task.Run(() => client.GetAsync(url)).GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode)
                {
                    using (var fs = new FileStream(filePath, FileMode.Create))
                    {
                        Task.Run(() => response.Content.CopyToAsync(fs)).GetAwaiter().GetResult();
                    }
                    return "/images/avatars/" + fileName;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading avatar: {ex.Message}");
            }
            return string.Empty;
        }
    }
}
