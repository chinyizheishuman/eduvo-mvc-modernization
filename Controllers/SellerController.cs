using JQuery_ECommerceAss.Common.Enum;
using JQuery_ECommerceAss.Common.Security;
using JQuery_ECommerceAss.Data;
using JQuery_ECommerceAss.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using MailKit.Net.Smtp;
using System.Text.Json;

namespace JQuery_ECommerceAss.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class SellerController : Controller
    {
        private readonly AppDbContext db;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IConfiguration _configuration;

        public SellerController(
            AppDbContext _db,
            IWebHostEnvironment webHostEnvironment,
            IConfiguration configuration)
        {
            db = _db;
            _webHostEnvironment = webHostEnvironment;
            _configuration = configuration;
        }

        // ==================== LOGIN (AJAX) ====================
        public IActionResult Login()
        {
            if (IsSellerLoggedIn())
            {
                return RedirectToAction(nameof(Dashboard));
            }
            return View();
        }

        [HttpPost]
        public IActionResult Login(Seller seller)
        {
            if (string.IsNullOrWhiteSpace(seller.SellerUsername) || string.IsNullOrWhiteSpace(seller.SellerPassword))
            {
                return Json(new { success = false, message = "Username and Password are required" });
            }

            var getSeller = db.Sellers.FirstOrDefault(s => s.SellerUsername == seller.SellerUsername);

            if (getSeller == null)
            {
                return Json(new { success = false, message = "Incorrect Username or Password" });
            }

            if (getSeller.LoginTry >= 3)
            {
                if (getSeller.SellerStatus != Status.Suspended)
                {
                    getSeller.SellerStatus = Status.Suspended;
                    db.SaveChanges();
                }
                return Json(new { success = false, message = "Your account has been suspended due to multiple failed login attempts. Please contact admin." });
            }

            if (!PasswordSecurity.Verify(getSeller.SellerPassword ?? string.Empty, seller.SellerPassword, out var upgradedHash))
            {
                getSeller.LoginTry = (getSeller.LoginTry ?? 0) + 1;
                db.SaveChanges();

                int remainingAttempts = 3 - (getSeller.LoginTry ?? 0);

                if (remainingAttempts <= 0)
                {
                    getSeller.SellerStatus = Status.Suspended;
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
                getSeller.SellerPassword = upgradedHash;
            }

            if (getSeller.SellerStatus == Status.Inactive || getSeller.SellerStatus == Status.Suspended)
            {
                return Json(new { success = false, message = $"Your account is {getSeller.SellerStatus}. Please contact admin." });
            }

            if (getSeller.SellerStatus == Status.Pending)
            {
                return Json(new { success = false, message = "Your account is pending approval. Please wait for admin approval." });
            }

            getSeller.LoginTry = 0;

            if (getSeller.DateTime.HasValue)
            {
                HttpContext.Session.SetString("LastLogin", getSeller.DateTime.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            else
            {
                HttpContext.Session.SetString("LastLogin", "FIRST LOGIN");
            }

            getSeller.DateTime = DateTime.Now;
            db.SaveChanges();

            HttpContext.Session.SetString("LoginSeller", getSeller.SellerID.ToString());
            HttpContext.Session.SetString("SellerUsername", getSeller.SellerUsername ?? string.Empty);

            return Json(new { success = true, message = "Login successful!" });
        }

        // ==================== REGISTER (AJAX) ====================
        [HttpPost]
        public IActionResult Register(string SellerUsername, string SellerEmail, string SellerPassword, string ConfirmPassword)
        {
            if (SellerPassword != ConfirmPassword)
            {
                return Json(new { success = false, message = "Passwords do not match" });
            }

            if (db.Sellers.Any(s => s.SellerUsername == SellerUsername))
            {
                return Json(new { success = false, message = "Username already exists" });
            }

            if (db.Sellers.Any(s => s.SellerEmail == SellerEmail))
            {
                return Json(new { success = false, message = "Email already exists" });
            }

            var seller = new Seller
            {
                SellerUsername = SellerUsername,
                SellerEmail = SellerEmail,
                SellerPassword = PasswordSecurity.Hash(SellerPassword),
                SellerStatus = Status.Pending,
                DateTime = DateTime.Now,
                LoginTry = 0
            };

            string otp = GenerateOTP();
            string body = $"<h2>Welcome to Our Marketplace!</h2><p>Your OTP for registration is: <strong>{otp}</strong></p>";

            try
            {
                SendByGmail(SellerEmail, body);

                // Use a more secure serialization option
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNameCaseInsensitive = true
                };

                HttpContext.Session.SetString("RegisterSeller", JsonSerializer.Serialize(seller, options));
                HttpContext.Session.SetString("OTP", otp);
                HttpContext.Session.SetString("OTPType", "Register");

                return Json(new { success = true, message = "OTP sent to your email!" });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Failed to send OTP. Please try again." });
            }
        }

        // ==================== OTP ====================
        public IActionResult OTP()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("OTP")))
            {
                return RedirectToAction(nameof(Login));
            }
            return View();
        }

        [HttpPost]
        public IActionResult OTP(string otp)
        {
            var getOtp = HttpContext.Session.GetString("OTP");
            var otpType = HttpContext.Session.GetString("OTPType");

            if (string.IsNullOrEmpty(getOtp))
            {
                return Json(new { success = false, message = "OTP session expired" });
            }

            if (otp != getOtp)
            {
                return Json(new { success = false, message = "OTP Incorrect" });
            }

            if (otpType == "Register")
            {
                var getSellerJson = HttpContext.Session.GetString("RegisterSeller");
                if (string.IsNullOrEmpty(getSellerJson))
                {
                    return Json(new { success = false, message = "Registration session expired" });
                }

                // Use the correct deserialization options
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var seller = JsonSerializer.Deserialize<Seller>(getSellerJson, options);

                if (seller == null || string.IsNullOrEmpty(seller.SellerPassword))
                {
                    return Json(new { success = false, message = "Invalid registration data" });
                }

                seller.DateTime = DateTime.Now;
                seller.LoginTry = 0;
                seller.SellerStatus = Status.Pending;

                db.Sellers.Add(seller);
                db.SaveChanges();

                HttpContext.Session.Remove("RegisterSeller");
                HttpContext.Session.Remove("OTP");
                HttpContext.Session.Remove("OTPType");

                return Json(new { success = true, message = "Registration successful! Please wait for admin approval." });
            }
            else if (otpType == "ForgotPassword")
            {
                HttpContext.Session.Remove("OTP");
                HttpContext.Session.Remove("OTPType");
                HttpContext.Session.SetString("PasswordResetVerifiedAt", DateTime.UtcNow.ToString("O"));
                return Json(new { success = true, redirect = "/Seller/ChangePassword" });
            }

            return Json(new { success = false, message = "Invalid OTP type" });
        }

        // ==================== FORGOT PASSWORD (AJAX) ====================
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ForgotPassword(string email)
        {
            HttpContext.Session.Remove("PasswordResetVerifiedAt");
            var getSeller = db.Sellers.FirstOrDefault(s => s.SellerEmail == email);
            if (getSeller == null)
            {
                return Json(new { success = false, message = "Email not found." });
            }

            string otp = GenerateOTP();
            string body = $"<h2>Password Reset Request</h2><p>Your OTP for password reset is: <strong>{otp}</strong></p>";

            try
            {
                SendByGmail(email, body);
                HttpContext.Session.SetString("OTP", otp);
                HttpContext.Session.SetString("Email", email);
                HttpContext.Session.SetString("OTPType", "ForgotPassword");

                return Json(new { success = true, message = "OTP sent to your email!" });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Failed to send OTP. Please try again." });
            }
        }

        // ==================== CHANGE PASSWORD (AJAX) ====================
        public IActionResult ChangePassword()
        {
            if (!IsPasswordResetVerified())
            {
                return RedirectToAction(nameof(ForgotPassword));
            }
            return View();
        }

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

            var email = HttpContext.Session.GetString("Email");
            var getSeller = db.Sellers.FirstOrDefault(s => s.SellerEmail == email);

            if (getSeller == null)
            {
                return Json(new { success = false, message = "Seller not found" });
            }

            getSeller.SellerPassword = PasswordSecurity.Hash(newPassword);
            db.SaveChanges();

            HttpContext.Session.Remove("Email");
            HttpContext.Session.Remove("PasswordResetVerifiedAt");

            return Json(new { success = true, message = "Password changed successfully!" });
        }

        // ==================== LOGOUT ====================
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }

        // ==================== DASHBOARD ====================
        public IActionResult Dashboard()
        {
            if (!IsSellerLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            var sellerId = Convert.ToInt32(HttpContext.Session.GetString("LoginSeller"));
            var seller = db.Sellers.Find(sellerId);

            if (seller == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction(nameof(Login));
            }

            if (seller.SellerStatus != Status.Active && seller.SellerStatus != Status.Approve)
            {
                TempData["Error"] = $"Your account is {seller.SellerStatus}. Access denied.";
                HttpContext.Session.Clear();
                return RedirectToAction(nameof(Login));
            }

            var products = db.Products.Where(p => p.SellerID == sellerId).ToList();

            return View(products);
        }

        // ==================== ORIGINAL VIEW ACTIONS ====================
        public IActionResult ManageProducts()
        {
            if (!IsSellerLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            var sellerId = Convert.ToInt32(HttpContext.Session.GetString("LoginSeller"));
            var products = db.Products.Where(p => p.SellerID == sellerId).ToList();

            return View(products);
        }

        public IActionResult AddProduct()
        {
            if (!IsSellerLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddProduct(Product product, IFormFile Image)
        {
            if (!IsSellerLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            var sellerId = Convert.ToInt32(HttpContext.Session.GetString("LoginSeller"));
            product.SellerID = sellerId;
            product.ProductStatus = Status.Pending;

            if (Image != null && Image.Length > 0)
            {
                if (!FileUploadSecurity.IsSafeImage(Image))
                {
                    TempData["Error"] = "Only valid JPG, PNG, or WEBP images up to 5 MB are allowed.";
                    return RedirectToAction(nameof(AddProduct));
                }

                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
                Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = FileUploadSecurity.CreateSafeFileName(Image);
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await Image.CopyToAsync(fileStream);
                }

                product.ImagePath = "/images/products/" + uniqueFileName;
            }

            db.Products.Add(product);
            await db.SaveChangesAsync();

            TempData["Success"] = "Product added successfully! Waiting for admin approval.";
            return RedirectToAction(nameof(Dashboard));
        }

        public IActionResult EditProduct(int? id)
        {
            if (!IsSellerLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            if (id == null)
            {
                return NotFound();
            }

            var sellerId = Convert.ToInt32(HttpContext.Session.GetString("LoginSeller"));
            var product = db.Products.FirstOrDefault(p => p.ProductID == id && p.SellerID == sellerId);

            if (product == null)
            {
                TempData["Error"] = "Product not found or access denied.";
                return RedirectToAction(nameof(ManageProducts));
            }

            return View(product);
        }

        [HttpPost]
        public async Task<IActionResult> EditProduct(int id, string ProductName, string Description, decimal UnitPrice, int Quantity, IFormFile Image)
        {
            if (!IsSellerLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            var sellerId = Convert.ToInt32(HttpContext.Session.GetString("LoginSeller"));
            var existingProduct = db.Products.FirstOrDefault(p => p.ProductID == id && p.SellerID == sellerId);

            if (existingProduct == null)
            {
                TempData["Error"] = "Product not found or access denied.";
                return RedirectToAction(nameof(ManageProducts));
            }

            existingProduct.ProductName = ProductName;
            existingProduct.Description = Description;
            existingProduct.UnitPrice = UnitPrice;
            existingProduct.Quantity = Quantity;
            existingProduct.ProductStatus = Status.Pending;

            if (Image != null && Image.Length > 0)
            {
                if (!FileUploadSecurity.IsSafeImage(Image))
                {
                    TempData["Error"] = "Only valid JPG, PNG, or WEBP images up to 5 MB are allowed.";
                    return RedirectToAction(nameof(ManageProducts));
                }

                if (!string.IsNullOrEmpty(existingProduct.ImagePath))
                {
                    string oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, existingProduct.ImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldImagePath))
                    {
                        System.IO.File.Delete(oldImagePath);
                    }
                }

                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
                Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = FileUploadSecurity.CreateSafeFileName(Image);
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await Image.CopyToAsync(fileStream);
                }

                existingProduct.ImagePath = "/images/products/" + uniqueFileName;
            }

            await db.SaveChangesAsync();

            TempData["Success"] = "Product updated successfully! Waiting for admin approval.";
            return RedirectToAction(nameof(Dashboard));
        }

        public IActionResult DeleteProduct(int? id)
        {
            if (!IsSellerLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            if (id == null)
            {
                return NotFound();
            }

            var sellerId = Convert.ToInt32(HttpContext.Session.GetString("LoginSeller"));
            var product = db.Products.FirstOrDefault(p => p.ProductID == id && p.SellerID == sellerId);

            if (product == null)
            {
                TempData["Error"] = "Product not found or access denied.";
                return RedirectToAction(nameof(ManageProducts));
            }

            return View(product);
        }

        [HttpPost, ActionName("DeleteProduct")]
        public IActionResult DeleteProductConfirmed(int id)
        {
            if (!IsSellerLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            var sellerId = Convert.ToInt32(HttpContext.Session.GetString("LoginSeller"));
            var product = db.Products.FirstOrDefault(p => p.ProductID == id && p.SellerID == sellerId);

            if (product != null)
            {
                if (!string.IsNullOrEmpty(product.ImagePath))
                {
                    string imagePath = Path.Combine(_webHostEnvironment.WebRootPath, product.ImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
                }

                db.Products.Remove(product);
                db.SaveChanges();
                TempData["Success"] = "Product deleted successfully!";
            }
            else
            {
                TempData["Error"] = "Product not found or access denied.";
            }

            return RedirectToAction(nameof(Dashboard));
        }

        // ==================== CUSTOMER INSIGHTS (VIEW) ====================
        public IActionResult CustomerInsights()
        {
            if (!IsSellerLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            var sellerId = Convert.ToInt32(HttpContext.Session.GetString("LoginSeller"));
            var seller = db.Sellers.Find(sellerId);

            if (seller == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction(nameof(Login));
            }

            if (seller.SellerStatus != Status.Active && seller.SellerStatus != Status.Approve)
            {
                TempData["Error"] = $"Your account is {seller.SellerStatus}. Access denied.";
                HttpContext.Session.Clear();
                return RedirectToAction(nameof(Login));
            }

            return View();
        }

        // ==================== GET CUSTOMER INSIGHTS DATA (AJAX) ====================
        [HttpGet]
        public IActionResult GetCustomerInsightsAjax()
        {
            if (!IsSellerLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var sellerId = Convert.ToInt32(HttpContext.Session.GetString("LoginSeller"));

                // check 当下seller transactions history
                // 如果seller A 的products 给了seller B， 旧的transactions history 还是在原本的seller 
                var sellerTransactions = db.TransactionHis
                    .Include(t => t.User)
                    .Where(t => t.SellerID == sellerId && t.UserID != null)
                    .ToList();

                if (!sellerTransactions.Any())
                {
                    return Json(new
                    {
                        success = true,
                        data = new
                        {
                            stats = new { totalCustomers = 0, totalPurchases = 0, totalRevenue = 0, avgOrderValue = 0 },
                            customers = new List<object>(),
                            purchaseTrends = new List<object>(),
                            topProducts = new List<object>()
                        }
                    });
                }

                //统计那个Seller的user名单
                var customerData = sellerTransactions
                    .GroupBy(t => t.UserID)
                    .Select(g => new
                    {
                        userId = g.Key,
                        username = g.First().User?.Username ?? "Unknown",
                        email = g.First().User?.UserGmail ?? "N/A",
                        phone = g.First().User?.UserPhoneNum ?? "N/A",
                        purchaseCount = g.Count(),
                        totalSpent = g.Sum(t => t.Amount ?? 0),
                        lastPurchase = g.Max(t => t.DateTime)
                    })
                    .OrderByDescending(c => c.totalSpent)
                    .ToList();

                //算data
                var totalCustomers = customerData.Count;
                var totalPurchases = sellerTransactions.Count;
                var totalRevenue = sellerTransactions.Sum(t => t.Amount ?? 0);
                var avgOrderValue = totalPurchases > 0 ? totalRevenue / totalPurchases : 0;

                // 统计selling trends（跟日期分组）
                var purchaseTrends = sellerTransactions
                    .Where(t => t.DateTime.HasValue)
                    .GroupBy(t => new
                    {
                        Year = t.DateTime!.Value.Year,
                        Month = t.DateTime!.Value.Month
                    })
                    .Select(g => new
                    {
                        month = $"{g.Key.Year}-{g.Key.Month:D2}",
                        amount = g.Sum(t => t.Amount ?? 0)
                    })
                    .OrderBy(x => x.month)
                    .ToList();

                // Top Products - 获取这个 seller 最好卖的products
                var topProducts = db.Products
                    .Where(p => p.SellerID == sellerId)
                    .OrderByDescending(p => p.ProductID) // 可以根据实际销量排
                    .Take(5)
                    .Select(p => new
                    {
                        name = p.ProductName ?? "Unknown Product",
                        sales = p.Quantity ?? 0 // 这里可以改成实际销量
                    })
                    .ToList();

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        stats = new
                        {
                            totalCustomers,
                            totalPurchases,
                            totalRevenue,
                            avgOrderValue
                        },
                        customers = customerData,
                        purchaseTrends = purchaseTrends,
                        topProducts = topProducts
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading customer insights: " + ex.Message });
            }
        }
        // ==================== MANAGE PRODUCTS (AJAX) ====================
        [HttpGet]
        public IActionResult GetProductsAjax()
        {
            if (!IsSellerLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            var sellerId = Convert.ToInt32(HttpContext.Session.GetString("LoginSeller"));
            var products = db.Products
                .Where(p => p.SellerID == sellerId)
                .Select(p => new
                {
                    p.ProductID,
                    p.ProductName,
                    p.Description,
                    p.UnitPrice,
                    p.Quantity,
                    p.ProductStatus,
                    p.ImagePath
                })
                .ToList();

            return Json(new { success = true, data = products });
        }

        // ==================== ADD PRODUCT (AJAX) ====================
        [HttpPost]
        public async Task<IActionResult> AddProductAjax([FromForm] Product product, [FromForm] IFormFile Image)
        {
            if (!IsSellerLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var sellerId = Convert.ToInt32(HttpContext.Session.GetString("LoginSeller"));
                product.SellerID = sellerId;
                product.ProductStatus = Status.Pending;

                if (Image != null && Image.Length > 0)
                {
                    if (!FileUploadSecurity.IsSafeImage(Image))
                    {
                        return Json(new { success = false, message = "Only valid JPG, PNG, or WEBP images up to 5 MB are allowed." });
                    }

                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
                    Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = FileUploadSecurity.CreateSafeFileName(Image);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await Image.CopyToAsync(fileStream);
                    }

                    product.ImagePath = "/images/products/" + uniqueFileName;
                }

                db.Products.Add(product);
                await db.SaveChangesAsync();

                return Json(new { success = true, message = "Product added successfully! Waiting for admin approval.", productId = product.ProductID });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error adding product: " + ex.Message });
            }
        }

        // ==================== EDIT PRODUCT (AJAX) ====================
        [HttpPost]
        public async Task<IActionResult> EditProductAjax([FromForm] int ProductID, [FromForm] string ProductName,
            [FromForm] string Description, [FromForm] decimal UnitPrice, [FromForm] int Quantity, [FromForm] IFormFile Image)
        {
            if (!IsSellerLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var sellerId = Convert.ToInt32(HttpContext.Session.GetString("LoginSeller"));
                var existingProduct = db.Products.FirstOrDefault(p => p.ProductID == ProductID && p.SellerID == sellerId);

                if (existingProduct == null)
                {
                    return Json(new { success = false, message = "Product not found" });
                }

                existingProduct.ProductName = ProductName;
                existingProduct.Description = Description;
                existingProduct.UnitPrice = UnitPrice;
                existingProduct.Quantity = Quantity;
                existingProduct.ProductStatus = Status.Pending;

                if (Image != null && Image.Length > 0)
                {
                    if (!FileUploadSecurity.IsSafeImage(Image))
                    {
                        return Json(new { success = false, message = "Only valid JPG, PNG, or WEBP images up to 5 MB are allowed." });
                    }

                    if (!string.IsNullOrEmpty(existingProduct.ImagePath))
                    {
                        string oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, existingProduct.ImagePath.TrimStart('/'));
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }

                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
                    Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = FileUploadSecurity.CreateSafeFileName(Image);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await Image.CopyToAsync(fileStream);
                    }

                    existingProduct.ImagePath = "/images/products/" + uniqueFileName;
                }

                await db.SaveChangesAsync();

                return Json(new { success = true, message = "Product updated successfully! Waiting for admin approval." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating product: " + ex.Message });
            }
        }

        // ==================== DELETE PRODUCT (AJAX) ====================
        [HttpPost]
        public IActionResult DeleteProductAjax(int productId)
        {
            if (!IsSellerLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var sellerId = Convert.ToInt32(HttpContext.Session.GetString("LoginSeller"));
                var product = db.Products.FirstOrDefault(p => p.ProductID == productId && p.SellerID == sellerId);

                if (product == null)
                {
                    return Json(new { success = false, message = "Product not found or access denied" });
                }

                if (!string.IsNullOrEmpty(product.ImagePath))
                {
                    string imagePath = Path.Combine(_webHostEnvironment.WebRootPath, product.ImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
                }

                db.Products.Remove(product);
                db.SaveChanges();

                return Json(new { success = true, message = "Product deleted successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting product: " + ex.Message });
            }
        }

        // ==================== GET DASHBOARD STATS (AJAX) ====================
        [HttpGet]
        public IActionResult GetDashboardStatsAjax()
        {
            if (!IsSellerLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var sellerId = Convert.ToInt32(HttpContext.Session.GetString("LoginSeller"));

                var totalProducts = db.Products.Count(p => p.SellerID == sellerId);
                var activeProducts = db.Products.Count(p => p.SellerID == sellerId && p.ProductStatus == Status.Active);
                var pendingProducts = db.Products.Count(p => p.SellerID == sellerId && p.ProductStatus == Status.Pending);
                var totalStock = db.Products.Where(p => p.SellerID == sellerId).Sum(p => p.Quantity ?? 0);

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        totalProducts,
                        activeProducts,
                        pendingProducts,
                        totalStock
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading stats: " + ex.Message });
            }
        }

        // ==================== SEND EMAIL AFTER PURCHASE ====================
        // send purchase confirmation emails
        private void SendPurchaseConfirmationEmail(string userEmail, string username, decimal amount, List<Cart> items)
        {
            try
            {
                var itemsHtml = string.Join("", items.Select(item =>
                    $"<li>{item.Product?.ProductName} - Quantity: {item.Quantity} - RM {((item.Price ?? 0) * (item.Quantity ?? 0)):0.00}</li>"
                ));

                string body = $@"
                    <h2>Purchase Confirmation</h2>
                    <p>Dear {username},</p>
                    <p>Thank you for your purchase! Here are the details:</p>
                    <ul>
                        {itemsHtml}
                    </ul>
                    <p><strong>Total Amount: RM {amount:0.00}</strong></p>
                    <p>Your order will be processed shortly.</p>
                    <p>Best regards,<br/>RETRO.TECH Team</p>
                ";

                SendByGmail(userEmail, body);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the transaction
                Console.WriteLine($"Failed to send confirmation email: {ex.Message}");
            }
        }

        // ==================== HELPER METHODS ====================
        private bool IsSellerLoggedIn()
        {
            var sellerId = HttpContext.Session.GetString("LoginSeller");
            return int.TryParse(sellerId, out var id) &&
                   db.Sellers.Any(s => s.SellerID == id &&
                       (s.SellerStatus == Status.Active || s.SellerStatus == Status.Approve));
        }

        private bool IsPasswordResetVerified()
        {
            var email = HttpContext.Session.GetString("Email");
            var verifiedAt = HttpContext.Session.GetString("PasswordResetVerifiedAt");

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
            message.Subject = "Seller Portal - OTP Verification";
            message.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = body };

            using var smtp = new SmtpClient();
            smtp.Connect(
                GetRequiredConfiguration("Smtp:Host"),
                GetRequiredPort("Smtp:Port"));
            smtp.Authenticate(
                GetRequiredConfiguration("Smtp:Username"),
                GetRequiredConfiguration("Smtp:Password"));
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

        private string GenerateOTP()
        {
            Random random = new Random();
            return random.Next(100000, 999999).ToString();
        }
    }
}