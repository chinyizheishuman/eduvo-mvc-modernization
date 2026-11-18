# Eduvo E-Commerce Modernization

### You might be asking why.

This started as an ASP.NET Core MVC e-commerce assignment I built for Eduvo Academy's DSE207 module back in 2025.

A year later, I found myself looking through the source code again.

A lot had changed since I originally worked on it. AI coding tools had become part of almost every development conversation. Job listings were asking students to know increasingly broad stacks, sometimes with experience requirements that didn't seem to belong in an internship advertisement.

And somewhere in all of that, I still had this old project sitting around.

I could have generated a new application, put a modern interface on it, and called it a portfolio project.

Instead, I wanted to find out something else:

**Could I take software I'd already written, understand what was wrong with it, and make it genuinely better?**

That's what this repository is about.

---

## The original project

The application was built around three roles:

- **Administrator:** Account management, product administration, and user status.
- **Seller:** Product listings, inventory, and catalogue management.
- **Shopper:** Product browsing, shopping cart, promotions, checkout, and transaction history.

It uses C#, ASP.NET Core MVC, Razor views, Entity Framework Core, SQL Server, JavaScript, and jQuery.

It was coursework. It wasn't designed to become Amazon, it's just a look back on whether humans could still really code, engineer, imagine.

But it was a real application with interconnected workflows, a database, authentication, and business logic. That made it worth revisiting for me personally.

## What I found when I came back

The original code had the sort of problems that are easy to overlook when you're focused on finishing an assignment.

Password handling needed attention. Some authorization decisions relied on session state. Checkout trusted information that should have been recalculated by the server. The application also needed better protection against repeated requests and inconsistent database updates.

Getting everything to compile wasn't going to answer those problems.

I wanted to see whether the application could behave correctly when the input was wrong, a request was repeated, or a user tried to perform an action outside their role.

That became the focus of the modernization.

## What changed

| Area | Work completed during modernization |
|---|---|
| Password security | PBKDF2-SHA256 hashing, salted passwords, legacy-password upgrades |
| Account recovery | OTP verification, expiring reset authorization, configurable email delivery |
| Authentication | Cookie authentication and role policies alongside legacy session compatibility |
| Checkout | Server-side calculation of product prices, discounts, tax, and final totals |
| Data consistency | Transactional checkout and persisted duplicate-request protection |
| Authorization | Seller ownership checks, account status checks, and antiforgery protection |
| Uploads | Generated filenames and file-signature checks |
| Testing | Focused automated tests for password, pricing, promotion, and upload logic |

These changes didn't turn the application into a finished commercial platform.

They made the existing application easier to reason about, test, and improve.

And importantly, they didn't require throwing away the entire original project.

## The engineering behind it

The application still follows its original MVC architecture.

```text
                 Browser
                    |
            Razor Views / JS
                    |
             MVC Controllers
                    |
             Application Logic
                    |
            Entity Framework
                    |
                SQL Server
```

The development environment also uses Mailpit to capture password-recovery emails without sending them to actual recipients.

For the checkout flow, the important change is that the server makes the final decisions about prices, stock, promotions, and totals.

A browser can display a price. It shouldn't get to decide what the database records as the final price.

The modernization also introduces persisted checkout request identifiers to help prevent the same submission from creating duplicate transactions.

## Why GitHub Codespaces?

Partly because I didn't want to rebuild an entire old Windows development environment just to recover an assignment.

My daily computer is a Windows business laptop. The original project came from an older development setup, and I wanted something more reproducible than depending on whichever machine happened to have SQL Server installed.

Codespaces gave me a Linux development environment where I could work with the .NET SDK, build the application, and test it against disposable services.

It also meant I could keep the database and mail-testing services isolated from anything public.

It isn't a production deployment, and I'm not presenting it as one.

## What actually works?

The modernization session recorded these results:

| Verification | Recorded result |
|---|---|
| .NET 8 application build | Passed, 0 warnings and 0 errors |
| Automated tests | 10 focused tests passed |
| Database migrations | Applied to disposable SQL Server |
| Password reset | OTP captured through Mailpit |
| Authentication | Password upgrade and reset scenarios checked |
| Protected routes | Unauthenticated access rejected in tested scenarios |
| Duplicate checkout | Persisted request uniqueness constraint added |
| Production deployment | Not performed |

These are development verification results, not proof that every application path has been tested.

The build and test rows were re-verified on 18 November 2026 in an independent Linux environment using the repository snapshot with .NET SDK 8.0.428: restore succeeded, build finished with 0 warnings and 0 errors, and all 10 tests passed.

### Reproducing the build

From the repository root:

```bash
dotnet restore JQuery-ECommerceAss.csproj
dotnet build JQuery-ECommerceAss.csproj
dotnet test Eduvo.Tests/Eduvo.Tests.csproj
```

To run the application after configuring the development database:

```bash
dotnet run --project JQuery-ECommerceAss.csproj
```

The application targets .NET 8. Use a compatible runtime and provide database and SMTP configuration through development settings or environment variables.

The original verification environment used a runtime roll-forward workaround; the correct .NET 8 runtime is preferable for normal development.

## Running the disposable demo

The repository includes `scripts/start-demo.sh`, which automates the whole disposable demo environment:

- starts disposable SQL Server 2022 and Mailpit containers, bound only to `127.0.0.1`
- generates a temporary SQL password used only inside those containers
- applies the database migrations
- seeds fictional demo accounts and a demo product, then prints the demo logins
- starts the application on port `5067` and prints the browser URLs
- stops and removes the containers it started when you press `Ctrl+C`

```bash
./scripts/start-demo.sh
```

The exact SQL Server and Mailpit container commands live in the script itself, so the standard path needs no Docker Compose file and no manual environment setup. Mailpit's inbox sits at `http://127.0.0.1:8025` and captures password-recovery email without sending anything to real recipients.

This is a disposable local environment, not a deployment.

The complete start-to-finish screen-recording walkthrough, including a manual alternative that runs every piece by hand, lives in [`docs/demo-recording-script.md`](docs/demo-recording-script.md).

## What's still wrong with it?

Quite a few things.

Some recovered workflows still use session-based authorization. The application hasn't been fully migrated to ASP.NET Core Identity.

Inventory concurrency needs stronger testing with competing requests. There isn't a complete automated browser or accessibility test suite. Uploaded files aren't malware-scanned or re-encoded.

There's also no real payment provider. Checkout remains a coursework workflow rather than a live payment system.

I could hide all that behind a polished interface and a few screenshots, but it would give the wrong impression of the engineering work.

These aren't accomplishments. They're the remaining problems.

## A demonstration worth showing

A useful demonstration should show more than a landing page.

It should walk through the actual application:

1. Administrator authentication and access to protected pages.
2. Seller product creation and inventory management.
3. Shopper browsing, cart updates, and promotions.
4. Checkout with totals recalculated by the server.
5. Password recovery using a locally captured OTP.
6. The corresponding database changes and automated tests.

Each demonstrated behavior should be reproducible using sample accounts and fictional products.

For this project, a successful demonstration means someone can see the workflow, inspect the source code, and understand how it was tested.

Not just watch an animation of a website that might or might not work.

The practical demo uses a private application port and disposable local services. Mailpit captures reset email without sending messages to real recipients, and the database is disposable rather than a production system.

## Why any of this matters in 2026

AI can generate controllers, write tests, scaffold interfaces, and produce documentation.

I've used AI development tools during this modernization too.

But generating code and understanding code are not the same thing.

A generated solution can compile and still make the wrong assumptions about authorization, pricing, database consistency, or what the application actually needs.

The frustrating part of maintaining older software is that you don't get to start with a perfect specification. You inherit previous decisions, incomplete assumptions, and things that worked well enough at the time.

You have to investigate before you change them.

That's the part of software engineering I wanted this project to represent.

Not how fast I could generate another repository, but whether I could examine an existing one, make defensible decisions, and show the results.

## Where it goes from here

The next improvements are straightforward to identify, even if implementing them properly takes more work:

- Integration tests around authentication, checkout, and account recovery.
- Inventory concurrency handling and competing-request tests.
- Removal of remaining session-only authorization decisions.
- Browser-level testing and accessibility improvements.
- More consistent UI behavior across administrator, seller, and shopper pages.
- Deployment and migration verification in a controlled environment.

I'm keeping the original coursework history visible while documenting the subsequent changes.

This isn't a finished product dressed up as an open-source success story.

It's an old application I'm trying to engineer better than I did the first time.

And I think that's worth showing.

---

**Chin Yi Zhe**
Eduvo Academy · DSE207 coursework (2025)
Modernization started in 2026

*C#, ASP.NET Core MVC, Entity Framework Core, SQL Server*

**Status:** Active modernization. Development build and focused tests reported passing. Not production-ready.
