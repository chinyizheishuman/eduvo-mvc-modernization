using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using JQuery_ECommerceAss.Common.Enum;
using JQuery_ECommerceAss.Common.Security;
using JQuery_ECommerceAss.Data;
using JQuery_ECommerceAss.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JQuery_ECommerceAss.Controllers
{
    [AutoValidateAntiforgeryToken]
    [Authorize(Roles = "Admin", Policy = "AdminOnly")]
    public class AdminController : Controller
    {
        private readonly AppDbContext db;
        public AdminController(AppDbContext _db)
        {
            db = _db;
        } 
        [AllowAnonymous]
        public IActionResult Login()
        {
            if (IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Index));
            }
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login(Admin admin)
        {
            if (string.IsNullOrWhiteSpace(admin.AdminUsername) || string.IsNullOrWhiteSpace(admin.AdminPassword))
            {
                ViewBag.Error = "Username and Password are required";
                return View();
            }

            var getAdmin = db.Admins.FirstOrDefault(a => a.AdminUsername == admin.AdminUsername);
            if (getAdmin != null &&
                PasswordSecurity.Verify(getAdmin.AdminPassword, admin.AdminPassword, out var upgradedHash))
            {
                if (upgradedHash != null)
                {
                    getAdmin.AdminPassword = upgradedHash;
                }
            }
            else
            {
                getAdmin = null;
            }

            if (getAdmin == null)
            {
                ViewBag.Error = "Incorrect Username or Password";
                return View();
            }

            if (getAdmin.DateTime.HasValue)
            {
                HttpContext.Session.SetString("LastLogin", getAdmin.DateTime.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            else
            {
                HttpContext.Session.SetString("LastLogin", "FIRST LOGIN");
            }

            getAdmin.DateTime = DateTime.Now;
            db.SaveChanges();

            HttpContext.Session.SetString("LoginAdmin", getAdmin.AdminID.ToString());
            await SignInAsync(getAdmin);

            return RedirectToAction(nameof(Index));
        }

        [AllowAnonymous]
        public async Task<IActionResult> Logout()
        {
            var adminId = HttpContext.Session.GetString("LoginAdmin");
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }
        //===========================================================================================
        public async Task<IActionResult> Index()
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            try
            {
                var products = await db.Products.Include(p => p.Seller).ToListAsync();

                return View(products);
            }
            catch (Exception)
            {
                TempData["Error"] = "Failed to load products";
                return View(new List<Product>());
            }
        }

        [HttpGet]
        public IActionResult Dashboard() // (Add Chart, Search Function) 
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }
            return View();
        }

        //===========================================================================================
        public IActionResult ProductCreateForm() //Product Create Form Add Upload Multiple Photos Function 
        {
            return View();
        }

        //===========================================================================================
        public async Task<IActionResult> ChangeStatus(int? id)
        {
            // check login
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            // check id valid/invalid
            if (id == null)
            {
                return NotFound();
            }

            // get products from database
            var product = await db.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(int id, Status newStatus)
        {
            // check login
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            // get products from database
            var product = await db.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            // update products status
            product.ProductStatus = newStatus;

            try
            {
                db.Update(product);
                await db.SaveChangesAsync();

                TempData["Success"] = $"Product status changed to {newStatus} successfully!";
            }
            catch (Exception)
            {               
                TempData["Error"] = "Failed to change product status.";
            }

            return RedirectToAction(nameof(Index));
        }

        //===========================================================================================
        public async Task<IActionResult> EditProduct(int? id)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            if (id == null)
            {
                return NotFound();
            }

            var product = await db.Products.Include(p => p.Seller).FirstOrDefaultAsync(p => p.ProductID == id);

            if (product == null)
            {
                return NotFound();
            }

            //Get Seller for choice
            ViewBag.Sellers = await db.Sellers.ToListAsync();

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(int id, Product product)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            if (id != product.ProductID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    db.Update(product);
                    await db.SaveChangesAsync();
                    TempData["Success"] = "Product updated successfully!";
                }
                catch (DbUpdateConcurrencyException) //避免很多人同时同一个data, 第一个改，第二个会被通知
                {
                    if (!ProductExists(product.ProductID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (Exception)
                {
                    TempData["Error"] = "Failed to update product.";
                }
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> DeleteProduct(int? id)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            if (id == null)
            {
                return NotFound();
            }

            var product = await db.Products.FirstOrDefaultAsync(p => p.ProductID == id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        [HttpPost, ActionName("DeleteProduct")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProductConfirmed(int id)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            var products = await db.Products.FindAsync(id);
            if (products != null)
            {
                db.Products.Remove(products);
                await db.SaveChangesAsync();
                TempData["Success"] = "Product deleted successfully!";
            }

            return RedirectToAction(nameof(Index));
        }

        //===========================================================================================
        public async Task<IActionResult> ProductDetails(int? id)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            if (id == null)
            {
                return NotFound();
            }

            var product = await db.Products.Include(p => p.Seller).FirstOrDefaultAsync(p => p.ProductID == id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        //======================================================================

        public async Task<IActionResult> ManageUser()
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            var users = await db.Users.ToListAsync();

            return View(users);
        }

        public async Task<IActionResult> ChangeUserStatus(int? id)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            if (id == null)
            {
                return NotFound();
            }

            var user = await db.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeUserStatus(int id, Status newStatus)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            var user = await db.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            user.UserStatus = newStatus;

            try
            {
                db.Update(user);
                await db.SaveChangesAsync();
                TempData["Success"] = $"User status changed to {newStatus} successfully!";
            }
            catch (Exception)
            {
                TempData["Error"] = "Failed to change user status.";
            }

            return RedirectToAction(nameof(ManageUser));
        }

        public async Task<IActionResult> EditUser(int? id)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            if (id == null)
            {
                return NotFound();
            }

            var user = await db.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(int id, User user)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            if (id != user.UserID)
            {
                return NotFound();
            }

            try
            {
                db.Update(user);
                await db.SaveChangesAsync();
                TempData["Success"] = "User updated successfully!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(user.UserID))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            catch (Exception)
            {
                TempData["Error"] = "Failed to update user.";
            }

            return RedirectToAction(nameof(ManageUser));
        }

        public async Task<IActionResult> DeleteUser(int? id)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            if (id == null)
            {
                return NotFound();
            }

            var user = await db.Users.FirstOrDefaultAsync(p => p.UserID == id);

            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        [HttpPost, ActionName("DeleteUser")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUserConfirmed(int id)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            var user = await db.Users.FindAsync(id);
            if (user != null)
            {
                db.Users.Remove(user);
                await db.SaveChangesAsync();
                TempData["Success"] = "User deleted successfully!";
            }
            return RedirectToAction(nameof(ManageUser));
        }

        //===========================================================================================
        public async Task<IActionResult> UserDetails(int? id)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            if (id == null)
            {
                return NotFound();
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.UserID == id);

            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        //======================================================================
        public async Task<IActionResult> ManageSeller()
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            var sellers = await db.Sellers.ToListAsync();

            return View(sellers);
        }

        public async Task<IActionResult> EditSeller(int? id)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            if (id == null)
            {
                return NotFound();
            }

            var seller = await db.Sellers.FindAsync(id);
            if (seller == null)
            {
                return NotFound();
            }

            return View(seller);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSeller(int id, Seller seller)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            if (id != seller.SellerID)
            {
                return NotFound();
            }

            try
            {
                db.Update(seller);
                await db.SaveChangesAsync();
                TempData["Success"] = "Seller updated successfully!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SellerExists(seller.SellerID))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            catch (Exception)
            {
                TempData["Error"] = "Failed to update seller.";
            }

            return RedirectToAction(nameof(ManageSeller));
        }

        public async Task<IActionResult> DeleteSeller(int? id)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            if (id == null)
            {
                return NotFound();
            }

            var seller = await db.Sellers.FirstOrDefaultAsync(s => s.SellerID == id);

            if (seller == null)
            {
                return NotFound();
            }

            return View(seller);
        }

        [HttpPost, ActionName("DeleteSeller")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSellerConfirmed(int id)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            var seller = await db.Sellers.FindAsync(id);
            if (seller != null)
            {
                db.Sellers.Remove(seller);
                await db.SaveChangesAsync();
                TempData["Success"] = "Seller deleted successfully!";
            }

            return RedirectToAction(nameof(ManageSeller));
        }

        public async Task<IActionResult> SellerDetails(int? id)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            if (id == null)
            {
                return NotFound();
            }

            var seller = await db.Sellers.FirstOrDefaultAsync(s => s.SellerID == id);

            if (seller == null)
            {
                return NotFound();
            }

            return View(seller);
        }

        // ============ AJAX API METHODS ============
        [HttpGet]
        public async Task<IActionResult> GetProductsJson()
        {
            if (!IsAdminLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var products = await db.Products
                    .Include(p => p.Seller)
                    .Select(p => new
                    {
                        p.ProductID,
                        p.ProductName,
                        p.Description,
                        p.UnitPrice,
                        p.Quantity,
                        p.ProductStatus,
                        p.ImagePath,
                        SellerName = p.Seller != null ? p.Seller.SellerUsername : "No Seller",
                        p.SellerID
                    })
                    .ToListAsync();

                return Json(new { success = true, data = products });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading products: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetProductDetailsJson(int id)
        {
            if (!IsAdminLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var product = await db.Products
                    .Include(p => p.Seller)
                    .Where(p => p.ProductID == id)
                    .Select(p => new
                    {
                        p.ProductID,
                        p.ProductName,
                        p.Description,
                        p.UnitPrice,
                        p.Quantity,
                        p.ProductStatus,
                        p.ImagePath,
                        p.SellerID,
                        SellerName = p.Seller != null ? p.Seller.SellerUsername : "No Seller"
                    })
                    .FirstOrDefaultAsync();

                if(product == null)
                {
                    return Json(new { success = false, message = "Product not found" });
                }

                return Json(new { success = true, data = product });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading product: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateProductAjax([FromForm] Product product, [FromForm] List<IFormFile> images)
        {
            if (!IsAdminLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                if (images != null && images.Count > 0)
                {
                    var imagesUrls = new List<string>();
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products");
                    Directory.CreateDirectory(uploadsFolder);

                    foreach (var image in images)
                    {
                        if (image.Length > 0 && FileUploadSecurity.IsSafeImage(image))
                        {
                            var uniqueFileName = FileUploadSecurity.CreateSafeFileName(image);
                            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await image.CopyToAsync(fileStream);
                            }

                            imagesUrls.Add("/images/products/" + uniqueFileName);
                        }
                        else if (image.Length > 0)
                        {
                            return Json(new { success = false, message = "Only valid JPG, PNG, or WEBP images up to 5 MB are allowed." });
                        }
                    }

                    product.ImagePath = string.Join(",", imagesUrls);
                }

                db.Products.Add(product);
                await db.SaveChangesAsync();

                return Json(new { success = true, message = "Product created successfully", productId = product.ProductID });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error creating product: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProductAjax([FromForm] Product product, [FromForm] List<IFormFile> images)
        {
            if (!IsAdminLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var existingProduct = await db.Products.FindAsync(product.ProductID);
                if (existingProduct == null)
                {
                    return Json(new { success = false, message = "Product not found" });
                }

                existingProduct.ProductName = product.ProductName;
                existingProduct.Description = product.Description;
                existingProduct.UnitPrice = product.UnitPrice;
                existingProduct.Quantity = product.Quantity;
                existingProduct.ProductStatus = product.ProductStatus;
                existingProduct.SellerID = product.SellerID;

                if(images != null && images.Count > 0)
                {
                    var imageUrls = new List<string>();
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products");

                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    foreach (var image in images)
                    {
                        if (image.Length > 0 && FileUploadSecurity.IsSafeImage(image))
                        {
                            var uniqueFileName = FileUploadSecurity.CreateSafeFileName(image);
                            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await image.CopyToAsync(fileStream);
                            }

                            imageUrls.Add("/images/products/" + uniqueFileName);
                        }
                        else if (image.Length > 0)
                        {
                            return Json(new { success = false, message = "Only valid JPG, PNG, or WEBP images up to 5 MB are allowed." });
                        }
                    }

                    existingProduct.ImagePath = string.Join(",", imageUrls);
                }

                db.Update(existingProduct);
                await db.SaveChangesAsync();

                return Json(new { success = true, message = "Product updated successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating product: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProductAjax(int id)
        {
            if (!IsAdminLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var product = await db.Products.FindAsync(id);
                if(product == null)
                {
                    return Json(new { success = false, message = "Product  not found" });
                }

                // delete 相关image
                if (!string.IsNullOrEmpty(product.ImagePath))
                {
                    var imagePaths = product.ImagePath.Split(',');
                    foreach (var imagePath in imagePaths)
                    {
                        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imagePath.TrimStart('/'));
                        if (System.IO.File.Exists(fullPath))
                        {
                            try
                            {
                                System.IO.File.Delete(fullPath);
                            }
                            catch { /* ignore file delete error */ }
                        }
                    }
                }

                db.Products.Remove(product);
                await db.SaveChangesAsync();

                return Json(new { success = true, message = "Product deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting product: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProductStatusAjax(int id, Status status)
        {
            if (!IsAdminLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var product = await db.Products.FindAsync(id);
                if (product == null)
                {
                    return Json(new { success = false, message = "Product not found" });
                }

                product.ProductStatus = status;
                db.Update(product);
                await db.SaveChangesAsync();

                return Json(new { success = true, message = $"Product status changed to {status}" });
            }
            catch(Exception ex)
            {
                return Json(new { success = false, message = "Error updating status: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchProducts(string searchTerm)
        {
            if (!IsAdminLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var products = await db.Products
                    .Include(p => p.Seller)
                    .Where(p => (p.ProductName != null && p.ProductName.Contains(searchTerm)) || 
                                (p.Description != null && p.Description.Contains(searchTerm)) ||
                                (p.Seller != null && p.Seller.SellerUsername != null && p.Seller.SellerUsername.Contains(searchTerm)))
                    .Select(p => new
                    {
                        p.ProductID,
                        p.ProductName,
                        p.UnitPrice,
                        p.Quantity,
                        p.ProductStatus,
                        p.ImagePath,
                        SellerName = p.Seller != null ? p.Seller.SellerUsername : "No Seller"
                    })
                    .ToListAsync();

                return Json(new { success = true, data = products });
            }
            catch(Exception ex)
            {
                return Json(new { success = false, message = "Error searching products: " + ex.Message });
            }
        }

        // ============================================================
        // AJAX API METHODS - user manage（完整CRUD + search）

        [HttpGet]
        public async Task<IActionResult> GetUsersJson()
        {
            if (!IsAdminLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var users = await db.Users
                    .Select(u => new
                    {
                        u.UserID,
                        u.Username,
                        u.UserGmail,
                        u.UserPhoneNum,
                        u.UserStatus,
                        u.DateTime,
                        u.LoginTry
                    })
                    .ToListAsync();

                return Json(new { success = true, data = users });
            }
            catch(Exception ex)
            {
                return Json(new { success = false, message = "Error loading users: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUserDetailsJson(int id)
        {
            if (!IsAdminLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var user = await db.Users
                    .Where(u => u.UserID == id)
                    .Select(u => new
                    {
                        u.UserID,
                        u.Username,
                        u.UserGmail,
                        u.UserPhoneNum,
                        u.UserStatus,
                        RegistrationDate = u.DateTime,
                        u.LoginTry
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                return Json(new { success = true, data = user });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading user: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateUserAjax([FromForm] User user)
        {
            if (!IsAdminLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var existingUser = await db.Users.FindAsync(user.UserID);
                if (existingUser == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                existingUser.Username = user.Username;
                existingUser.UserGmail = user.UserGmail;
                existingUser.UserPhoneNum = user.UserPhoneNum;

                //如果从suspended 改成Active，reset logintry
                if (existingUser.UserStatus == Status.Suspended && user.UserStatus == Status.Active)
                {
                    existingUser.LoginTry = 0;
                }
                else
                {
                    //允许admin edit LoginTry
                    existingUser.LoginTry = user.LoginTry;
                }

                existingUser.UserStatus = user.UserStatus;

                db.Update(existingUser);
                await db.SaveChangesAsync();

                return Json(new { success = true, message = "User updated successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating user: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUserAjax(int id)
        {
            if (!IsAdminLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var user = await db.Users.FindAsync(id);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                db.Users.Remove(user);
                await db.SaveChangesAsync();

                return Json(new { success = true, message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting user: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateUserStatusAjax(int id, Status status)
        {
            if (!IsAdminLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var user = await db.Users.FindAsync(id);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                // 如果从 Suspended 改成 Active，自动reset LoginTry
                if (user.UserStatus == Status.Suspended && status == Status.Active)
                {
                    user.LoginTry = 0;
                }

                user.UserStatus = status;
                db.Update(user);
                await db.SaveChangesAsync();

                return Json(new { success = true, message = "User status updated successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating status: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchUsers(string searchTerm)
        {
            if (!IsAdminLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var users = await db.Users
                    .Where(u => (u.Username != null && u.Username.Contains(searchTerm)) ||
                                (u.UserGmail != null && u.UserGmail.Contains(searchTerm)) ||
                                 (u.UserPhoneNum != null && u.UserPhoneNum.Contains(searchTerm)))
                    .Select(u => new
                    {
                        u.UserID,
                        u.Username,
                        u.UserGmail,
                        u.UserPhoneNum,
                        u.UserStatus
                    })
                    .ToListAsync();

                return Json(new { success = true, data = users });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error searching users: " + ex.Message });
            }
        }

        // ============================================================
        // AJAX API METHODS - seller manage（完整CRUD + search）
        [HttpGet]
        public async Task<IActionResult> GetSellersJson()
        {
            if (!IsAdminLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var sellers = await db.Sellers
                    .Select(s => new
                    {
                        s.SellerID,
                        s.SellerUsername,
                        s.SellerEmail,
                        s.SellerStatus,
                        s.DateTime,
                        s.LoginTry
                    })
                    .ToListAsync();

                return Json(new { success = true, data = sellers });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading sellers: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSellerDetailsJson(int id)
        {
            if (!IsAdminLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var seller = await db.Sellers
                    .Where(s => s.SellerID == id)
                    .Select(s => new
                    {
                        s.SellerID,
                        s.SellerUsername,
                        s.SellerEmail,
                        s.SellerStatus,
                        RegistrationDate = s.DateTime,
                        s.LoginTry
                    })
                    .FirstOrDefaultAsync();

                if (seller == null)
                {
                    return Json(new { success = false, message = "Seller not found" });
                }

                return Json(new { success = true, data = seller });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading seller: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateSellerAjax([FromForm] Seller seller)
        {
            if (!IsAdminLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var existingSeller = await db.Sellers.FindAsync(seller.SellerID);
                if (existingSeller == null)
                {
                    return Json(new { success = false, message = "Seller not found" });
                }

                existingSeller.SellerUsername = seller.SellerUsername;
                existingSeller.SellerEmail = seller.SellerEmail;

                //如果从 Suspended 改成 Active，自动reset LoginTry
                if (existingSeller.SellerStatus == Status.Suspended && seller.SellerStatus == Status.Active)
                {
                    existingSeller.LoginTry = 0;
                }
                else
                {
                    //允许admin edit LoginTry
                    existingSeller.LoginTry = seller.LoginTry;
                }

                existingSeller.SellerStatus = seller.SellerStatus;

                db.Update(existingSeller);
                await db.SaveChangesAsync();

                return Json(new { success = true, message = "Seller updated successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating seller: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteSellerAjax(int id)
        {
            if (!IsAdminLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var seller = await db.Sellers.FindAsync(id);
                if (seller == null)
                {
                    return Json(new { success = false, message = "Seller not found" });
                }

                db.Sellers.Remove(seller);
                await db.SaveChangesAsync();

                return Json(new { success = true, message = "Seller deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting seller: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchSellers(string searchTerm)
        {
            if (!IsAdminLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var sellers = await db.Sellers
                    .Where(s => (s.SellerUsername != null && s.SellerUsername.Contains(searchTerm)) ||
                               (s.SellerEmail != null && s.SellerEmail.Contains(searchTerm)))
                    .Select(s => new
                    {
                        s.SellerID,
                        s.SellerUsername,
                        s.SellerEmail,
                        s.SellerStatus
                    })
                    .ToListAsync();

                return Json(new { success = true, data = sellers });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error searching sellers: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateSellerStatusAjax(int id, Status status)
        {
            if (!IsAdminLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var seller = await db.Sellers.FindAsync(id);
                if(seller == null)
                {
                    return Json(new { success = false, message = "Seller not found" });
                }

                //如果从 Suspended 改成 Active，自动reset LoginTry
                if (seller.SellerStatus == Status.Suspended && status == Status.Active)
                {
                    seller.LoginTry = 0;
                }

                seller.SellerStatus = status;
                db.Update(seller);
                await db.SaveChangesAsync();

                return Json(new { success = true, message = $"Seller status updated to {status} successfully" });
            }
            catch(Exception ex)
            {
                return Json(new { success = false, message = "Error updating seller status: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateUserAjax([FromForm] User user)
        {
            if (!IsAdminLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                if (string.IsNullOrWhiteSpace(user.Username) || string.IsNullOrWhiteSpace(user.UserGmail))
                {
                    return Json(new { success = false, message = "Username and Email are required" });
                }

                var existingUser = await db.Users
                    .FirstOrDefaultAsync(u => u.Username == user.Username || u.UserGmail == user.UserGmail);

                if (existingUser != null)
                {
                    return Json(new { success = false, message = "Username or Email already exists" });
                }

                user.RegisterDate = DateTime.Now;
                user.DateTime = DateTime.Now;
                user.LoginTry = 0;

                db.Users.Add(user);
                await db.SaveChangesAsync();

                return Json(new { success = true, message = "User created successfully", userId = user.UserID });
            }
            catch(Exception ex)
            {
                return Json(new { success = false, message = "Error creating user: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateSellerAjax([FromForm] Seller seller)
        {
            if (!IsAdminLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                if (string.IsNullOrWhiteSpace(seller.SellerUsername) || string.IsNullOrWhiteSpace(seller.SellerEmail))
                {
                    return Json(new { success = false, message = "Username and Email are required" });
                }

                var existingSeller = await db.Sellers
                    .FirstOrDefaultAsync(s => s.SellerUsername == seller.SellerUsername || s.SellerEmail == seller.SellerEmail);

                if(existingSeller != null)
                {
                    return Json(new { success = false, message = "Username and Email already Exists" });
                }

                seller.DateTime = DateTime.Now;
                seller.LoginTry = 0;

                db.Sellers.Add(seller);
                await db.SaveChangesAsync();

                return Json(new { success = true, message = "Seller created successfully", sellerId = seller.SellerID });
            }
            catch(Exception ex)
            {
                return Json(new { success = false, message = "Error creating seller: " + ex.Message });
            }
        }

        // ============================================================
        // GetSellersJson 加更多细节 (opitonal)
        [HttpGet]
        public async Task<IActionResult> GetSellersJsonEnhanced()
        {
            if (!IsAdminLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var sellers = await db.Sellers
                    .Include(s => s.Products)
                    .Select(s => new
                    {
                        s.SellerID,
                        s.SellerUsername,
                        s.SellerEmail,
                        s.SellerStatus,
                        s.DateTime,
                        s.LoginTry,
                        ProductCount = s.Products != null ? s.Products.Count : 0
                    })
                    .ToListAsync();

                return Json(new { success = true, data = sellers });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading sellers: " + ex.Message });
            }
        }


        // ============================================================
        // AJAX API METHODS - Dashboard统计

        [HttpGet]
        public async Task<IActionResult> GetDashboardStats()
        {
            if (!IsAdminLoggedIn())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var totalProducts = await db.Products.CountAsync();
                var totalUsers = await db.Users.CountAsync();
                var totalSellers = await db.Sellers.CountAsync();
                var activeProducts = await db.Products.CountAsync(p => p.ProductStatus == Status.Active);

                // 按status 算products
                var productsByStatus = await db.Products
                    .GroupBy(p => p.ProductStatus)
                    .Select(g => new {
                        status = g.Key.HasValue ? g.Key.Value.ToString() : "Unknown",
                        count = g.Count()
                    })
                    .ToListAsync();

                // latest的5个products
                var recentProducts = await db.Products
                    .OrderByDescending(p => p.ProductID)
                    .Take(5)
                    .Select(p => new {
                        productName = p.ProductName,
                        productPrice = p.UnitPrice
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        totalProducts,
                        totalUsers,
                        totalSellers,
                        activeProducts,
                        productsByStatus,
                        recentProducts
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading stats: " + ex.Message });
            }
        }

        //======================================================================
        // Helper method
        private bool IsAdminLoggedIn()
        {
            var adminId = HttpContext.Session.GetString("LoginAdmin");
            return !string.IsNullOrEmpty(adminId);
        }

        private async Task SignInAsync(Admin admin)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, admin.AdminID.ToString()),
                new Claim(ClaimTypes.Name, admin.AdminUsername),
                new Claim(ClaimTypes.Role, "Admin")
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

        private bool ProductExists(int id)
        {
            return db.Products.Any(e => e.ProductID == id);
        }

        private bool UserExists(int id)
        {
            return db.Users.Any(e => e.UserID == id);
        }

        private bool SellerExists(int id)
        {
            return db.Sellers.Any(e => e.SellerID == id);
        }
    }
}
