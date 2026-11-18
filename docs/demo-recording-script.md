# Demo Recording Script

This is the complete start-to-finish path used for a screen recording of the
application. It is deliberately kept in the repository because the
demonstration should be reproducible, not a hidden sequence of clicks.

There are two ways to reach a running demo:

- **Scripted path (recommended):** `./scripts/start-demo.sh` automates steps 2
  through 5. It starts disposable SQL Server 2022 and Mailpit containers bound
  only to `127.0.0.1`, generates a temporary SQL password, applies migrations,
  seeds fictional demo accounts and a demo product, prints the demo logins and
  URLs, and stops the containers it started when you press `Ctrl+C`.
- **Manual path:** every step below, run by hand. Use it when you want full
  control over each disposable service, or when demonstrating without Docker.

Steps 1, 6, 7, 8, and 9 apply to both paths.

## 1. Open a clean Codespace terminal

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

## 2. Start disposable services

The database and SMTP ports remain private. The exact SQL Server and Mailpit
container commands used by the scripted path live in `scripts/start-demo.sh`.

For the manual path, the tested Mailpit command is:

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

## 3. Configure the disposable environment (manual path only)

The scripted path sets these automatically. When running manually, provide the
disposable database and SMTP settings through environment variables, never by
committing credentials:

```bash
export ConnectionStrings__JQuery-ECommerceAss='Server=<disposable-sql-host>;Database=EduvoTest;User Id=<user>;Password=<password>;TrustServerCertificate=True'
export Smtp__Host='127.0.0.1'
export Smtp__Port='1025'
export Smtp__From='no-reply@example.test'
export Smtp__Username=''
export Smtp__Password=''
```

The database placeholders are replaced only in the local terminal. Mailpit
does not require SMTP authentication. The scripted path creates a database
named `EduvoDemo`; the manual example above uses `EduvoTest`. Either name is
fine as long as the connection string and migrations point at the same
disposable database.

## 4. Apply migrations (manual path only)

With the disposable SQL Server running:

```bash
dotnet ef database update --project JQuery-ECommerceAss.csproj
```

The final migration creates `CheckoutAttempts` and its unique request index.
The scripted path applies migrations automatically and seeds only fictional
accounts and products.

## 5. Start the application (manual path only)

The scripted path handles this step, including the runtime configuration
below.

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

## 6. Open only the application port

In the Codespace **Ports** panel:

1. Find port `5067`.
2. Open it in the browser for the recording.
3. Keep SQL Server, SMTP, and Mailpit ports private.
4. Return port `5067` to private after recording.

This is temporary access to a development process, not a deployment.

## Starting URLs

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

## 7. Record the application walkthrough

Begin with this explanation:

> This is recovered 2025 DSE207 coursework. I kept the original MVC project
> and modernized the highest-risk areas instead of replacing it with a new
> application.

The scripted path seeds three fictional demo accounts (admin, seller, and
shopper) and one demo product, and prints their logins when it starts. Use
those accounts for the recording.

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

## 8. Explain what the recording proves

The recording demonstrates:

- the recovered application runs in a reproducible Linux environment
- role-protected workflows are reachable
- checkout decisions are made server-side
- password reset requires OTP verification
- Mailpit captures email without contacting real recipients
- the database and SMTP services remain disposable and private

It does not prove production payment processing, complete browser coverage,
load-tested inventory concurrency, or production deployment.

## 9. Stop and clean up

When the recording is complete:

1. Stop the application with `Ctrl+C` (the scripted path also stops and
   removes the containers it started).
2. Stop and remove only the named disposable SQL Server and Mailpit resources
   when using the manual path.
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
