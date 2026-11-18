# Eduvo E-Commerce Modernization

I built this ASP.NET Core MVC e-commerce application in 2025 for Eduvo Academy’s DSE207 module. I came back to it in 2026 while looking for an internship, at a time when AI could generate a new portfolio project in an afternoon.

I wanted to see whether I could take something I had already written, understand where it broke, and make it genuinely better. I used AI tools during the modernization. The work I want to show is tracing a failing request through the view, controller, and database, then proving that the repair holds up in a real workflow.

## What you can do

| Role | Workflow |
| --- | --- |
| Administrator | Manage product listings, approve accounts, and change product, seller, and shopper statuses. |
| Seller | Create and edit listings, update stock, and view customer insights. |
| Shopper | Browse products, use the cart and promotions, place a demo order, and view order history. Register and recover a password using a code delivered to Mailpit. |

## Architecture

The application uses C#, ASP.NET Core MVC on .NET 8, Razor, jQuery, Entity Framework Core, and SQL Server. Razor renders the pages and antiforgery tokens. Forms and AJAX requests go to the role-specific MVC controllers, which apply cookie authentication, check the current role and session, and use EF Core for data access. Mailpit captures demo email locally.

Checkout loads current product prices, validates stock and any applied promotion, and records the order, promotion use, and inventory change in a database transaction. A stored checkout identifier helps prevent repeat submissions. This coursework flow never asks for card details or processes a payment.

## Build and test

From the repository root in a Linux GitHub Codespace or another environment with a compatible .NET SDK and the .NET 8 runtime:

```bash
dotnet restore Eduvo.Tests/Eduvo.Tests.csproj
dotnet build JQuery-ECommerceAss.csproj --no-restore
dotnet test Eduvo.Tests/Eduvo.Tests.csproj --no-restore
```

For an empty disposable development environment with Docker, `./scripts/start-demo.sh` starts SQL Server and Mailpit, applies migrations, and seeds fictional demo accounts. The script is designed for a fresh environment and cleans up the containers it starts. Check `docker ps -a` first and use the existing instance if the demo services or database are already present. Keep Codespaces port visibility private. Use the terminal output for the sample sign-ins.

The [verification record](docs/VERIFICATION.md) has the observed browser, database, and test results. The automated HTTP integration tests use EF Core’s in-memory provider. The Codespace browser checks used SQL Server and Mailpit.

## Portfolio walkthrough

The [recording storyboard](docs/VERIFICATION.md#recording-storyboard) follows one listing through Admin, Seller, and Shopper. The screenshots come from the private demo with fictional data. [Admin products](docs/screenshots/admin-products.png) · [Admin on a phone](docs/screenshots/admin-mobile.png) · [Seller products](docs/screenshots/seller-products.png) · [Shopper checkout](docs/screenshots/shopper-checkout.png)

## Known limits

The application is a private coursework demo. It still uses session state alongside role cookies, some icons and styles depend on external CDNs, and uploads are checked by type signature and size without scanning or re-encoding the files. The [verification record](docs/VERIFICATION.md#remaining-work) lists the security and accessibility checks that are still open.

**Chin Yi Zhe** · Eduvo Academy DSE207 coursework (2025) · Modernization (2026)
