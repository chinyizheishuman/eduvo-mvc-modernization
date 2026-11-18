# Modernization notes: what separates this from the original DSE207 module

This document exists so the claims are checkable. The README tells the story in
my voice. This file holds the engineering specifics with file and line
references, so anyone reading the repo can verify each difference in minutes.

Written 18 November 2026 after a live end-to-end session in a fresh Codespace.

## The original module, verified from the 2025 archive

Every line below was read directly from the original DSE207 submission archive.

1. Passwords were compared in plaintext, directly against the database.
   `DSE207/Controllers/AdminController.cs` line 34:
   `db.Admins.FirstOrDefault(a => a.AdminUsername == admin.AdminUsername && a.AdminPassword == admin.AdminPassword)`
2. No antiforgery protection anywhere. No `ValidateAntiforgeryToken` or
   `AutoValidateAntiforgeryToken` appears in any controller.
3. One-time passwords for password recovery were generated with `System.Random`
   in both `UserController.cs` (line 1367) and `SellerController.cs` (line 918).
   `System.Random` is predictable and must never gate account recovery.
4. Checkout totals were trusted from the client. `ProcessPayment` in the
   original `UserController.cs` (line 612) accepts `subtotal`, `gst`,
   `promotion` and `promoCode` as parameters supplied by the browser. The
   line-item price it stored came from the cart row, not from a fresh database
   read: `(c.Price ?? 0) * (c.Quantity ?? 0)`.
5. Session idle timeout was 5 minutes (`Program.cs` line 17), while the demo
   cookie lives longer, so users were logged out mid-task.
6. The solution had a single project: no tests, no CI, no pricing service,
   no upload validation helpers. `Common/` contained only an `Enum` folder.
7. The archive itself shipped compiled `bin/Debug` output, about 26 MB of
   build artifacts checked in next to the source.

## What the modernization changed

Each item points at where to look.

1. Password security. `Common/Security/PasswordSecurity.cs` hashes with
   PBKDF2-SHA256, a random salt per user, and a constant-time comparison.
   Legacy plaintext records are upgraded transparently on first successful
   login. Covered by `Eduvo.Tests/PasswordSecurityTests.cs`.
2. Checkout pricing moved server-side. `Common/Pricing/CheckoutPricing.cs`
   reloads prices, stock and promotions from the database inside the checkout
   flow instead of trusting browser-supplied totals, and the checkout runs in
   a serializable transaction. Covered by `Eduvo.Tests/CheckoutPricingTests.cs`.
3. Request uniqueness at the database level. `CheckoutAttempts` and the
   migration `20260309000000_AddCheckoutAttempt.cs` persist a constraint that
   blocks double-checking the same request.
4. Upload validation. `Common/Security/FileUploadSecurity.cs` checks file
   type and size before an image is stored. Covered by
   `Eduvo.Tests/FileUploadSecurityTests.cs`.
5. Antiforgery everywhere. `[AutoValidateAntiforgeryToken]` sits at class
   level on the Admin, Seller and User controllers.
6. Role-based entry points. `[Authorize(Roles = ...)]` guards the User area,
   with cookie authentication sliding for 30 minutes.
7. Session lifetime aligned with the cookie. `Program.cs` now sets the
   session idle timeout to 30 minutes to match the auth cookie, so users stop
   being logged out mid-task by the old 5-minute session.
8. Cryptographically secure OTP. Both `UserController.cs` and
   `SellerController.cs` now draw one-time passwords from
   `RandomNumberGenerator.GetInt32` instead of `System.Random`.
9. No silent failures on expired sessions. `wwwroot/js/session-guard.js`
   detects the `Unauthorized` JSON reply that data endpoints return after a
   session dies and redirects to the matching login page instead of leaving a
   dashboard frozen at zero. It is wired into the 11 pages that poll
   session-gated data with AJAX.
10. Automated tests and CI. `Eduvo.Tests` (15 focused tests covering password
    hashing, checkout pricing, upload validation and an integration smoke
    pass) plus the GitHub Actions workflow in `.github/workflows/build.yml`.
11. A disposable demo stack. `scripts/start-demo.sh` boots SQL Server 2022
    and Mailpit containers bound to 127.0.0.1, generates a temporary SQL
    password, applies migrations, seeds demo accounts, and tears everything
    down on Ctrl+C. Mailpit captures password-recovery email locally so no
    real SMTP account is needed, which also removes the personal Gmail
    credential the original module carried in its settings.
12. The repository no longer contains build artifacts.

## What was done on 18 November 2026

A live session in a fresh Codespace confirmed the whole stack runs: admin,
seller and shopper flows all respond, the dashboard fills from real database
data, and Mailpit captures mail. During that session:

1. The session and cookie lifetime alignment was confirmed present and working.
2. OTP generation was moved to `RandomNumberGenerator` in both controllers.
3. `session-guard.js` was added and wired into the 11 AJAX pages.
4. The README gained a troubleshooting list for the common demo failures.

## How to verify the claims

From the repository root:

```bash
dotnet build                                    # expect 0 warnings, 0 errors
dotnet test                                     # expect 15 passed
grep -n "System.Random" Controllers/*.cs        # expect no matches
grep -rn "AutoValidateAntiforgeryToken" Controllers/ | head
```

To see the original behavior, open the 2025 archive and compare the lines
cited above.

## Honest limits

Session-backed authorization still exists in recovered workflows; a full move
to ASP.NET Core Identity is future work. Mailpit is a demo tool, not an email
provider. Demo passwords are deliberately simple because the environment is
disposable. The SMTP fields still present in `appsettings.json` should be
rotated and kept empty before the repository ever becomes public. The session
store is in-process, so horizontal scaling would need a distributed cache.
The application is not production-ready, and none of this pretends otherwise.
