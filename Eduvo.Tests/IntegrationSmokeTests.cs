using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using JQuery_ECommerceAss.Common.Enum;
using JQuery_ECommerceAss.Common.Security;
using JQuery_ECommerceAss.Data;
using JQuery_ECommerceAss.Models;

namespace Eduvo.Tests;

/// <summary>
/// Boots the real application in a test server, replaces SQL Server with the
/// EF Core in-memory provider, and drives the actual HTTP endpoints with the
/// same payloads the real views send.
/// </summary>
public class SmokeTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            // EF Core 9 registers provider configuration through this interface;
            // without removing it, both SQL Server and InMemory end up on one options object.
            services.RemoveAll(typeof(Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<AppDbContext>));
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("eduvo-smoke-db")
                    .ConfigureWarnings(w => w.Ignore(
                        Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));
        });
    }

    public void Seed(Action<AppDbContext> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        seed(db);
        db.SaveChanges();
    }

    public T Observe<T>(Func<AppDbContext, T> query)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return query(db);
    }
}

public class IntegrationSmokeTests : IClassFixture<SmokeTestFactory>
{
    private readonly SmokeTestFactory _factory;
    private static int _suffix;

    public IntegrationSmokeTests(SmokeTestFactory factory)
    {
        _factory = factory;
        _suffix++;
    }

    private string Run => _suffix.ToString();

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

    private static async Task<string> GetAsync(string html, string inputName)
    {
        var match = Regex.Match(
            html,
            "name=\"" + inputName + "\"[^>]*value=\"([^\"]+)\"");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static async Task<string> BodyAsync(HttpResponseMessage response)
    {
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task<bool> PostFormAsync(HttpClient client, string url, Dictionary<string, string> fields)
    {
        var response = await client.PostAsync(url, new FormUrlEncodedContent(fields));
        var body = await BodyAsync(response);
        return response.IsSuccessStatusCode && body.Contains("\"success\":true");
    }

    private sealed record SeededAccounts(int SellerId, int UserId, int ProductId);

    private SeededAccounts SeedAccounts(
        string productQuantity = "10", string unitPrice = "50.00")
    {
        var password = PasswordSecurity.Hash("Smoke123!");
        var seller = new Seller
        {
            SellerUsername = $"smoke-seller-{Run}",
            SellerPassword = password,
            SellerEmail = $"smoke-seller-{Run}@example.test",
            LoginTry = 0,
            SellerStatus = Status.Approve
        };
        var user = new User
        {
            Username = $"smoke-user-{Run}",
            UserPassword = password,
            UserGmail = null,
            LoginTry = 0,
            UserStatus = Status.Active
        };
        SeededAccounts result = null!;
        _factory.Seed(db =>
        {
            db.Sellers.Add(seller);
            db.Users.Add(user);
            db.SaveChanges();
            var product = new Product
            {
                ProductName = $"Smoke Laptop {Run}",
                Description = "Smoke test product",
                UnitPrice = decimal.Parse(unitPrice),
                Quantity = int.Parse(productQuantity),
                SellerID = seller.SellerID,
                ProductStatus = Status.Active
            };
            db.Products.Add(product);
            db.SaveChanges();
            result = new SeededAccounts(seller.SellerID, user.UserID, product.ProductID);
        });
        return result;
    }

    [Fact]
    public async Task AdminDashboard_RedirectsAnonymousUserToLogin()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/Admin/Dashboard");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Admin/Login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task AdminLogin_FullFlow_ReachesProtectedPage()
    {
        SeedAccounts();
        _factory.Seed(db => db.Admins.Add(new Admin
        {
            AdminUsername = $"smoke-admin-{Run}",
            AdminPassword = PasswordSecurity.Hash("Admin123!")
        }));

        var client = CreateClient();

        var loginPage = await client.GetAsync("/Admin/Login");
        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);
        var loginHtml = await BodyAsync(loginPage);
        var token = await GetAsync(loginHtml, "__RequestVerificationToken");
        Assert.False(string.IsNullOrEmpty(token), "Admin login page did not render an antiforgery token.");

        var login = await client.PostAsync("/Admin/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["AdminUsername"] = $"smoke-admin-{Run}",
            ["AdminPassword"] = "Admin123!",
            ["__RequestVerificationToken"] = token
        }));

        if (login.StatusCode != HttpStatusCode.Redirect)
        {
            var body = await BodyAsync(login);
            Assert.Fail($"Admin login POST returned {(int)login.StatusCode} instead of a redirect. Response excerpt: {Truncate(body)}");
        }

        var follow = await client.GetAsync(login.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, follow.StatusCode);
    }

    [Theory]
    [InlineData("Index", "UpdateProductStatusAjax")]
    [InlineData("ManageUser", "UpdateUserStatusAjax")]
    [InlineData("ManageSeller", "UpdateSellerStatusAjax")]
    public async Task AdminStatusChanges_UseTheRenderedAntiforgeryHeader(string page, string action)
    {
        var accounts = SeedAccounts();
        using var client = CreateClient();
        await SignInAdminAsync(client);

        var html = await client.GetStringAsync($"/Admin/{page}");
        Assert.Contains("/js/ajax-antiforgery.js", html);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/js/ajax-antiforgery.js")).StatusCode);
        var token = await GetAsync(html, "__RequestVerificationToken");
        Assert.NotEmpty(token);

        var id = page switch
        {
            "Index" => accounts.ProductId,
            "ManageUser" => accounts.UserId,
            _ => accounts.SellerId
        };
        if (page == "ManageSeller")
        {
            using var sellers = JsonDocument.Parse(await client.GetStringAsync("/Admin/GetSellersJson"));
            var seller = Assert.Single(sellers.RootElement.GetProperty("data").EnumerateArray(),
                item => item.GetProperty("sellerID").GetInt32() == id);
            Assert.Equal((int)Status.Approve, seller.GetProperty("sellerStatus").GetInt32());
            Assert.Contains("<option value=\"4\">Approved</option>", html);
        }

        var fields = new Dictionary<string, string>
        {
            ["id"] = id.ToString(),
            ["status"] = ((int)Status.Inactive).ToString()
        };
        client.DefaultRequestHeaders.Add("RequestVerificationToken", token);
        Assert.True(await PostFormAsync(client, $"/Admin/{action}", fields));
        Assert.Equal(Status.Inactive, _factory.Observe(db => page switch
        {
            "Index" => db.Products.Single(p => p.ProductID == id).ProductStatus,
            "ManageUser" => db.Users.Single(u => u.UserID == id).UserStatus,
            _ => db.Sellers.Single(s => s.SellerID == id).SellerStatus
        }));
    }

    [Fact]
    public async Task AdminProductManagement_CreatesEditsAndDeletesProduct()
    {
        var accounts = SeedAccounts();
        using var client = CreateClient();
        await SignInAdminAsync(client);
        var page = await client.GetStringAsync("/Admin/Index");
        client.DefaultRequestHeaders.Add("RequestVerificationToken",
            await GetAsync(page, "__RequestVerificationToken"));

        var productName = $"Admin Product {Run}";
        var fields = new Dictionary<string, string>
        {
            ["ProductName"] = productName,
            ["Description"] = "Admin smoke test",
            ["UnitPrice"] = "30.00",
            ["Quantity"] = "3",
            ["SellerID"] = accounts.SellerId.ToString(),
            ["ProductStatus"] = ((int)Status.Active).ToString()
        };
        Assert.True(await PostFormAsync(client, "/Admin/CreateProductAjax", fields));
        Assert.Contains(productName, await client.GetStringAsync("/Admin/GetProductsJson"));

        var id = _factory.Observe(db => db.Products.Single(p => p.ProductName == productName).ProductID);
        fields["ProductID"] = id.ToString();
        fields["Quantity"] = "6";
        Assert.True(await PostFormAsync(client, "/Admin/UpdateProductAjax", fields));
        Assert.Equal(6, _factory.Observe(db => db.Products.Single(p => p.ProductID == id).Quantity));

        Assert.True(await PostFormAsync(client, "/Admin/DeleteProductAjax", new() { ["id"] = id.ToString() }));
        Assert.False(_factory.Observe(db => db.Products.Any(p => p.ProductID == id)));
    }

    private async Task SignInAdminAsync(HttpClient client)
    {
        _factory.Seed(db => db.Admins.Add(new Admin
        {
            AdminUsername = $"smoke-admin-{Run}",
            AdminPassword = PasswordSecurity.Hash("Admin123!")
        }));
        var loginPage = await client.GetStringAsync("/Admin/Login");
        var login = await client.PostAsync("/Admin/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["AdminUsername"] = $"smoke-admin-{Run}",
            ["AdminPassword"] = "Admin123!",
            ["__RequestVerificationToken"] = await GetAsync(loginPage, "__RequestVerificationToken")
        }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
    }

    [Fact]
    public async Task SellerLogin_AcceptsPayloadExactlyAsTheRealViewSendsIt()
    {
        SeedAccounts();

        var client = CreateClient();

        var page = await client.GetAsync("/Seller/Login");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await BodyAsync(page);
        var token = await GetAsync(html, "__RequestVerificationToken");
        Assert.False(string.IsNullOrEmpty(token), "Seller login page did not render an antiforgery token.");

        var login = await client.PostAsync("/Seller/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["SellerUsername"] = $"smoke-seller-{Run}",
            ["SellerPassword"] = "Smoke123!",
            ["__RequestVerificationToken"] = token
        }));

        var body = await BodyAsync(login);

        if (!login.IsSuccessStatusCode)
        {
            Assert.Fail($"Seller login POST returned {(int)login.StatusCode}. Response excerpt: {Truncate(body)}");
        }

        Assert.Contains("\"success\":true", body);
    }

    [Fact]
    public async Task SellerAddProduct_WithRenderedToken_Succeeds_AndWithoutToken_IsRejected()
    {
        var accounts = SeedAccounts();
        var client = CreateClient();

        var login = await client.PostAsync("/Seller/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["SellerUsername"] = $"smoke-seller-{Run}",
            ["SellerPassword"] = "Smoke123!",
            ["__RequestVerificationToken"] = await GetAsync(await BodyAsync(await client.GetAsync("/Seller/Login")), "__RequestVerificationToken")
        }));
        Assert.True((await BodyAsync(login)).Contains("\"success\":true"), "Seller login failed, cannot test product management.");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Seller/Dashboard")).StatusCode);
        Assert.Contains($"Smoke Laptop {Run}", await client.GetStringAsync("/Seller/ManageProducts"));
        var editHtml = await client.GetStringAsync($"/Seller/EditProduct/{accounts.ProductId}");
        var edit = await client.PostAsync($"/Seller/EditProduct/{accounts.ProductId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["ProductName"] = $"Smoke Laptop {Run}",
                ["Description"] = "Updated inventory",
                ["UnitPrice"] = "50.00",
                ["Quantity"] = "7",
                ["__RequestVerificationToken"] = await GetAsync(editHtml, "__RequestVerificationToken")
            }));
        Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);
        Assert.Equal(7, _factory.Observe(db => db.Products.Single(p => p.ProductID == accounts.ProductId).Quantity));

        var page = await client.GetAsync("/Seller/AddProduct");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await BodyAsync(page);
        var token = await GetAsync(html, "__RequestVerificationToken");
        Assert.False(string.IsNullOrEmpty(token), "Seller AddProduct page did not render an antiforgery token.");

        var withoutToken = await client.PostAsync("/Seller/AddProduct", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ProductName"] = "Tokenless Product",
            ["Description"] = "Should be rejected",
            ["UnitPrice"] = "10",
            ["Quantity"] = "1"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, withoutToken.StatusCode);

        var withToken = await client.PostAsync("/Seller/AddProduct", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ProductName"] = "Token Product",
            ["Description"] = "Submitted with the token the view renders",
            ["UnitPrice"] = "10",
            ["Quantity"] = "1",
            ["__RequestVerificationToken"] = token
        }));

        if (withToken.StatusCode != HttpStatusCode.Redirect && withToken.StatusCode != HttpStatusCode.OK)
        {
            var body = await BodyAsync(withToken);
            Assert.Fail($"Seller AddProduct with a valid token returned {(int)withToken.StatusCode}. Response excerpt: {Truncate(body)}");
        }

        var saved = _factory.Observe(db =>
            db.Products.Any(p => p.ProductName == "Token Product"));
        Assert.True(saved, "Product submitted with a valid token was not persisted.");

        var deleteHtml = await client.GetStringAsync($"/Seller/DeleteProduct/{accounts.ProductId}");
        var delete = await client.PostAsync($"/Seller/DeleteProduct/{accounts.ProductId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = await GetAsync(deleteHtml, "__RequestVerificationToken")
            }));
        Assert.Equal(HttpStatusCode.Redirect, delete.StatusCode);
        Assert.False(_factory.Observe(db => db.Products.Any(p => p.ProductID == accounts.ProductId)));
    }

    [Fact]
    public async Task UserLogin_Browse_Cart_Checkout_FullFlow()
    {
        var accounts = SeedAccounts();
        var userId = accounts.UserId;
        var productId = accounts.ProductId;
        var client = CreateClient();
        var promotionCode = $"SMOKE-{Run}";
        _factory.Seed(db => db.UserPromotions.Add(new UserPromotion
        {
            UserID = userId,
            Promotion = new Promotion
            {
                PromotionCode = promotionCode,
                PromotionName = "Smoke promotion",
                PromotionStatus = Status.Active,
                DiscountPercentage = 10,
                StartDate = DateTime.Now.AddDays(-1)
            }
        }));

        // 1. Login with exactly the payload the real AJAX view sends, including
        // the antiforgery token rendered by @Html.AntiForgeryToken() in the form.
        var loginPage = await client.GetAsync("/User/Login");
        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);
        var loginHtml = await BodyAsync(loginPage);
        var loginToken = await GetAsync(loginHtml, "__RequestVerificationToken");
        Assert.False(string.IsNullOrEmpty(loginToken), "Shopper login page did not render an antiforgery token.");

        var login = await client.PostAsync("/User/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = $"smoke-user-{Run}",
            ["UserPassword"] = "Smoke123!",
            ["__RequestVerificationToken"] = loginToken
        }));
        var loginBody = await BodyAsync(login);
        if (!login.IsSuccessStatusCode)
        {
            Assert.Fail($"Shopper login POST returned {(int)login.StatusCode}. The real User/Login.cshtml AJAX posts exactly this payload, including the rendered antiforgery token. Response excerpt: {Truncate(loginBody)}");
        }
        Assert.Contains("\"success\":true", loginBody);
        Assert.Contains(promotionCode, await client.GetStringAsync("/User/MyPromotions"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/User/Profile")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/User/UpdatePassword")).StatusCode);

        // 2. Catalogue renders the product and the antiforgery token partial.
        var products = await client.GetAsync("/User/Products");
        Assert.Equal(HttpStatusCode.OK, products.StatusCode);
        var productsHtml = await BodyAsync(products);
        Assert.Contains($"Smoke Laptop {Run}", productsHtml);
        var ajaxToken = await GetAsync(productsHtml, "__RequestVerificationToken");
        Assert.False(string.IsNullOrEmpty(ajaxToken), "Products page did not render an antiforgery token.");

        // 3. Add to cart the way the patched Products.cshtml does: same payload,
        // plus the RequestVerificationToken header attached by _AntiforgeryHeader.cshtml.
        var tokenlessAdd = await client.PostAsync("/User/AddToCartAjax", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["productId"] = productId.ToString(),
            ["quantity"] = "2"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, tokenlessAdd.StatusCode);

        var addRequest = new HttpRequestMessage(HttpMethod.Post, "/User/AddToCartAjax")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["productId"] = productId.ToString(),
                ["quantity"] = "2"
            })
        };
        addRequest.Headers.Add("RequestVerificationToken", ajaxToken);
        var addToCart = await client.SendAsync(addRequest);
        var cartBody = await BodyAsync(addToCart);
        if (!addToCart.IsSuccessStatusCode || !cartBody.Contains("\"success\":true"))
        {
            Assert.Fail($"AddToCartAjax returned {(int)addToCart.StatusCode}. Response excerpt: {Truncate(cartBody)}");
        }

        // 4. Cart page renders.
        var cart = await client.GetAsync("/User/Cart");
        Assert.Equal(HttpStatusCode.OK, cart.StatusCode);
        Assert.Contains($"Smoke Laptop {Run}", await BodyAsync(cart));

        // 5. Checkout page issues the checkout token.
        var checkout = await client.GetAsync("/User/Checkout");
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);
        var checkoutHtml = await BodyAsync(checkout);
        var applyPromotion = await client.PostAsync("/User/ApplyPromoCode",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["promoCode"] = promotionCode,
                ["__RequestVerificationToken"] = await GetAsync(checkoutHtml, "__RequestVerificationToken")
            }));
        Assert.Equal(HttpStatusCode.Redirect, applyPromotion.StatusCode);
        checkoutHtml = await client.GetStringAsync("/User/Checkout");
        Assert.Contains("RM 96.00", checkoutHtml);
        var checkoutToken = await GetAsync(checkoutHtml, "checkoutToken");
        Assert.False(string.IsNullOrEmpty(checkoutToken), "Checkout page did not render a checkout token.");

        // 6. Pay exactly like the real checkout form does: the form posts the
        // antiforgery token as a field (rendered by @Html.AntiForgeryToken()).
        var paymentToken = await GetAsync(checkoutHtml, "__RequestVerificationToken");
        Assert.False(string.IsNullOrEmpty(paymentToken), "Checkout page did not render an antiforgery token.");

        var payment = await client.PostAsync("/User/ProcessPayment", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["cardNumber"] = "4111111111111111",
            ["subtotal"] = "100",
            ["gst"] = "6",
            ["promotion"] = "0",
            ["promoCode"] = "",
            ["checkoutToken"] = checkoutToken,
            ["__RequestVerificationToken"] = paymentToken
        }));

        if (payment.StatusCode != HttpStatusCode.Redirect)
        {
            var body = await BodyAsync(payment);
            Assert.Fail($"ProcessPayment returned {(int)payment.StatusCode}. Response excerpt: {Truncate(body)}");
        }

        // 7. Server-side effects actually happened.
        var transactionCount = _factory.Observe(db => db.TransactionHis.Count(t => t.UserID == userId));
        Assert.Equal(1, transactionCount);
        Assert.Equal(96m, _factory.Observe(db => db.TransactionHis.Single(t => t.UserID == userId).Amount));
        Assert.True(_factory.Observe(db => db.UserPromotions
            .Single(p => p.UserID == userId && p.Promotion!.PromotionCode == promotionCode).IsUsed));
        Assert.Contains("Purchase", await client.GetStringAsync("/User/TransactionHistory"));

        var stock = _factory.Observe(db => db.Products.Find(productId)!.Quantity);
        Assert.Equal(8, stock);

        var cartRows = _factory.Observe(db => db.Carts.Count(c => c.UserID == userId));
        Assert.Equal(0, cartRows);

        // 8. Replaying the same payment must not create a second transaction.
        var replay = await client.PostAsync("/User/ProcessPayment", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["cardNumber"] = "4111111111111111",
            ["subtotal"] = "100",
            ["gst"] = "6",
            ["promotion"] = "0",
            ["promoCode"] = "",
            ["checkoutToken"] = checkoutToken,
            ["__RequestVerificationToken"] = paymentToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, replay.StatusCode);

        var replayCount = _factory.Observe(db => db.TransactionHis.Count(t => t.UserID == userId));
        Assert.Equal(1, replayCount);
    }

    private static string Truncate(string value)
    {
        var clean = value.Replace('\n', ' ').Replace('\r', ' ');
        return clean.Length <= 300 ? clean : clean[..300] + "...";
    }
}
