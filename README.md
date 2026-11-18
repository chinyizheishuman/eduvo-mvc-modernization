# Eduvo E-Commerce

**Chin Yi Zhe · Eduvo Academy · DSE207 coursework, 2025**

I built this shop for class in 2025 and came back to it in 2026 while looking for an internship. By then I could have generated a fresh portfolio project in an afternoon. I wanted to get better at fixing the one I already had.

The app built, but **Add Product** and **Checkout** both crashed. Fixing those clicks meant finding the missing pages, following the requests through the controllers, and testing them with a real database.

![Shopper checkout showing a discount and the final demo order total](docs/screenshots/shopper-checkout.png)

*Checkout with a sample promotion applied. This is a demo order, so there are no card fields.*

## Follow one listing through the shop

| Who | What to try |
| --- | --- |
| **Seller** | Add a listing, set its price and stock, and see it marked as pending. [See Manage Products](docs/screenshots/seller-products.png) |
| **Admin** | Activate the listing so it appears in the shop. The same area handles seller and shopper accounts. [See Product Management](docs/screenshots/admin-products.png) · [Phone view](docs/screenshots/admin-mobile.png) |
| **Shopper** | Add the item to the cart, apply an assigned promotion, and find the demo order in order history. Registration and password reset codes go to Mailpit. |

## The bit I’m proud of

My favorite check was two shoppers trying to buy the last unit. In SQL Server, one order went through, the other shopper landed back in the cart, and the listing ended up with zero stock. The recovered checkout read inventory before the transaction started. Moving the read into the transaction and handling the conflict meant the second shopper got a useful response.

The recovery and hardening work passed **23 automated tests**. Those tests use EF Core’s in-memory provider. I also ran the three roles in Chromium against SQL Server and Mailpit. [See what passed and what still needs work](docs/VERIFICATION.md)

## Run it yourself

You’ll need a .NET SDK that can target .NET 8, the .NET 8 runtime, and Docker for the full demo. From the repo root:

```bash
dotnet restore Eduvo.Tests/Eduvo.Tests.csproj
dotnet build JQuery-ECommerceAss.csproj --no-restore
dotnet test Eduvo.Tests/Eduvo.Tests.csproj --no-restore
```

In a fresh disposable environment, run `./scripts/start-demo.sh` to start SQL Server and Mailpit, apply the migrations, and seed sample accounts. The terminal prints the sign-in details. In a Codespace, keep the forwarded ports private. If you already have a demo database you want to keep using, check `docker ps -a` before starting a fresh demo.

## Where to look in the code

- [Views](Views) holds the Razor pages. [Controllers](Controllers) handles the requests from each role.
- [AppDbContext](Data/AppDbContext.cs) maps the application data to SQL Server through EF Core. [CheckoutPricing](Common/Pricing/CheckoutPricing.cs) works out the subtotal, tax, and discount on the server.
- [Integration tests](Eduvo.Tests/IntegrationSmokeTests.cs) go through real MVC routes. [Modernization notes](docs/MODERNIZATION-NOTES.md) compare the original coursework with the earlier recovery.

This is still a coursework demo. It uses an in-process session, some assets come from CDNs, and uploads are checked by size and signature. [Known limits](docs/VERIFICATION.md#remaining-work) · [Demo recording outline](docs/VERIFICATION.md#recording-storyboard)
