﻿using System;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using GameStatsApp.Interfaces.Services;
using GameStatsApp.Model;
using GameStatsApp.Model.Data;
using GameStatsApp.Model.ViewModels;
using GameStatsApp.Common.Extensions;
using System.Security.Claims;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Threading.Tasks;
using System.Linq;
using Serilog;
using Microsoft.AspNetCore.Authorization;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Http.Extensions;
using Newtonsoft.Json;

namespace GameStatsApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly IUserService _userService = null;
        private readonly IAuthService _authService = null;
        private readonly IConfiguration _config = null;
        private readonly ILogger _logger = null;

        public HomeController(IUserService userService, IAuthService authService, IConfiguration config, ILogger logger)
        {
            _userService = userService;
            _authService = authService;
            _config = config;
            _logger = logger;
        }

        public ActionResult Index()
        {
            var indexVM = new IndexViewModel();
            indexVM.IsAuth = User.Identity.IsAuthenticated;

            if (indexVM.IsAuth)
            {
                indexVM.UserID = Convert.ToInt32(User.FindFirstValue(ClaimTypes.NameIdentifier));
                indexVM.UserGameLists = _userService.GetUserGameLists(indexVM.UserID).ToList();
            }

            return View(indexVM);
        }

        public ViewResult Error()
        {
            return View();
        }

        [HttpGet]
        public ActionResult Login()
        {
            var loginVM = new LoginViewModel() {
                GClientID = _config.GetSection("Auth").GetSection("Google").GetSection("ClientID").Value,
                FBClientID = _config.GetSection("Auth").GetSection("Facebook").GetSection("ClientID").Value,
                FBApiVer = _config.GetSection("Auth").GetSection("Facebook").GetSection("ApiVersion").Value,
            };

            return View(loginVM);
        }

        [HttpPost]
        public JsonResult Login(LoginViewModel loginVM)
        {
            var success = false;
            List<string> errorMessages = null;

            try
            {
                if (!_userService.EmailExists(loginVM.Email, true))
                {
                    ModelState.AddModelError("Login", "Email not found");
                }

                if (!_userService.PasswordMatches(loginVM.Password, loginVM.Email))
                {
                    ModelState.AddModelError("Login", "Invalid password");
                }

                if (ModelState.IsValid)
                {
                    var userVW = _userService.GetUserViews(i => i.Email == loginVM.Email).FirstOrDefault();
                    LoginUser(userVW);
                    success = true;
                }
                else
                {
                    success = false;
                    errorMessages = ModelState.Values.SelectMany(i => i.Errors).Select(i => i.ErrorMessage).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Login");
                success = false;
                errorMessages = new List<string>() { "Error logging user in." };
            }

            return Json(new { success = success, errorMessages = errorMessages });
        }

        [HttpGet]
        public async Task<ActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var baseUrl = string.Format("{0}://{1}{2}", Request.Scheme, Request.Host, Request.PathBase);
            var refUrl = Request.Headers["Referer"].ToString();
            var url = !string.IsNullOrWhiteSpace(refUrl) ? refUrl : baseUrl;

            return Redirect(url);
        }

        [HttpGet]
        public ActionResult SignUp()
        {
            var signUpVM = new SignUpViewModel() {
                GClientID = _config.GetSection("Auth").GetSection("Google").GetSection("ClientID").Value,
                FBClientID = _config.GetSection("Auth").GetSection("Facebook").GetSection("ClientID").Value,
                FBApiVer = _config.GetSection("Auth").GetSection("Facebook").GetSection("ApiVersion").Value,
            };

            return View(signUpVM);
        }

        [HttpPost]
        public JsonResult SignUp(SignUpViewModel signUpVM)
        {
            var success = false;
            List<string> errorMessages = null;

            try
            {
                if (_userService.EmailExists(signUpVM.Email, false))
                {
                    ModelState.AddModelError("SignUp", "Email already exists for another user");
                }

                if (ModelState.IsValid)
                {
                    _ = _userService.SendActivationEmail(signUpVM.Email).ContinueWith(t => _logger.Error(t.Exception, "SendActivationEmail"), TaskContinuationOptions.OnlyOnFaulted);
                    success = true;
                }
                else
                {
                    success = false;
                    errorMessages = ModelState.Values.SelectMany(i => i.Errors).Select(i => i.ErrorMessage).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "SignUp");
                success = false;
                errorMessages = new List<string>() { "Error signing up user" };
            }

            return Json(new { success = success, errorMessages = errorMessages });
        }

        public async Task<JsonResult> LoginOrSignUpWithSocial(string accessToken, int socialAccountTypeID)
        {
            var success = false;
            var isNewUser = false;
            List<string> errorMessages = null;
 
            try
            {
                var result = await _authService.ValidateSocialToken(accessToken, socialAccountTypeID);

                if (result == null)
                {
                    ModelState.AddModelError("LoginOrSignUpWithSocial", "Invalid Token");
                }

                var userVW = _userService.GetUserViews(i => i.Email == result.Email && i.Active).FirstOrDefault();              
                if (_userService.EmailExists(result.Email, false) && userVW == null)
                {
                    ModelState.AddModelError("LoginOrSignUpWithSocial", "Email not found");
                }

                if (ModelState.IsValid)           
                {
                    if (userVW != null)
                    {
                        LoginUser(userVW);
                        success = true;
                    }
                    else
                    {
                        var username = result.Email.Split('@')[0];
                        if (_userService.UsernameExists(username, false))
                        {
                            username += '_' + ((String)result.Email).GetHashCode();
                        }
                        var pass = StringExtensions.GeneratePassword(15, 2);

                        _userService.CreateUser(result.Email, username, pass);
                        userVW = _userService.GetUserViews(i => i.Email == result.Email).FirstOrDefault();
                        LoginUser(userVW);
                        _ = _userService.SendConfirmRegistrationEmail(userVW.Email, userVW.Username).ContinueWith(t => _logger.Error(t.Exception, "SendConfirmRegistrationEmail"), TaskContinuationOptions.OnlyOnFaulted);
                        isNewUser = true;
                        success = true;
                    }
                }
                else
                {
                    success = false;
                    errorMessages = ModelState.Values.SelectMany(i => i.Errors).Select(i => i.ErrorMessage).ToList();
                }                
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "LoginOrSignUpByGoogle");
                success = false;
                errorMessages = new List<string>() { "Error logging in with Google" };
            }

            return Json(new { success = success, isnewuser = isNewUser, errorMessages = errorMessages });
        }

        [HttpGet]
        public ViewResult Activate(string email, long expirationTime, string token)
        {
            var activateUserVM = _userService.GetActivateUser(email, expirationTime, token);
            HttpContext.Session.Set<string>("Email", email);

            return View(activateUserVM);
        }

        [HttpPost]
        public JsonResult Activate(ActivateViewModel activateUserVM)
        {
            var success = false;
            List<string> errorMessages = null;

            try
            {
                activateUserVM.Email = HttpContext.Session.Get<string>("Email");

                if (_userService.EmailExists(activateUserVM.Email, false))
                {
                    ModelState.AddModelError("Activate", "Email already exists for another user");
                }

                if (_userService.UsernameExists(activateUserVM.Username, false))
                {
                    ModelState.AddModelError("Activate", "Username already exists for another user");
                }

                if (ModelState.IsValid)
                {
                    _userService.CreateUser(activateUserVM.Email, activateUserVM.Username, activateUserVM.Password);
                    var userVW = _userService.GetUserViews(i => i.Email == activateUserVM.Email).FirstOrDefault();
                    LoginUser(userVW);
                    _ = _userService.SendConfirmRegistrationEmail(userVW.Email, userVW.Username).ContinueWith(t => _logger.Error(t.Exception, "SendConfirmRegistrationEmail"), TaskContinuationOptions.OnlyOnFaulted);
                    success = true;
                }
                else
                {
                    success = false;
                    errorMessages = ModelState.Values.SelectMany(i => i.Errors).Select(i => i.ErrorMessage).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Activate");
                success = false;
                errorMessages = new List<string>() { "Error creating user" };
            }

            return Json(new { success = success, errorMessages = errorMessages });
        }

        [HttpGet]
        public ActionResult ResetPassword(string email)
        {
            var resetPassVM = new ResetPasswordViewModel() { Email = email };
            return View(resetPassVM);
        }

        [AllowAnonymous]
        [HttpPost]
        public JsonResult ResetPassword(ResetPasswordViewModel resetPassVM)
        {
            var success = false;
            List<string> errorMessages = null;

            try
            {
                if (!_userService.EmailExists(resetPassVM.Email, true))
                {
                    ModelState.AddModelError("ResetPassword", "Email not found");
                }

                if (ModelState.IsValid)
                {
                    _ = _userService.SendResetPasswordEmail(resetPassVM.Email).ContinueWith(t => _logger.Error(t.Exception, "SendResetPasswordEmail"), TaskContinuationOptions.OnlyOnFaulted);
                    success = true;
                }
                else
                {
                    success = false;
                    errorMessages = ModelState.Values.SelectMany(i => i.Errors).Select(i => i.ErrorMessage).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ResetPassword");
                success = false;
                errorMessages = new List<string>() { "Error resetting password" };
            }

            return Json(new { success = success, errorMessages = errorMessages });
        }

        [HttpGet]
        public ViewResult ChangePassword(string email, long expirationTime, string token)
        {
            var changePassVM = _userService.GetChangePassword(email, expirationTime, token);
            HttpContext.Session.Set<string>("Email", email);

            return View(changePassVM);
        }

        [HttpPost]
        public JsonResult ChangePassword(ChangePasswordViewModel changePassVM)
        {
            var success = false;
            List<string> errorMessages = null;

            try
            {
                var email = HttpContext.Session.Get<string>("Email");
                if (_userService.PasswordMatches(changePassVM.Password, email))
                {
                    ModelState.AddModelError("ChangePassword", "Password must differ from previous password");
                }

                if (ModelState.IsValid)
                {
                    _userService.ChangeUserPassword(email, changePassVM.Password);
                    success = true;
                }
                else
                {
                    success = false;
                    errorMessages = ModelState.Values.SelectMany(i => i.Errors).Select(i => i.ErrorMessage).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ChangePassword");
                success = false;
                errorMessages = new List<string>() { "Error changing password" };
            }

            return Json(new { success = success, errorMessages = errorMessages });
        }

        [HttpPost]
        public JsonResult ChangeUsername(ChangeUsernameViewModel changeUsernameVM)
        {
            var success = false;
            List<string> errorMessages = null;

            try
            {
                var email = User.FindFirstValue(ClaimTypes.Email);
                if (_userService.UsernameExists(changeUsernameVM.Username, false))
                {
                    ModelState.AddModelError("Activate", "Username already exists for another user");
                }

                if (ModelState.IsValid)
                {
                    _userService.ChangeUsername(email, changeUsernameVM.Username);
                    var userVW = _userService.GetUserViews(i => i.Email == email).FirstOrDefault();
                    LoginUser(userVW);
                    success = true;
                }
                else
                {
                    success = false;
                    errorMessages = ModelState.Values.SelectMany(i => i.Errors).Select(i => i.ErrorMessage).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ChangeUsername");
                success = false;
                errorMessages = new List<string>() { "Error changing username" };
            }

            return Json(new { success = success, errorMessages = errorMessages });
        }

        private async void LoginUser(UserView userVW)
        {
            var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.NameIdentifier, userVW.UserID.ToString()),
                            new Claim(ClaimTypes.Email, userVW.Email),
                            new Claim(ClaimTypes.Name, userVW.Username),
                            new Claim("theme", userVW.IsDarkTheme ? "theme-dark" : "theme-light")
                        };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));
        }
        
        [HttpGet]
        [Authorize]
        public ViewResult Welcome(bool? authSuccess = null)
        {
            var userID = Convert.ToInt32(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var userVW = _userService.GetUserViews(i => i.UserID == userID).FirstOrDefault();
            var gameAccountTypeIDs = !string.IsNullOrWhiteSpace(userVW.GameAccountTypeIDs) ? userVW.GameAccountTypeIDs.Split(",").Select(i => int.Parse(i)).ToList() : new List<int>();
            var redirectUri = _config.GetSection("Auth").GetSection("Microsoft").GetSection("WelcomeRedirectUri").Value;
            var windowsLiveAuthUrl = _authService.GetWindowsLiveAuthUrl(redirectUri);

            var welcomeVM = new WelcomeViewModel() { Username = userVW.Username,                                              
                                                     WindowsLiveAuthUrl = windowsLiveAuthUrl,
                                                     GameAccountTypeIDs = gameAccountTypeIDs,
                                                     AuthSuccess = authSuccess };

            return View(welcomeVM);
        }

        public async Task<ActionResult> MicrosoftAuthCallbackWelcome()
        {
            var success = false;

            try
            {
                var code = Request.Query["code"].ToString();
                if (!string.IsNullOrWhiteSpace(code))
                {
                    var redirectUri = _config.GetSection("Auth").GetSection("Microsoft").GetSection("WelcomeRedirectUri").Value;
                    var tokenResponse = await _authService.Authenticate(code, redirectUri);

                    var userID = Convert.ToInt32(User.FindFirstValue(ClaimTypes.NameIdentifier));
                    _userService.SaveUserGameAccount(userID, (int)GameAccountType.Xbox, tokenResponse);

                    success = true;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MicrosoftAuthCallbackWelcome");
                success = false;
            }            

            return RedirectToAction("Welcome", new { authSuccess = success });
        }

        [HttpGet]
        [Authorize] 
        public ViewResult ImportGames(bool? authSuccess = null, int? authGameAccountTypeID = null)
        {
            var userID = Convert.ToInt32(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var userGameAccountVMs = _userService.GetUserGameAccounts(userID).ToList();

            var importGamesVM = new ImportGamesViewModel() { UserGameAccounts = userGameAccountVMs,
                                                             AuthSuccess = authSuccess,
                                                             AuthGameAccountTypeID = authGameAccountTypeID };

            return View(importGamesVM);
        }

        [HttpPost]
        public async Task<ActionResult> ImportGames(int userGameAccountID, bool isImportAll)
        {
            var success = false;
            List<string> errorMessages = null;

            try
            {
                var userID = Convert.ToInt32(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var result = await _userService.GetRefreshedUserGameAccount(userID, userGameAccountID);
                
                if (!string.IsNullOrWhiteSpace(result.Item2))
                {
                    Redirect(result.Item2);
                }
                else
                {
                    await _userService.ImportGamesFromUserGameAccount(userID, result.Item1, isImportAll);
                }

                success = true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ImportGames");
                success = false;
                errorMessages = new List<string>() { "Error importing games" };
            }

            return Json(new { success = success, errorMessages = errorMessages });
        }      

        public async Task<ActionResult> MicrosoftAuthCallbackImportGames()
        {
            var success = false;

            try
            {
                var code = Request.Query["code"].ToString();
                if (!string.IsNullOrWhiteSpace(code))
                {
                    var redirectUri = _config.GetSection("Auth").GetSection("Microsoft").GetSection("ImportGamesRedirectUri").Value;
                    var tokenResponse = await _authService.Authenticate(code, redirectUri);

                    var userID = Convert.ToInt32(User.FindFirstValue(ClaimTypes.NameIdentifier));
                    _userService.SaveUserGameAccount(userID, (int)GameAccountType.Xbox, tokenResponse);

                    success = true;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MicrosoftAuthCallbackImportGames");
                success = false;
            }            

            return RedirectToAction("ImportGames", new { authSuccess = success, authGameAccountTypeID = (int)GameAccountType.Xbox });
        }                  

        [AllowAnonymous]
        [HttpGet]
        public IActionResult ActiveEmailExists(string email)
        {
            var result = _userService.EmailExists(email, true);

            return Json(result);
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult UsernameNotExists(string username)
        {
            var result = !_userService.UsernameExists(username, false);

            return Json(result);
        }
     
        [AllowAnonymous]
        [HttpGet]
        public IActionResult PasswordNotMatches(string password)
        {
            var email = HttpContext.Session.Get<string>("Email");
            var result = !_userService.PasswordMatches(password, email);

            return Json(result);
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult EmailNotExists(string email)
        {
            var result = !_userService.EmailExists(email, false);

            return Json(result);
        }     
    }
}
