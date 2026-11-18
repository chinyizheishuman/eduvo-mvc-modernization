using JQuery_ECommerceAss.Common.Enum;
using JQuery_ECommerceAss.Common.Security;
using JQuery_ECommerceAss.Common.Pricing;
using JQuery_ECommerceAss.Data;
using JQuery_ECommerceAss.Models;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace JQuery_ECommerceAss.Controllers
{
    [AutoValidateAntiforgeryToken]
    [Authorize(Roles = "Shopper", Policy = "ShopperOnly")]
    public class UserController : Controller
    {
        private static readonly AccountVerificationCode VerificationCode = new("ShopperVerificationCode");
        private readonly AppDbContext db;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserController> _logger;

        public UserController(
            AppDbContext _db,
            IWebHostEnvironment webHostEnvironment,
            IConfiguration configuration,
            ILogger<UserController> logger)
        {
            db = _db;
            _webHostEnvironment = webHostEnvironment;
            _configuration = configuration;
            _logger = logger;
        }

        // ==================== LOGIN (AJAX) ====================
        [AllowAnonymous]
        public IActionResult Login()
        {
            if (IsUserLoggedIn())
            {
                return RedirectToAction(nameof(Index));
            }
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login(User user)
        {
            if (string.IsNullOrWhiteSpace(user.Username) || string.IsNullOrWhiteSpace(user.UserPassword))
            {
                return Json(new { success = false, message = "Username and Password are required" });
            }

            var getUser = db.Users.FirstOrDefault(c => c.Username == user.Username);

            if (getUser == null)
            {
                return Json(new { success = false, message = "Incorrect Username or Password" });
            }

            if (getUser.LoginTry >= 3)
            {
                if (getUser.UserStatus != Status.Suspended)
                {
                    getUser.UserStatus = Status.Suspended;
                    db.SaveChanges();
                }
                return Json(new { success = false, message = "Your account has been suspended due to multiple failed login attempts. Please contact admin." });
            }

            if (!PasswordSecurity.Verify(getUser.UserPassword ?? string.Empty, user.UserPassword, out var upgradedHash))
            {
                getUser.LoginTry = (getUser.LoginTry ?? 0) + 1;
                db.SaveChanges();

                int remainingAttempts = 3 - (getUser.LoginTry ?? 0);

                if (remainingAttempts <= 0)
                {
                    getUser.UserStatus = Status.Suspended;
                    db.SaveChanges();
                    return Json(new { success = false, message = "Your account has been suspended due to multiple failed login attempts. Please contact admin." });
                }
                else
                {
                    return Json(new { success = false, message = $"Incorrect Password. You have {remainingAttempts} attempt(s) remaining." });
                }
            }

            if (upgradedHash != null)
            {
                getUser.UserPassword = upgradedHash;
            }

            if (getUser.UserStatus == Status.Inactive || getUser.UserStatus == Status.Suspended)
            {
                return Json(new { success = false, message = $"Your account is {getUser.UserStatus}. Please contact admin." });
            }

            if (getUser.UserStatus == Status.Pending)
            {
                return Json(new { success = false, message = "Your account is pending approval. Please wait for admin approval." });
            }

            getUser.LoginTry = 0;

            HttpContext.Session.Clear();
            if (getUser.DateTime.HasValue)
            {
                HttpContext.Session.SetString("LastLogin", getUser.DateTime.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            else
            {
                HttpContext.Session.SetString("LastLogin", "FIRST LOGIN");
            }

            getUser.DateTime = DateTime.Now;
            db.SaveChanges();

            HttpContext.Session.SetString("LoginUser", getUser.UserID.ToString());
            HttpContext.Session.SetString("Username", getUser.Username ?? string.Empty);
            await SignInAsync(getUser);

            AutoAssignPromotions(getUser.UserID);

            return Json(new { success = true, message = "Login successful!" });
        }

        // ==================== INDEX ====================
        public IActionResult Index()
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            var userId = Convert.ToInt32(HttpContext.Session.GetString("LoginUser"));
            var user = db.Users.Find(userId);

            if (user == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction(nameof(Login));
            }

            if (user.UserStatus != Status.Active && user.UserStatus != Status.Approve)
            {
                TempData["Error"] = $"Your account is {user.UserStatus}. Access denied.";
                HttpContext.Session.Clear();
                return RedirectToAction(nameof(Login));
            }

            var products = db.Products
                .Where(p => (p.ProductStatus == Status.Active || p.ProductStatus == Status.Approve) && p.Quantity > 0)
                .OrderByDescending(p => p.ProductID)
                .Take(12)
                .ToList();

            ViewBag.Username = user.Username;
            ViewBag.LastLogin = HttpContext.Session.GetString("LastLogin");

            return View(products);
        }

        // ==================== REGISTER (AJAX) ====================
        [AllowAnonymous]
        [HttpPost]
        public IActionResult Register(string Username, string UserGmail, string UserPhoneNum, string UserPassword, string ConfirmPassword)
        {
            if (UserPassword != ConfirmPassword)
            {
                return Json(new { success = false, message = "Passwords do not match" });
            }

            if (string.IsNullOrWhiteSpace(UserPhoneNum))
            {
                return Json(new { success = false, message = "Phone number is required" });
            }

            if (db.Users.Any(c => c.Username == Username))
            {
                return Json(new { success = false, message = "Username already exists" });
            }

            if (db.Users.Any(c => c.UserGmail == UserGmail))
            {
                return Json(new { success = false, message = "Email already exists" });
            }

            if (db.Users.Any(c => c.UserPhoneNum == UserPhoneNum))
            {
                return Json(new { success = false, message = "Phone number already exists" });
            }

            // create user 并save到 Session（确保password不会不见）
            var user = new User
            {
                Username = Username,
                UserGmail = UserGmail,
                UserPhoneNum = UserPhoneNum,
                UserPassword = PasswordSecurity.Hash(UserPassword),
                UserStatus = Status.Pending,
                DateTime = DateTime.Now,
                LoginTry = 0
            };

            string otp = AccountVerificationCode.Generate();
            string body = $"<h2>Welcome to Our Marketplace!</h2><p>Your OTP for registration is: <strong>{otp}</strong></p>";

            try
            {
                SendByGmail(UserGmail, body);

                // Use more secure serialization options
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNameCaseInsensitive = true
                };

                HttpContext.Session.SetString("RegisterUser", JsonSerializer.Serialize(user, options));
                VerificationCode.Issue(HttpContext.Session, otp, "Register");

                return Json(new { success = true, message = "OTP sent to your email!" });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Failed to send OTP. Please try again." });
            }
        }

        // ==================== OTP ====================
        [AllowAnonymous]
        public IActionResult OTP()
        {
            if (!VerificationCode.IsActive(HttpContext.Session))
            {
                return RedirectToAction(nameof(Login));
            }
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        public IActionResult OTP(string otp)
        {
            var otpType = VerificationCode.Verify(HttpContext.Session, otp);
            if (otpType is null)
            {
                return Json(new { success = false, message = "The code is invalid or expired. Request a new code if needed." });
            }

            if (otpType == "Register")
            {
                var getUserJson = HttpContext.Session.GetString("RegisterUser");
                if (string.IsNullOrEmpty(getUserJson))
                {
                    return Json(new { success = false, message = "Registration session expired" });
                }

                // Use more secure serialization options
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var user = JsonSerializer.Deserialize<User>(getUserJson, options);

                if (user == null || string.IsNullOrEmpty(user.UserPassword))
                {
                    return Json(new { success = false, message = "Invalid registration data" });
                }

                user.RegisterDate = DateTime.Now;
                user.DateTime = DateTime.Now;
                user.LoginTry = 0;
                user.UserStatus = Status.Pending;

                try
                {
                    //save user
                    db.Users.Add(user);
                    db.SaveChanges();// 确保有user id 有generate

                    //check user id 有没有generate
                    if(user.UserID > 0)
                    {
                        // 自动分配 Promotions
                        AutoAssignPromotions(user.UserID);

                        //save 多一次 （确保user promotions 有保存）
                        db.SaveChanges();
                    }
                    else
                    {
                        Console.WriteLine("ERROR: User ID not generated after SaveChanges");
                    }

                    //clear session
                    HttpContext.Session.Remove("RegisterUser");

                    return Json(new { success = true, message = "Registration successful! Please wait for admin approval." });
                }
                catch (Exception ex)
                {
                    // 记录error message
                    Console.WriteLine($"ERROR in OTP Registration: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");

                    //如果create user 了但是promotions分配失败，试多一次分promotions
                    if(user.UserID > 0)
                    {
                        try
                        {
                            AutoAssignPromotions(user.UserID);
                            db.SaveChanges();
                        }
                        catch (Exception innerEx)
                        {
                            Console.WriteLine($"ERROR in AutoAssignPromotions retry: {innerEx.Message}");
                        }
                    }

                    return Json(new { success = false, message = "Registration completed but promotion assignment may have failed. Please contact admin." });
                }           
            }
            else if (otpType == "ForgotPassword")
            {
                HttpContext.Session.SetString("ShopperResetVerifiedAt", DateTime.UtcNow.ToString("O"));
                return Json(new { success = true, redirect = "/User/ChangePassword" });
            }

            return Json(new { success = false, message = "Invalid OTP type" });
        }

        // ==================== FORGOT PASSWORD (AJAX) ====================
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        public IActionResult ForgotPassword(string email)
        {
            HttpContext.Session.Remove("ShopperResetVerifiedAt");
            var getUser = db.Users.FirstOrDefault(c => c.UserGmail == email);
            if (getUser == null)
            {
                return Json(new { success = false, message = "Email not found." });
            }

            string otp = AccountVerificationCode.Generate();
            string body = $"<h2>Password Reset Request</h2><p>Your OTP for password reset is: <strong>{otp}</strong></p>";

            try
            {
                SendByGmail(email, body);
                VerificationCode.Issue(HttpContext.Session, otp, "ForgotPassword");
                HttpContext.Session.SetString("ShopperResetEmail", email);

                return Json(new { success = true, message = "OTP sent to your email!" });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Failed to send OTP. Please try again." });
            }
        }

        // ==================== CHANGE PASSWORD (AJAX) ====================
        [AllowAnonymous]
        public IActionResult ChangePassword()
        {
            if (!IsPasswordResetVerified())
            {
                return RedirectToAction(nameof(ForgotPassword));
            }
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        public IActionResult ChangePassword(string newPassword, string ConfirmNewPassword)
        {
            if (newPassword != ConfirmNewPassword)
            {
                return Json(new { success = false, message = "Passwords do not match" });
            }

            if (!IsPasswordResetVerified())
            {
                return Json(new { success = false, message = "Session expired" });
            }

            var email = HttpContext.Session.GetString("ShopperResetEmail");
            var getUser = db.Users.FirstOrDefault(c => c.UserGmail == email);

            if (getUser == null)
            {
                return Json(new { success = false, message = "User not found" });
            }

            getUser.UserPassword = PasswordSecurity.Hash(newPassword);
            db.SaveChanges();

            HttpContext.Session.Remove("ShopperResetEmail");
            HttpContext.Session.Remove("ShopperResetVerifiedAt");

            return Json(new { success = true, message = "Password changed successfully!" });
        }

        // ==================== LOGOUT ====================
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        // ==================== CART ====================
        public IActionResult Cart()
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            var userId = Convert.ToInt32(HttpContext.Session.GetString("LoginUser"));
            var cartItems = db.Carts
                .AsNoTracking()
                .Include(c => c.Product)
                .Where(c => c.UserID == userId)
                .ToList();

            UseCurrentPrices(cartItems);
            return View(cartItems);
        }

        // ==================== CART AJAX METHODS ====================
        [HttpPost]
        public IActionResult AddToCartAjax(int productId, int quantity = 1)
        {
            if (!IsUserLoggedIn())
            {
                return Json(new { success = false, message = "Please login first" });
            }

            var userId = Convert.ToInt32(HttpContext.Session.GetString("LoginUser"));
            var product = db.Products.Find(productId);

            if (quantity < 1)
            {
                return Json(new { success = false, message = "Quantity must be at least 1" });
            }

            if (!IsAvailableForPurchase(product))
            {
                return Json(new { success = false, message = "Product is unavailable" });
            }

            if (product.Quantity < quantity)
            {
                return Json(new { success = false, message = $"Only {product.Quantity} items available in stock" });
            }

            var existingCart = db.Carts.FirstOrDefault(c => c.UserID == userId && c.ProductID == productId);

            if (existingCart != null)
            {
                var newQuantity = (existingCart.Quantity ?? 0) + quantity;
                if (newQuantity > product.Quantity)
                {
                    return Json(new { success = false, message = $"Cannot add more. Only {product.Quantity} items available" });
                }
                existingCart.Quantity = newQuantity;
            }
            else
            {
                var cart = new Cart
                {
                    UserID = userId,
                    ProductID = productId,
                    Quantity = quantity,
                    Price = product.UnitPrice
                };
                db.Carts.Add(cart);
            }

            db.SaveChanges();
            return Json(new { success = true, message = $"Added {quantity} item(s) to cart successfully!" });
        }

        [HttpPost]
        public IActionResult RemoveFromCartAjax(int cartId)
        {
            if (!IsUserLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            var userId = Convert.ToInt32(HttpContext.Session.GetString("LoginUser"));
            var cart = db.Carts.FirstOrDefault(c => c.CartID == cartId && c.UserID == userId);

            if (cart != null)
            {
                db.Carts.Remove(cart);
                db.SaveChanges();
                return Json(new { success = true, message = "Item removed from cart" });
            }

            return Json(new { success = false, message = "Cart item not found" });
        }

        [HttpGet]
        public IActionResult GetCartItemsAjax()
        {
            if (!IsUserLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            var userId = Convert.ToInt32(HttpContext.Session.GetString("LoginUser"));
            var cartItems = db.Carts
                .Include(c => c.Product)
                .Where(c => c.UserID == userId)
                .Select(c => new
                {
                    c.CartID,
                    c.ProductID,
                    ProductName = c.Product != null ? c.Product.ProductName : "Unavailable product",
                    Price = c.Price,
                    Quantity = c.Quantity,
                    ImagePath = c.Product != null ? c.Product.ImagePath : null,
                    TotalPrice = (c.Price ?? 0) * (c.Quantity ?? 0)
                })
                .ToList();

            return Json(new { success = true, data = cartItems });
        }

        [HttpPost]
        public IActionResult UpdateCartQuantityAjax(int cartId, int quantity)
        {
            if (!IsUserLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var userId = Convert.ToInt32(HttpContext.Session.GetString("LoginUser"));
                var cart = db.Carts
                    .Include(c => c.Product)
                    .FirstOrDefault(c => c.CartID == cartId && c.UserID == userId);

                if (cart == null)
                {
                    return Json(new { success = false, message = "Cart item not found" });
                }

                if (quantity < 1)
                {
                    return Json(new { success = false, message = "Quantity must be at least 1" });
                }

                if (!IsAvailableForPurchase(cart.Product))
                {
                    return Json(new { success = false, message = "Product is unavailable" });
                }

                if (quantity > cart.Product.Quantity)
                {
                    return Json(new { success = false, message = $"Only {cart.Product.Quantity} items available in stock" });
                }

                cart.Quantity = quantity;
                db.SaveChanges();

                return Json(new { success = true, message = "Quantity updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating quantity.");
                return Json(new { success = false, message = "Error updating quantity." });
            }
        }

        // ==================== PRODUCTS ====================
        public IActionResult Products()
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            var products = db.Products
                .Where(p => (p.ProductStatus == Status.Active || p.ProductStatus == Status.Approve) && p.Quantity > 0)
                .OrderByDescending(p => p.ProductID)
                .ToList();

            return View(products);
        }

        // ==================== CHECKOUT ====================
        public IActionResult Checkout()
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            var userId = Convert.ToInt32(HttpContext.Session.GetString("LoginUser"));
            var cartItems = db.Carts
                .AsNoTracking()
                .Include(c => c.Product)
                .Where(c => c.UserID == userId)
                .ToList();

            if (!cartItems.Any())
            {
                TempData["Error"] = "Your cart is empty";
                return RedirectToAction(nameof(Cart));
            }

            if (cartItems.Any(item => !IsAvailableForPurchase(item.Product) || item.Product.SellerID == null))
            {
                TempData["Error"] = "Remove unavailable items from your cart before placing an order.";
                return RedirectToAction(nameof(Cart));
            }

            UseCurrentPrices(cartItems);
            var promotion = FindCheckoutPromotion(userId);
            var discount = CalculateCheckoutDiscount(CheckoutPricing.CalculateSubtotal(cartItems), promotion);
            if (discount == 0m && HttpContext.Session.GetInt32("UserPromotionID").HasValue)
            {
                HttpContext.Session.Remove("UserPromotionID");
                promotion = null;
            }

            var review = new CheckoutReview(Guid.NewGuid().ToString("N"), ReviewLines(cartItems),
                promotion?.UserPromotionID, discount);
            HttpContext.Session.SetString(nameof(CheckoutReview), JsonSerializer.Serialize(review));
            ViewBag.CheckoutToken = review.Token;
            ViewBag.AppliedPromoCode = promotion?.Promotion?.PromotionCode;
            ViewBag.PromotionDiscount = review.Discount;

            return View(cartItems);
        }

        // ==================== PROCESS PAYMENT ====================
        [HttpPost]
        public IActionResult ProcessPayment(string checkoutToken)
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            var storedReview = HttpContext.Session.GetString(nameof(CheckoutReview));
            var review = storedReview is null ? null : JsonSerializer.Deserialize<CheckoutReview>(storedReview);
            if (review is null || !string.Equals(review.Token, checkoutToken, StringComparison.Ordinal))
            {
                TempData["Error"] = "This checkout request has expired. Please reload checkout and try again.";
                return RedirectToAction(nameof(Checkout));
            }

            var userId = Convert.ToInt32(HttpContext.Session.GetString("LoginUser"));
            try
            {
                return CompleteCheckout(userId, review);
            }
            catch (Exception exception) when (IsCheckoutConflict(exception))
            {
                _logger.LogWarning(exception, "Checkout conflicted with another order or inventory update.");
                TempData["Error"] = "Checkout changed while you were ordering. Review your cart and try again.";
                return RedirectToAction(nameof(Cart));
            }
        }

        private IActionResult CompleteCheckout(int userId, CheckoutReview review)
        {
            using var transactionScope = db.Database.BeginTransaction(
                System.Data.IsolationLevel.Serializable);

            var cartItems = db.Carts
                .Include(c => c.Product)
                .Where(c => c.UserID == userId)
                .ToList();

            if (!cartItems.Any())
            {
                TempData["Error"] = "Your cart is empty";
                return RedirectToAction(nameof(Cart));
            }

            var existingAttempt = db.CheckoutAttempts
                .FirstOrDefault(attempt =>
                    attempt.RequestId == review.Token &&
                    attempt.UserID == userId);
            if (existingAttempt != null)
            {
                TempData["Error"] = "This checkout was already submitted.";
                return RedirectToAction(nameof(Checkout));
            }

            var inventory = new List<(Product Product, int Quantity)>();
            foreach (var items in cartItems.GroupBy(item => item.ProductID))
            {
                var product = items.First().Product;
                if (!IsAvailableForPurchase(product) || product.SellerID == null)
                {
                    TempData["Error"] = "A product in your cart is no longer available.";
                    return RedirectToAction(nameof(Cart));
                }

                var requiredQuantity = items.Sum(item => item.Quantity ?? 0);
                if (items.Any(item => item.Quantity is null or <= 0 || item.Quantity % 1 != 0) ||
                    requiredQuantity > product.Quantity)
                {
                    TempData["Error"] = $"Insufficient stock for {product.ProductName}";
                    return RedirectToAction(nameof(Cart));
                }

                inventory.Add((product, (int)requiredQuantity));
            }

            UseCurrentPrices(cartItems);
            var authoritativeSubtotal = CheckoutPricing.CalculateSubtotal(cartItems);
            var userPromotion = FindCheckoutPromotion(userId);
            var authoritativePromotion = CalculateCheckoutDiscount(authoritativeSubtotal, userPromotion);
            if (authoritativePromotion == 0m)
            {
                userPromotion = null;
            }

            if (!review.Lines.SequenceEqual(ReviewLines(cartItems)) ||
                review.PromotionId != userPromotion?.UserPromotionID ||
                review.Discount != authoritativePromotion)
            {
                TempData["Error"] = "Your order changed. Review the updated order before placing it.";
                return RedirectToAction(nameof(Checkout));
            }

            db.CheckoutAttempts.Add(new CheckoutAttempt
            {
                RequestId = review.Token,
                UserID = userId,
                DateTime = DateTime.Now,
                Status = "Completed"
            });
            foreach (var (product, quantity) in inventory)
            {
                product.Quantity -= quantity;
            }

            var authoritativeGst = CheckoutPricing.RoundMoney(CheckoutPricing.CalculateGst(authoritativeSubtotal));
            var remainingSubtotal = authoritativeSubtotal;
            var remainingGst = authoritativeGst;
            var remainingPromotion = authoritativePromotion;
            var sellerGroups = cartItems.GroupBy(item => item.Product!.SellerID!.Value).OrderBy(group => group.Key);

            foreach (var sellerGroup in sellerGroups)
            {
                var sellerSubtotal = CheckoutPricing.CalculateSubtotal(sellerGroup);
                // Allocate from the remaining amounts so the final seller record absorbs any rounding remainder.
                var share = remainingSubtotal == 0m ? 0m : sellerSubtotal / remainingSubtotal;
                var sellerGst = CheckoutPricing.RoundMoney(remainingGst * share);
                var sellerPromotion = CheckoutPricing.RoundMoney(remainingPromotion * share);
                remainingSubtotal -= sellerSubtotal;
                remainingGst -= sellerGst;
                remainingPromotion -= sellerPromotion;

                var transaction = new TransactionHis
                {
                    UserID = userId,
                    SellerID = sellerGroup.Key, //记录 SellerID
                    DateTime = DateTime.Now,
                    Description = "Purchase",
                    SubtotalAmount = sellerSubtotal,
                    GstAmount = sellerGst,
                    Promotion = sellerPromotion,
                    Amount = sellerSubtotal + sellerGst - sellerPromotion,
                };

                db.TransactionHis.Add(transaction);
            }

            if (userPromotion != null)
            {
                userPromotion.IsUsed = true;
                userPromotion.UsedDate = DateTime.Now;
                userPromotion.UsedCount = (userPromotion.UsedCount ?? 0) + 1;
            }

            db.Carts.RemoveRange(cartItems);
            db.SaveChanges();
            transactionScope.Commit();

            var receiptSent = false;
            try
            {
                var user = db.Users.Find(userId);
                if (user != null && !string.IsNullOrEmpty(user.UserGmail))
                {
                    decimal totalAmount = authoritativeSubtotal + authoritativeGst - authoritativePromotion;
                    receiptSent = SendPurchaseConfirmationEmail(user.UserGmail, user.Username ?? string.Empty, totalAmount, cartItems);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to send the demo order confirmation.");
            }

            HttpContext.Session.Remove("UserPromotionID");
            HttpContext.Session.Remove(nameof(CheckoutReview));

            AutoAssignPromotions(userId);

            TempData["Success"] = receiptSent
                ? "Demo order placed. A confirmation email was sent."
                : "Demo order placed. View the details in order history.";
            return RedirectToAction(nameof(Index));
        }

        private sealed record CheckoutReview(string Token, CheckoutLine[] Lines, int? PromotionId, decimal Discount);
        private sealed record CheckoutLine(int CartId, int? ProductId, decimal? Quantity, decimal? Price);

        private static CheckoutLine[] ReviewLines(IEnumerable<Cart> cartItems) =>
            cartItems.OrderBy(item => item.CartID)
                .Select(item => new CheckoutLine(item.CartID, item.ProductID, item.Quantity, item.Price))
                .ToArray();

        private static void UseCurrentPrices(IEnumerable<Cart> cartItems)
        {
            foreach (var item in cartItems)
            {
                item.Price = item.Product?.UnitPrice;
            }
        }

        private UserPromotion? FindCheckoutPromotion(int userId)
        {
            var promotionId = HttpContext.Session.GetInt32("UserPromotionID");
            return promotionId.HasValue
                ? db.UserPromotions.Include(up => up.Promotion)
                    .FirstOrDefault(up => up.UserPromotionID == promotionId.Value && up.UserID == userId)
                : null;
        }

        private static decimal CalculateCheckoutDiscount(decimal subtotal, UserPromotion? promotion) =>
            promotion?.Promotion?.StartDate is not null
                ? CheckoutPricing.RoundMoney(CheckoutPricing.CalculatePromotion(subtotal,
                    promotion.Promotion, promotion.IsUsed, DateTime.Now))
                : 0m;

        private static bool IsCheckoutConflict(Exception exception)
        {
            return exception.GetBaseException() is SqlException { Number: 1205 or 2601 or 2627 };
        }

        // ==================== AUTO ASSIGN PROMOTIONS ====================
        private void AutoAssignPromotions(int userId)
        {
            try
            {
                Console.WriteLine($"=== AutoAssignPromotions START for UserID: {userId} ===");

                if (userId <= 0)
                {
                    return;
                }

                // 检查user是否存在
                var userExists = db.Users.Any(u => u.UserID == userId);
                if (!userExists)
                {
                    Console.WriteLine($"ERROR: User {userId} not found in database");
                    return;
                }
                Console.WriteLine($"User {userId} exists");

                // 检查user现有的 promotions
                var existingPromotions = db.UserPromotions
                    .Where(up => up.UserID == userId)
                    .ToList();

                Console.WriteLine($"User {userId} has {existingPromotions.Count} existing promotions");

                var existingPromoIds = existingPromotions.Select(up => up.PromotionID).ToList();

                // 计算需要分配的数量
                int currentCount = existingPromotions.Count;
                int needed = 3 - currentCount;

                Console.WriteLine($"Promotions needed: {needed}");

                if (needed <= 0)
                {
                    Console.WriteLine($"User {userId} already has {currentCount} promotions. No assignment needed.");
                    return; // 已经有3个或更多
                }

                // 获取所有 Active 的 promotions
                var now = DateTime.Now;
                Console.WriteLine($"Current DateTime: {now}");

                //check 符合条件的promotions
                var allActivePromos = db.Promotions
                    .Where(p => p.PromotionStatus == Status.Active)
                    .ToList();

                Console.WriteLine($"Total Active promotions in DB: {allActivePromos.Count}");

                foreach (var p in allActivePromos)
                {
                    var inDateRange = p.StartDate <= now && (p.EndDate == null || p.EndDate >= now);
                    var notAssigned = !existingPromoIds.Contains(p.PromotionID);
                    Console.WriteLine($"  - Promo {p.PromotionID} ({p.PromotionCode}): " +
                                    $"InDateRange={inDateRange}, NotAssigned={notAssigned}");
                }

                var availablePromotions = db.Promotions
                    .Where(p => p.PromotionStatus == Status.Active &&
                               p.StartDate <= now &&
                               (p.EndDate == null || p.EndDate >= now) &&
                               !existingPromoIds.Contains(p.PromotionID))
                    .OrderBy(x => Guid.NewGuid())
                    .Take(needed)
                    .ToList();

                Console.WriteLine($"✓ Available promotions found: {availablePromotions.Count}");

                if (!availablePromotions.Any())
                {
                    Console.WriteLine($"WARNING: No available promotions to assign!");

                    //check 为什么没有可以用的promotions
                    var allPromos = db.Promotions.ToList();
                    Console.WriteLine($"Total promotions in database: {allPromos.Count}");

                    foreach (var p in allPromos)
                    {
                        var statusCheck = p.PromotionStatus == Status.Active ? "✓" : "✗";
                        var dateCheck = p.StartDate <= now && (p.EndDate == null || p.EndDate >= now) ? "✓" : "✗";
                        var assignCheck = !existingPromoIds.Contains(p.PromotionID) ? "✓" : "✗";

                        Console.WriteLine($"  Promo {p.PromotionID}: " +
                                        $"Status({statusCheck})={p.PromotionStatus}, " +
                                        $"DateRange({dateCheck})={p.StartDate} to {p.EndDate}, " +
                                        $"NotAssigned({assignCheck})");
                    }

                    return;
                }

                //分配 promotions
                int assignedCount = 0;
                foreach (var promo in availablePromotions)
                {
                    Console.WriteLine($"Assigning Promo {promo.PromotionID} (Code: {promo.PromotionCode}) to User {userId}");

                    var userPromotion = new UserPromotion
                    {
                        UserID = userId,
                        PromotionID = promo.PromotionID,
                        AssignedDate = DateTime.Now,
                        IsUsed = false,
                        UsedCount = 0,
                        UsedDate = null
                    };

                    db.UserPromotions.Add(userPromotion);
                    assignedCount++;
                }

                db.SaveChanges();

                Console.WriteLine($"=== AutoAssignPromotions END - Assigned {assignedCount} promotions to User {userId} ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in AutoAssignPromotions for user {userId}: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }

            }
        }

        // ==================== TEST PROMOTIONS ====================
        [NonAction]
        public IActionResult TestPromotions(int userId)
        {
            try
            {
                Console.WriteLine($"========== TESTING PROMOTIONS FOR USER {userId} ==========");

                //check user 是否存在
                var user = db.Users.Find(userId);
                if(user == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "User not found",
                        userId = userId
                    });
                }

                Console.WriteLine($"User found: {user.Username}");

                //check database 里面的全部promotions
                var allPromos = db.Promotions.ToList();
                Console.WriteLine($"Total promotions in database: {allPromos.Count}");

                foreach (var p in allPromos)
                {
                    Console.WriteLine($"  - Promo {p.PromotionID}: Code={p.PromotionCode}, " +
                            $"Status={p.PromotionStatus}, " +
                            $"Start={p.StartDate}, " +
                            $"End={p.EndDate}");
                }

                //check active 的promotions
                var now = DateTime.Now;
                var activePromos = db.Promotions
                    .Where(p => p.PromotionStatus == Status.Active &&
                               p.StartDate <= now &&
                               (p.EndDate == null || p.EndDate >= now))
                    .ToList();

                Console.WriteLine($"Active promotions: {activePromos.Count}");

                //check user 已经有的promotions
                var existingUserPromos = db.UserPromotions
                    .Include(up => up.Promotion)
                    .Where(up => up.UserID == userId)
                    .ToList();

                Console.WriteLine($"User's existing promotions: {existingUserPromos.Count}");

                foreach (var up in existingUserPromos)
                {
                    Console.WriteLine($"  - UserPromo {up.UserPromotionID}: " +
                                    $"PromotionID={up.PromotionID}, " +
                                    $"Code={up.Promotion?.PromotionCode}, " +
                                    $"IsUsed={up.IsUsed}");
                }

                //try 分配promotions
                Console.WriteLine("Attempting to assign promotions...");
                AutoAssignPromotions(userId);
                db.SaveChanges();
                Console.WriteLine("AutoAssignPromotions completed and saved");

                // 再check user的 Promotions
                var updatedUserPromos = db.UserPromotions
                    .Include(up => up.Promotion)
                    .Where(up => up.UserID == userId)
                    .ToList();

                Console.WriteLine($"✓ User's promotions after assignment: {updatedUserPromos.Count}");

                Console.WriteLine($"========== TEST COMPLETED ==========");

                return Json(new
                {
                    success = true,
                    userId = userId,
                    username = user.Username,
                    totalPromotionsInDB = allPromos.Count,
                    activePromotions = activePromos.Count,
                    userPromotionsBefore = existingUserPromos.Count,
                    userPromotionsAfter = updatedUserPromos.Count,
                    promotions = updatedUserPromos.Select(up => new {
                        up.UserPromotionID,
                        up.PromotionID,
                        Code = up.Promotion?.PromotionCode,
                        Name = up.Promotion?.PromotionName,
                        up.IsUsed,
                        up.AssignedDate
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in TestPromotions: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");

                return Json(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        [NonAction]
        public IActionResult CreateTestPromotions()
        {
            try
            {
                // check是否已有text promotions
                var existingTestPromos = db.Promotions
                    .Where(p => p.PromotionCode != null && p.PromotionCode.StartsWith("TEST"))
                    .ToList();

                if (existingTestPromos.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = "Test promotions already exist",
                        count = existingTestPromos.Count
                    });
                }

                // create 3 个test promotions
                var testPromotions = new List<Promotion>
                {
                    new Promotion
                    {
                        PromotionCode = "TEST10",
                        PromotionName = "Test 10% Off",
                        Description = "Test promotion - 10% discount",
                        DiscountPercentage = 10,
                        MinPurchaseAmount = 0,
                        MaxDiscountAmount = 50,
                        StartDate = DateTime.Now.AddDays(-1),
                        EndDate = DateTime.Now.AddMonths(3),
                        UsageLimit = 100,
                        PromotionStatus = Status.Active
                    },
                    new Promotion
                    {
                        PromotionCode = "TEST20",
                        PromotionName = "Test 20% Off",
                        Description = "Test promotion - 20% discount",
                        DiscountPercentage = 20,
                        MinPurchaseAmount = 100,
                        MaxDiscountAmount = 100,
                        StartDate = DateTime.Now.AddDays(-1),
                        EndDate = DateTime.Now.AddMonths(3),
                        UsageLimit = 100,
                        PromotionStatus = Status.Active
                    },
                    new Promotion
                    {
                        PromotionCode = "TEST15",
                        PromotionName = "Test 15% Off",
                        Description = "Test promotion - 15% discount",
                        DiscountPercentage = 15,
                        MinPurchaseAmount = 50,
                        MaxDiscountAmount = 75,
                        StartDate = DateTime.Now.AddDays(-1),
                        EndDate = DateTime.Now.AddMonths(3),
                        UsageLimit = 100,
                        PromotionStatus = Status.Active
                    }
                };
                db.Promotions.AddRange(testPromotions);
                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = "3 test promotions created successfully",
                    promotions = testPromotions.Select(p => new {
                        p.PromotionID,
                        p.PromotionCode,
                        p.PromotionName,
                        p.DiscountPercentage
                    }).ToList()
                });
            }
            catch(Exception ex)
            {
                return Json(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }
        // ==================== PROMOTIONS ====================
        public IActionResult MyPromotions()
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            var userId = Convert.ToInt32(HttpContext.Session.GetString("LoginUser"));

            var userPromotions = db.UserPromotions
                .Include(up => up.Promotion)
                .Where(up => up.UserID == userId)
                .OrderByDescending(up => up.AssignedDate)
                .ToList();

            return View(userPromotions);
        }

        [HttpPost]
        public IActionResult ApplyPromoCode(string promoCode)
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            if (string.IsNullOrWhiteSpace(promoCode))
            {
                TempData["PromoError"] = "Please enter a promo code";
                return RedirectToAction(nameof(Checkout));
            }

            var userId = Convert.ToInt32(HttpContext.Session.GetString("LoginUser"));

            var userPromotion = db.UserPromotions
                .Include(up => up.Promotion)
                .Where(up => up.UserID == userId)
                .AsEnumerable()
                .FirstOrDefault(up =>
                    up.Promotion != null &&
                    up.Promotion.PromotionCode != null &&
                    up.Promotion.PromotionCode.Equals(promoCode, StringComparison.OrdinalIgnoreCase) &&
                    !up.IsUsed &&
                    up.Promotion.PromotionStatus == Status.Active &&
                    up.Promotion.StartDate <= DateTime.Now &&
                    (up.Promotion.EndDate == null || up.Promotion.EndDate >= DateTime.Now)
                );

            if (userPromotion == null)
            {
                TempData["PromoError"] = $"Promo code '{promoCode}' is invalid or expired.";
                return RedirectToAction(nameof(Checkout));
            }

            var promo = userPromotion.Promotion;
            if (promo == null)
            {
                TempData["PromoError"] = "Promotion data is unavailable.";
                return RedirectToAction(nameof(Checkout));
            }

            var cartItems = db.Carts
                .AsNoTracking()
                .Include(c => c.Product)
                .Where(c => c.UserID == userId)
                .ToList();

            if (!cartItems.Any())
            {
                TempData["PromoError"] = "Your cart is empty";
                return RedirectToAction(nameof(Cart));
            }

            UseCurrentPrices(cartItems);
            decimal subtotal = CheckoutPricing.CalculateSubtotal(cartItems);
            var discount = CalculateCheckoutDiscount(subtotal, userPromotion);

            if (discount == 0m &&
                promo.MinPurchaseAmount.HasValue &&
                promo.MinPurchaseAmount.Value > 0 &&
                subtotal < promo.MinPurchaseAmount.Value)
            {
                TempData["PromoError"] = $"Minimum purchase of RM {promo.MinPurchaseAmount.Value:0.00} required.";
                return RedirectToAction(nameof(Checkout));
            }

            HttpContext.Session.SetInt32("UserPromotionID", userPromotion.UserPromotionID);

            TempData["Success"] = $"✓ Promo code '{promo.PromotionCode}' applied! You saved RM {discount:0.00}";
            return RedirectToAction(nameof(Checkout));
        }

        [HttpPost]
        public IActionResult RemovePromoCode()
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            HttpContext.Session.Remove("UserPromotionID");

            TempData["Success"] = "Promo code removed successfully.";
            return RedirectToAction(nameof(Checkout));
        }

        // ==================== TRANSACTION HISTORY ====================
        public IActionResult TransactionHistory()
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            var userId = Convert.ToInt32(HttpContext.Session.GetString("LoginUser"));
            var transactions = db.TransactionHis
                .Where(t => t.UserID == userId)
                .OrderByDescending(t => t.DateTime)
                .ToList();

            return View(transactions);
        }

        // ==================== PROFILE ====================
        public IActionResult Profile()
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            var userId = Convert.ToInt32(HttpContext.Session.GetString("LoginUser"));
            var user = db.Users.Find(userId);

            if (user == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction(nameof(Login));
            }

            return View(user);
        }

        [HttpPost]
        public IActionResult Profile(string Username, string UserGmail, string UserPhoneNum)
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            var userId = Convert.ToInt32(HttpContext.Session.GetString("LoginUser"));
            var userToUpdate = db.Users.Find(userId);

            if (userToUpdate == null)
            {
                return NotFound();
            }

            userToUpdate.Username = Username;
            userToUpdate.UserGmail = UserGmail;
            userToUpdate.UserPhoneNum = UserPhoneNum;

            db.SaveChanges();

            TempData["Success"] = "Profile updated successfully!";
            return RedirectToAction(nameof(Profile));
        }

        // ==================== UPDATE PASSWORD ====================
        public IActionResult UpdatePassword()
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }
            return View();
        }

        [HttpPost]
        public IActionResult UpdatePassword(string CurrentPassword, string NewPassword, string ConfirmPassword)
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            if (NewPassword != ConfirmPassword)
            {
                ViewBag.Error = "New passwords do not match";
                return View();
            }

            var userId = Convert.ToInt32(HttpContext.Session.GetString("LoginUser"));
            var user = db.Users.Find(userId);

            if (user == null)
            {
                return NotFound();
            }

            if (!PasswordSecurity.Verify(user.UserPassword ?? string.Empty, CurrentPassword, out _))
            {
                ViewBag.Error = "Current password is incorrect";
                return View();
            }

            user.UserPassword = PasswordSecurity.Hash(NewPassword);
            db.SaveChanges();

            TempData["Success"] = "Password updated successfully!";
            return RedirectToAction(nameof(Profile));
        }


        // ==================== SEND PURCHASE CONFIRMATION EMAIL ====================
        private bool SendPurchaseConfirmationEmail(string userEmail, string username, decimal amount, List<Cart> items)
        {
            try
            {
                var itemsHtml = string.Join("", items.Select(item =>
                    $@"<tr>
                        <td style='padding: 10px; border-bottom: 1px solid #ddd;'>{System.Net.WebUtility.HtmlEncode(item.Product?.ProductName)}</td>
                        <td style='padding: 10px; border-bottom: 1px solid #ddd; text-align: center;'>{item.Quantity}</td>
                        <td style='padding: 10px; border-bottom: 1px solid #ddd; text-align: right;'>RM {(item.Price ?? 0):0.00}</td>
                        <td style='padding: 10px; border-bottom: 1px solid #ddd; text-align: right;'>RM {((item.Price ?? 0) * (item.Quantity ?? 0)):0.00}</td>
                       </tr>"
                    ));

                string body = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <style>
                            body {{ font-family: 'Arial', sans-serif; background: #f5f1eb; padding: 20px; }}
                            .container {{ max-width: 600px; margin: 0 auto; background: white; padding: 30px; border: 1px solid #7B8A6C; }}
                            .header {{ font-family: 'Courier New', monospace; font-size: 24px; color: #4A4A4A; letter-spacing: 3px; margin-bottom: 20px; text-align: center; }}
                            .subtitle {{ font-family: 'Courier New', monospace; font-size: 14px; color: #7B8A6C; letter-spacing: 2px; text-align: center; margin-bottom: 30px; }}
                            table {{ width: 100%; border-collapse: collapse; margin: 20px 0; }}
                            th {{ background: #7B8A6C; color: white; padding: 12px; text-align: left; }}
                            .total {{ font-size: 20px; font-weight: bold; color: #B67F65; text-align: right; margin-top: 20px; }}
                            .footer {{ margin-top: 30px; padding-top: 20px; border-top: 1px solid #ddd; text-align: center; color: #6A6A6A; font-size: 12px; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>RETRO.TECH</div>
                            <div class='subtitle'>[ PURCHASE CONFIRMATION ]</div>
                    
                            <p>Dear <strong>{System.Net.WebUtility.HtmlEncode(username)}</strong>,</p>
                            <p>Thank you for your purchase! Your order has been confirmed.</p>
                    
                            <table>
                                <thead>
                                    <tr>
                                        <th>Product</th>
                                        <th style='text-align: center;'>Quantity</th>
                                        <th style='text-align: right;'>Price</th>
                                        <th style='text-align: right;'>Subtotal</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {itemsHtml}
                                </tbody>
                            </table>
                    
                            <div class='total'>TOTAL: RM {amount:0.00}</div>
                    
                            <p style='margin-top: 30px;'>Your items will be processed and shipped shortly. You can track your order status in your account.</p>
                    
                            <div class='footer'>
                                <p>Thank you for shopping with RETRO.TECH</p>
                                <p>&copy; 2026 RETRO.TECH. All rights reserved.</p>
                            </div>
                        </div>
                    </body>
                    </html>
                ";

                var message = new MimeMessage();
                message.From.Add(MailboxAddress.Parse(GetRequiredConfiguration("Smtp:From")));
                message.To.Add(MailboxAddress.Parse(userEmail));
                message.Subject = "RETRO.TECH - Purchase Confirmation";
                message.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = body };

                using var smtp = new SmtpClient();
                smtp.Connect(
                    GetRequiredConfiguration("Smtp:Host"),
                    GetRequiredPort("Smtp:Port"));
                AuthenticateIfConfigured(smtp);
                smtp.Send(new SmtpSendRequest(message), CancellationToken.None);
                smtp.Disconnect(true);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to send the demo order confirmation.");
                return false;
            }
        }

        // ==================== HELPER METHODS ====================
        private static bool IsAvailableForPurchase([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] Product? product)
        {
            return product is { ProductStatus: Status.Active or Status.Approve, UnitPrice: >= 0, Quantity: > 0 };
        }

        private bool IsUserLoggedIn()
        {
            var userId = HttpContext.Session.GetString("LoginUser");
            return User.IsInRole("Shopper") &&
                   userId == User.FindFirstValue(ClaimTypes.NameIdentifier) &&
                   int.TryParse(userId, out var id) &&
                   db.Users.Any(u => u.UserID == id &&
                       (u.UserStatus == Status.Active || u.UserStatus == Status.Approve));
        }

        private async Task SignInAsync(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim(ClaimTypes.Name, user.Username ?? string.Empty),
                new Claim(ClaimTypes.Role, "Shopper")
            };
            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    AllowRefresh = true
                });
        }

        private bool IsPasswordResetVerified()
        {
            var email = HttpContext.Session.GetString("ShopperResetEmail");
            var verifiedAt = HttpContext.Session.GetString("ShopperResetVerifiedAt");

            return !string.IsNullOrEmpty(email) &&
                   DateTime.TryParse(
                       verifiedAt,
                       null,
                       System.Globalization.DateTimeStyles.RoundtripKind,
                       out var timestamp) &&
                   DateTime.UtcNow - timestamp <= TimeSpan.FromMinutes(10);
        }

        private void SendByGmail(string receiver, string body)
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(GetRequiredConfiguration("Smtp:From")));
            message.To.Add(MailboxAddress.Parse(receiver));
            message.Subject = "User Portal - OTP Verification";
            message.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = body };

            using var smtp = new SmtpClient();
            smtp.Connect(
                GetRequiredConfiguration("Smtp:Host"),
                GetRequiredPort("Smtp:Port"));
            AuthenticateIfConfigured(smtp);
            smtp.Send(new SmtpSendRequest(message), CancellationToken.None);
            smtp.Disconnect(true);
        }

        private string GetRequiredConfiguration(string key)
        {
            var value = _configuration[key];
            return !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new InvalidOperationException($"Missing required configuration: {key}");
        }

        private int GetRequiredPort(string key)
        {
            return int.TryParse(GetRequiredConfiguration(key), out var port)
                ? port
                : throw new InvalidOperationException($"Invalid SMTP port configuration: {key}");
        }

        private void AuthenticateIfConfigured(SmtpClient smtp)
        {
            var username = _configuration["Smtp:Username"];
            var password = _configuration["Smtp:Password"];

            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            {
                smtp.Authenticate(username, password);
            }
        }

    }
}