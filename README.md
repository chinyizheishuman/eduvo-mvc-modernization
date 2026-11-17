# eduvo-mvc-modernization

Private staging repository for restoring and modernizing original Eduvo DSE207
ASP.NET Core MVC coursework.

## Current status

| Area | Verified status |
|---|---|
| Target framework | `net8.0` |
| Available SDK | .NET SDK `10.0.403` |
| Restore | Succeeds |
| Build | Succeeds |
| Compiler warnings | `0` |
| Compiler errors | `0` |
| Vulnerable NuGet packages | None reported by the configured source |
| Application execution | Not run |
| Database access | Not attempted |
| Git commit, push, publish, and deploy | Not performed |

This repository contains recovered coursework. The modernization work is kept
small and traceable so the original project identity and provenance remain clear.

## Scope and evidence rules

The audit and verification were based on repository files and local commands.
The application was not started. No external database was contacted. Secret
values are not reproduced in this document. No claim is made about behavior that
was not visible in source or verified by a safe local command.

The original source remains in place. Changes are limited to security hardening,
warning cleanup, dependency updates, and this documentation.

## Step 1: Repository inventory

### Project and framework

- Project file: `JQuery-ECommerceAss.csproj`
- SDK-style ASP.NET Core web project
- Target framework: `net8.0`
- Nullable reference types enabled
- Implicit usings enabled
- No `global.json` was found
- Root namespace: `JQuery_ECommerceAss`

### Dependencies

Current direct package references in `JQuery-ECommerceAss.csproj`:

- MailKit `5.0.0`
- MimeKit `5.0.0`
- Microsoft.EntityFrameworkCore `9.0.11`
- Microsoft.EntityFrameworkCore.SqlServer `9.0.11`
- Microsoft.EntityFrameworkCore.Tools `9.0.11`

### Application areas

- `Controllers/`
  - `AdminController.cs`
  - `SellerController.cs`
  - `UserController.cs`
  - `HomeController.cs`
- `Models/`
  - administrators, sellers, users, products, carts, promotions,
    user promotions, transactions, and the error view model
- `Data/AppDbContext.cs`
- `Migrations/`
  - two initial migrations and the EF model snapshot
- `Views/`
  - administrator, seller, user, home, and shared Razor views
- `wwwroot/`
  - CSS, JavaScript, local library assets, product images, and favicon
- `Common/Enum/Status.cs`
- `Common/Security/PasswordSecurity.cs`

### External services and local configuration

- SQL Server is configured through the connection string in `appsettings.json`
  and points to local `localhost\SQLEXPRESS`.
- Email uses Gmail SMTP through MailKit.
- SMTP host, port, sender, username, and password are configuration keys.
  Credentials must be supplied through local secrets or environment configuration.
- `JQuery-ECommerceAss.csproj.user` contains IDE scaffolding metadata only.
- `Properties/launchSettings.json` contains development launch URLs.
- `jem.py` is a filesystem-listing utility.

No student-specific records were found during the inventory.

## Step 2: Architecture and implemented workflows

### Application startup

`Program.cs` registers MVC, `AppDbContext` with SQL Server, and five-minute
session state. The pipeline enables static files, routing, session, and
authorization middleware. ASP.NET Core Identity and policy-based role
authorization are not registered. The default route opens `Admin/Login`.

### Administrator management

`Controllers/AdminController.cs` contains:

- Administrator login and session creation in `Login`.
- Product status management in `ChangeStatus`.
- User listing, editing, status changes, and deletion.
- Seller listing, editing, status changes, and deletion.
- AJAX endpoints for product, user, seller, and dashboard operations.

The existing access model is manual session checking through
`IsAdminLoggedIn()`. It is not ASP.NET Core role authorization.

### Seller product management

`Controllers/SellerController.cs` contains:

- Seller registration, OTP verification, login, suspension, and password reset.
- Dashboard and product listing filtered by the logged-in seller ID.
- Product creation, editing, deletion, image upload, and status reset to pending.
- Customer insight calculations.
- AJAX versions of product operations.

Seller product operations check seller ownership in the recovered workflow.
Validation and antiforgery protection were not consistently present.

### Shopper catalogue and cart

`Controllers/UserController.cs` contains:

- Catalogue filtering to active or approved products with stock.
- Cart display, add, remove, and quantity update operations.
- Basic stock checks when adding or updating cart items.
- Session-based user login and profile operations.

The cart stores a copied price. Checkout still requires a server-side pricing
redesign because client-submitted totals remain part of the current workflow.

### Promotions and pricing

`UserController.cs` contains promotion assignment, listing, application, removal,
minimum purchase checks, percentage discounts, and maximum discount limits.

The recovered source also contains `TestPromotions` and
`CreateTestPromotions`, which are diagnostic actions without the same access
checks used by normal user operations.

### Checkout and transaction history

`UserController.cs` contains checkout display, payment processing, seller-grouped
transaction creation, inventory decrement, promotion consumption, cart removal,
confirmation email, and transaction history.

The current checkout still needs a later focused improvement for authoritative
server-side totals, database transaction boundaries, concurrency handling, and
duplicate request protection.

## Step 3: Original audit findings

These findings describe the recovered state before the remediation work.

| # | Severity | Evidence | Finding | Confidence |
|---|---|---|---|---|
| 1 | Critical | `Controllers/UserController.cs`, `Controllers/SellerController.cs` | SMTP credentials were hardcoded in controller source. | 10/10 |
| 2 | Critical | `Models/User.cs`, `Models/Seller.cs`, `Models/Admin.cs`, `Models/TransactionHis.cs` | Passwords were stored and compared as plaintext, and checkout persisted submitted card numbers. | 10/10 |
| 3 | High | `Controllers/UserController.cs`, `Views/User/Checkout.cshtml` | Checkout accepted client-controlled subtotal, GST, and promotion values. | 10/10 |
| 4 | High | `Controllers/UserController.cs` | Checkout had no explicit transaction, concurrency control, or idempotency key. | 9/10 |
| 5 | High | `Controllers/SellerController.cs` | Seller upload paths used client filenames without equivalent basename sanitization. | 9/10 |
| 6 | High | `Controllers/UserController.cs` | Diagnostic promotion actions were publicly routable without an apparent login or role check. | 9/10 |

Other verified observations from the recovered source:

- Most state-changing AJAX endpoints did not show antiforgery validation.
- Models had limited server-side validation attributes.
- Custom session checks were used instead of framework authentication and roles.
- The initial migration contains a card-number column.
- Local SQL Server configuration is development-specific and contains no database
  password in the checked file.

## Step 4: Applied changes

### 4.1 Password protection

Added `Common/Security/PasswordSecurity.cs`.

Implementation decisions:

1. PBKDF2-SHA256 is used with a random per-password salt.
2. The stored format includes the algorithm marker, iteration count, salt, and hash.
3. Hash comparison uses constant-time comparison.
4. New registrations use hashes.
5. Password changes use hashes.
6. Existing legacy plaintext records can authenticate once and are upgraded to
   hashes after successful login.
7. Invalid hash formats fail closed.

This preserves access to recovered coursework data while moving credentials
toward a safer representation. A future migration should add explicit password
format documentation and complete the transition to framework authentication.

Updated files:

- `Controllers/AdminController.cs`
- `Controllers/SellerController.cs`
- `Controllers/UserController.cs`
- `Common/Security/PasswordSecurity.cs`

### 4.2 SMTP secret removal

Removed literal SMTP credentials from both controllers. Email now reads:

- `Smtp:Host`
- `Smtp:Port`
- `Smtp:From`
- `Smtp:Username`
- `Smtp:Password`

Missing or blank required values throw an explicit configuration error instead
of silently using an unsafe fallback. The actual values are not stored in this
repository.

MailKit 5 changed the send API. The controllers now construct
`SmtpSendRequest` explicitly.

Updated files:

- `Controllers/UserController.cs`
- `Controllers/SellerController.cs`
- `appsettings.json`

### 4.3 Card-number removal

Removed `CardNumber` from the transaction model, checkout transaction creation,
and transaction-history display. The history view now reports that payment was
processed securely without displaying card fragments.

The historical EF migration still contains the old database column. This is
intentional provenance preservation. A future database migration must remove or
retire that column before any real database deployment.

Updated files:

- `Models/TransactionHis.cs`
- `Controllers/UserController.cs`
- `Views/User/TransactionHistory.cshtml`

### 4.4 Warning and nullable-flow cleanup

Resolved the compiler warnings by making narrow, behavior-preserving changes:

- Guarded nullable session values before storing or using them.
- Checked deserialization results before registration completion.
- Handled missing cart products.
- Handled missing sellers before seller dashboard access.
- Checked nullable promotion navigation data.
- Used null-safe date access after filtering.
- Removed unused exception variables where the existing response already handled
  the error.

The cleanup did not redesign the application or suppress warnings globally.

Updated files:

- `Controllers/AdminController.cs`
- `Controllers/SellerController.cs`
- `Controllers/UserController.cs`
- `Models/TransactionHis.cs`

### 4.5 Dependency update

MailKit and MimeKit were upgraded from the recovered 4.x versions to `5.0.0`.
The necessary MailKit 5 send-request API change was applied.

This removed the previously reported package advisories from the current
configured NuGet source.

Updated file:

- `JQuery-ECommerceAss.csproj`

## Step 5: Verification record

The application was not run and no database was contacted.

### Environment check

```text
dotnet --info
```

Observed SDK:

```text
10.0.403
```

### Restore

```text
dotnet restore JQuery-ECommerceAss.csproj --packages /tmp/eduvo-nuget
```

Result: restore succeeded.

### Build

```text
dotnet build JQuery-ECommerceAss.csproj --no-restore
```

Result:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

The same result was verified with:

```text
dotnet build JQuery-ECommerceAss.csproj
```

### Package vulnerability check

```text
dotnet list JQuery-ECommerceAss.csproj package --vulnerable --include-transitive
```

Result:

```text
The given project JQuery-ECommerceAss has no vulnerable packages given the current sources.
```

### Diff hygiene

`git diff --check` completed without whitespace errors. Generated `bin/` and
`obj/` directories are build outputs and are not source changes.

## Step 6: Current limitations and next work

The clean build is not equivalent to a complete security review. The following
items remain for later, separately testable work:

1. Recalculate checkout totals from database state and ignore hidden form totals.
2. Add an explicit checkout transaction, stock concurrency control, and request
   idempotency.
3. Add antiforgery protection to state-changing MVC and AJAX actions.
4. Replace manual session authentication with framework authentication and roles.
5. Remove or restrict diagnostic promotion actions.
6. Validate upload extension, content, size, generated names, and canonical path.
7. Add a migration to retire the historical card-number database column.
8. Add automated tests for password hashing, checkout calculation, authorization,
   and promotion eligibility.
9. Add Codespaces and CI instructions after a safe local test strategy exists.

## Step 7: Reproducible recovery commands

Use these commands from the repository root:

```text
dotnet restore JQuery-ECommerceAss.csproj --packages /tmp/eduvo-nuget
dotnet build JQuery-ECommerceAss.csproj --no-restore
dotnet list JQuery-ECommerceAss.csproj package --vulnerable --include-transitive
```

If the IDE turns the project red after generated files are cleaned, restore first.
The restore recreates `obj/project.assets.json`, which the build requires:

```text
dotnet restore JQuery-ECommerceAss.csproj
dotnet build JQuery-ECommerceAss.csproj --no-restore
```

Do not run the application or apply migrations until the SQL Server configuration,
SMTP configuration, authentication design, and checkout integrity work have been
reviewed.

## Handoff

The latest existing commit message was verified without a
`Co-authored-by: Copilot` trailer. This README handoff note is an uncommitted
documentation update. No history rewrite is required.

## Current deep review

Review date: 2026-11-17. This section records the latest read-only review after
the modernization commit. The project builds cleanly, but build success does not
prove that authentication, authorization, money, inventory, or file handling are
safe at runtime.

### Critical findings still open

| Priority | Evidence | What is wrong | Why it matters | Required fix |
|---|---|---|---|---|
| Critical | `Controllers/UserController.cs` reset flow around lines 350-403; `Controllers/SellerController.cs` around lines 248-308 | Password reset stores the requested email in session before OTP verification. The change-password action authorizes using that email without requiring an OTP-success marker. | An attacker can request a reset for another account and set a new password without knowing the OTP. | Use a single-use, short-lived, server-side reset token created only after verified OTP, or store the email only after successful OTP verification. |
| Critical | `Controllers/UserController.cs` `ProcessPayment` around lines 628-728; `Views/User/Checkout.cshtml` payment form | `subtotal`, `gst`, and `promotion` are accepted from hidden browser fields and used in transaction amounts, seller allocation, and email totals. | An authenticated shopper can alter totals and create falsified financial records. No trusted payment verification exists. | Recalculate all totals from current database prices, quantities, and validated promotion state. Add a trusted payment provider and atomic order commit. |

### High and medium findings still open

| Severity | Evidence | Finding | Why it matters | Required fix |
|---|---|---|---|---|
| High | `Controllers/UserController.cs` diagnostic promotion actions around lines 862-1052 | Public actions can inspect another user, assign promotions, and create test promotions. | Unauthenticated users can read or mutate business data. | Remove diagnostic actions or restrict them to development and administrator authorization with antiforgery protection. |
| High | `Controllers/SellerController.cs` product upload actions around lines 375-400 and 776-800; `Controllers/AdminController.cs` upload actions around lines 709-804 | Uploads accept arbitrary file content into the public `wwwroot` tree. Client-side `accept="image/*"` is not server validation. | HTML, SVG, or script content can become publicly served under the application origin. | Validate size, extension, MIME, and file signature. Generate controlled names and store files outside the public tree or serve through a constrained image endpoint. |
| Medium | `Controllers/SellerController.cs` privileged actions around lines 815-920 | Seller checks only that a login session exists. A seller suspended after login can retain access until session expiry. | Account suspension is not enforced consistently. | Resolve the seller on each privileged request, require active or approved status, and clear sessions after suspension. |

### Remediation status

| Area | Status |
|---|---|
| Hardcoded SMTP credentials | Fixed in the committed baseline |
| New password hashing | Implemented with PBKDF2-SHA256 |
| Legacy password transition | Temporary compatibility remains; upgrade occurs after successful login |
| Card-number persistence in current model and checkout code | Removed |
| Historical card-number migration column | Still present and requires a future migration |
| Password reset OTP authorization | Still open |
| Server-side checkout totals | Still open |
| Checkout transaction and idempotency | Still open |
| Diagnostic promotion access control | Still open |
| Upload validation | Still open |
| Framework authorization and antiforgery coverage | Still open |

### Why the clean build is not enough

The compiler verifies syntax, types, Razor compilation, and package resolution.
It cannot verify that a browser has not changed a hidden price field, that an OTP
was actually checked before a password reset, that two checkout requests cannot
spend the same stock, or that an uploaded file is safe to serve. These are
runtime security and business-integrity properties and require focused tests and
design changes.

### Latest verification

The following read-only checks were run after the deep review:

```text
dotnet build JQuery-ECommerceAss.csproj
Build succeeded. 0 Warning(s), 0 Error(s)

dotnet list JQuery-ECommerceAss.csproj package --vulnerable --include-transitive
The given project JQuery-ECommerceAss has no vulnerable packages given the current sources.
```

The application was not run. No database or external service was contacted. The
latest commit remains the user's commit, and the README changes in this section
remain uncommitted until explicitly reviewed.

The current worktree also shows generated `bin/` and `obj/` files as modified
because those directories are tracked by the existing commit. This is a source
hygiene problem: builds should generate ignored artifacts, not modify tracked
dependencies, binaries, symbols, and intermediate files. Remove them from
version control in a separate, explicitly reviewed cleanup commit. Do not mix
that cleanup with application security changes.

## Latest implementation update

The two highest-impact open flaws from the deep review were implemented after
the review.

### Password reset now requires verified OTP

User and seller reset flows now set a short-lived verification timestamp only
after the OTP is accepted. Both the reset page and the password-change POST
require that timestamp and the reset email. The timestamp expires after ten
minutes and is removed after a successful password change.

This closes the previous session-only authorization gap. Knowing or setting an
email session value is no longer sufficient to change a password.

### Checkout totals are now server-authoritative

The checkout POST still accepts the historical form signature for compatibility,
but it no longer trusts submitted subtotal, GST, promotion, or promo-code values.
It now:

1. Reloads each product from the database.
2. Rejects missing products, missing sellers, invalid quantities, and insufficient
   stock.
3. Uses current database product prices for the subtotal.
4. Calculates GST on the server.
5. Reloads the selected promotion for the logged-in user.
6. Revalidates promotion status, dates, usage state, and minimum purchase.
7. Applies the promotion cap on the server.
8. Allocates seller totals from authoritative values.
9. Uses authoritative totals for transaction records and email totals.
10. Removes browser-controlled total fields from the checkout form and tells the
    user that totals are recalculated securely.

This is a meaningful full-stack improvement because the browser is now a display
client, not the authority for money or discount values.

Verification after this update:

```text
dotnet build JQuery-ECommerceAss.csproj
Build succeeded. 0 Warning(s), 0 Error(s)
```

The application and database were not run. These changes remain uncommitted.

## Comprehensive hardening update

The follow-up hardening pass addressed the remaining reported application
security issues without rewriting the recovered coursework:

- Diagnostic promotion endpoints are marked non-actionable and are no longer
  reachable as MVC routes.
- Seller session checks now verify the seller still exists and is active or
  approved, so a suspended seller cannot continue using seller workflows.
- Seller and administrator product uploads now require a JPG, JPEG, PNG, or
  WEBP signature and a maximum size of 5 MB. Stored filenames are generated
  server-side and never include the client filename.
- MVC controllers now use automatic antiforgery validation for state-changing
  requests. The checkout form includes its antiforgery token explicitly.
- Checkout now uses a short-lived server-issued request token, an explicit
  EF Core database transaction, and commits database changes before sending
  confirmation email. Submitted money values remain ignored.
- A new migration removes the historical `CardNumber` column from deployed
  databases. The original migration remains unchanged to preserve provenance.

The checkout token reduces replay of a completed checkout from the same session.
The next production-grade step would be a persisted order aggregate with a
database uniqueness constraint and concurrency token for multi-instance,
concurrent checkout requests. The application still uses custom session
authorization rather than ASP.NET Core Identity, which remains a modernization
option rather than a claim about the original coursework.

Verification:

```text
dotnet build JQuery-ECommerceAss.csproj
Build succeeded. 0 Warning(s), 0 Error(s)

git -c core.whitespace=cr-at-eol diff --check
Passed
```

The application was not run and no database or external service was contacted.
No commit was created.

## Commit readiness

The modernization is ready for a source commit when the following source-only
files are explicitly staged:

- Controllers and views
- Password and upload security helpers
- The card-number cleanup migration
- EF Core migration snapshot
- README and `.gitignore`

Generated `bin/` and `obj/` artifacts should not be included. They are build
outputs, are already present in this repository from historical coursework,
and remain separately tracked until an explicit repository-hygiene commit
removes them from Git index control.

The verified source state is:

- `dotnet build JQuery-ECommerceAss.csproj`: 0 warnings, 0 errors
- Password resets require verified OTP state
- Checkout totals are recalculated on the server
- Checkout uses a session request token and EF Core transaction
- Diagnostic promotion routes are non-actionable
- Upload signatures and sizes are validated
- Suspended sellers cannot retain seller session access
- Antiforgery validation is enabled for MVC controllers
- The historical card column has a cleanup migration
