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

### From Codespace to recorded demonstration

This is the complete start-to-finish path I use for a screen recording. It is
deliberately visible here because the demonstration should be reproducible,
not a hidden sequence of clicks.

#### 1. Open a clean Codespace terminal

Open the repository in a Codespace and start a terminal at the repository root:

```bash
pwd
git status --short
dotnet --info
```

The expected working directory is `/workspaces/eduvo-mvc-modernization`.
Before opening a browser, establish the baseline:

```bash
dotnet restore JQuery-ECommerceAss.csproj
dotnet build JQuery-ECommerceAss.csproj
dotnet test Eduvo.Tests/Eduvo.Tests.csproj
```

The recorded baseline is a successful build with 0 warnings and 0 errors, and
10 focused tests passing.

The repeatable launch entry point is:

```bash
./scripts/start-demo.sh
```

The script starts disposable SQL Server and Mailpit containers, generates a
temporary SQL password, applies the database migrations, starts Eduvo on port
`5067`, and prints the browser URLs. It stops the containers that it started
when you press `Ctrl+C`. SQL Server is bound only to `127.0.0.1:1433`.

#### 2. Start disposable services

The database and SMTP ports remain private. I use a disposable SQL Server
instance configured for this Codespace and apply migrations to that database
only. This repository does not include a Docker Compose file, so the exact
SQL Server container command depends on the disposable service setup being
used and is not invented here.

For local email capture, the tested Mailpit command is:

```bash
docker run --rm \
  --name eduvo-mailpit \
  -p 127.0.0.1:1025:1025 \
  -p 127.0.0.1:8025:8025 \
  axllent/mailpit
```

Mailpit provides SMTP at `127.0.0.1:1025` and its private inbox at
`http://127.0.0.1:8025`. Check the inbox before starting the application:

```bash
curl -I http://127.0.0.1:8025
```

The response should be HTTP 200. If it is not, fix Mailpit before debugging
the application.

#### 3. Configure the disposable environment

I provide the disposable database and SMTP settings through environment
variables, never by committing credentials:

```bash
export ConnectionStrings__JQuery-ECommerceAss='Server=<disposable-sql-host>;Database=EduvoTest;User Id=<user>;Password=<password>;TrustServerCertificate=True'
export Smtp__Host='127.0.0.1'
export Smtp__Port='1025'
export Smtp__From='no-reply@example.test'
export Smtp__Username=''
export Smtp__Password=''
```

The database placeholders are replaced only in the local terminal. Mailpit
does not require SMTP authentication.

#### 4. Apply migrations

With the disposable SQL Server running:

```bash
dotnet ef database update --project JQuery-ECommerceAss.csproj
```

The final migration creates `CheckoutAttempts` and its unique request index.
The demonstration database is seeded only with fictional accounts and
products.

#### 5. Start the application

With the intended .NET 8 runtime:

```bash
ASPNETCORE_URLS=http://0.0.0.0:5067 \
dotnet run --project JQuery-ECommerceAss.csproj
```

The original verification machine lacked the .NET 8 shared runtime and used
this local compatibility workaround:

```bash
ASPNETCORE_URLS=http://0.0.0.0:5067 \
DOTNET_ROLL_FORWARD=Major \
dotnet run --project JQuery-ECommerceAss.csproj --no-build
```

#### 6. Open only the application port

In the Codespace **Ports** panel:

1. Find port `5067`.
2. Open it in the browser for the recording.
3. Keep SQL Server, SMTP, and Mailpit ports private.
4. Return port `5067` to private after recording.

This is temporary access to a development process, not a deployment.

#### Starting URLs

Use these URLs after the application starts:

| Purpose | URL |
|---|---|
| Application starting point | `http://localhost:5067` |
| Administrator login | `http://localhost:5067/Admin/Login` |
| Administrator dashboard | `http://localhost:5067/Admin/Dashboard` |
| Seller login | `http://localhost:5067/Seller/Login` |
| Seller dashboard | `http://localhost:5067/Seller/Dashboard` |
| Seller products | `http://localhost:5067/Seller/ManageProducts` |
| Shopper login | `http://localhost:5067/User/Login` |
| Shopper catalogue | `http://localhost:5067/User/Products` |
| Shopper cart | `http://localhost:5067/User/Cart` |
| Shopper checkout | `http://localhost:5067/User/Checkout` |
| Shopper password reset | `http://localhost:5067/User/ForgotPassword` |
| Shopper OTP | `http://localhost:5067/User/OTP` |
| Seller password reset | `http://localhost:5067/Seller/ForgotPassword` |
| Seller OTP | `http://localhost:5067/Seller/OTP` |
| Local OTP inbox | `http://localhost:8025` |

#### 7. Record the application walkthrough

Begin with this explanation:

> This is recovered 2025 DSE207 coursework. I kept the original MVC project
> and modernized the highest-risk areas instead of replacing it with a new
> application.

Then show the workflow in this order:

1. **Administrator:** open `/Admin/Login`, sign in, and show a protected
   management page.
2. **Seller:** open `/Seller/Login`, sign in, and show seller product and
   stock management.
3. **Shopper:** open `/User/Login`, browse the catalogue, update the cart, and
   show promotion behavior.
4. **Checkout:** submit the cart and explain that the server reloads prices,
   stock, GST, and promotion eligibility instead of trusting browser totals.
5. **Password reset:** open `/User/ForgotPassword` or
   `/Seller/ForgotPassword`, request a reset for a fictional test account,
   open `http://127.0.0.1:8025`, enter the captured OTP, and change the
   password.
6. **Verification:** show that the old password fails and the new password
   succeeds.

Do not record real credentials, email addresses, passwords, or OTP values.
The purpose is to show the workflow and the engineering reasoning behind it.

#### 8. Explain what the recording proves

The recording demonstrates:

- the recovered application runs in a reproducible Linux environment
- role-protected workflows are reachable
- checkout decisions are made server-side
- password reset requires OTP verification
- Mailpit captures email without contacting real recipients
- the database and SMTP services remain disposable and private

It does not prove production payment processing, complete browser coverage,
load-tested inventory concurrency, or production deployment.

#### 9. Stop and clean up

When the recording is complete:

1. Stop the application with `Ctrl+C`.
2. Stop and remove only the named disposable SQL Server and Mailpit resources.
3. Return port `5067` to private.
4. Remove temporary environment variables from the terminal session.
5. Confirm no secrets or service data were written to the repository.
6. Run:

```bash
git status --short
git diff --check
```

This gives the recording a complete ending instead of stopping at the first
successful page.

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

The practical demo uses a private application port and disposable local
services. Mailpit captures reset email without sending messages to real
recipients, and the database is disposable rather than a production system.

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