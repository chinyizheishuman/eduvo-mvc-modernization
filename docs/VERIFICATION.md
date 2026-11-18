# Eduvo MVC verification

## 18 November 2026

I verified the recovery in the Linux GitHub Codespace, with Chromium running against ASP.NET Core, SQL Server 2022, and Mailpit on loopback. The SQL Server and Mailpit containers were created for this check. All four repository migrations applied to the disposable database.

| Failure | Cause and repair | Evidence |
| --- | --- | --- |
| Seller Add Product and Shopper Checkout returned HTTP 500 | Commit `e53002b` removed Razor views still returned by their controllers. The original workflow views were recovered from `1120f38`, including the related product, account recovery, promotion, and order history pages. | The original suite reproduced both 500s. The repaired suite passed. Chromium completed seller product management and shopper checkout against SQL Server. |
| Admin status changes and other AJAX mutations could return HTTP 400 | Several pages rendered a token but their jQuery requests omitted it. The shared partial now attaches the verification header to same-origin, state-changing jQuery requests. Admin status and delete requests also display HTTP failures. | Chromium changed all three statuses and displayed the updated values. The HTTP integration tests verified that status changes persist and that requests without a token are rejected on the Seller create and Shopper add-to-cart paths. |
| Seller Management displayed an approved seller as Unknown | `ManageSeller.cshtml` omitted the numeric `Approve` enum value from its badge map and edit options. It now displays Approved and treats that state as enabled for the toggle. | Chromium displayed Approved for the seeded seller and deactivated and reactivated the account. |
| Admin product image selection could upload duplicates and retain removed previews | `Index.cshtml` appended files already present in `FormData`. The preview remove button now updates the submitted selection. The Admin create action also creates the image directory when needed. The custom modal uses one set of handlers. | Chromium removed one of two selected test images, created a product with one image, edited its stock, and deleted the test listing. |

### Workflow checks

- Admin signed in, listed products, created, edited and deleted a product, and toggled product, shopper and seller statuses.
- Seller signed in, loaded the dashboard and Manage Products, and created, edited and deleted a test listing. The inventory edit appeared in the list.
- Shopper signed in, added a product to the cart, checked out, then opened order history. A new shopper registered and completed password recovery using codes delivered to Mailpit. The replacement test password worked.
- All three signed-in roles lost access to protected pages on logout.
- The 19-test .NET suite passed. The automated shopper flow verifies an applied promotion, the recorded total, stock decrement, cart clearing, and one transaction after a repeat submission. These tests use EF Core’s in-memory provider. SQL Server concurrency is a separate verification item.

Run the regression suite with `dotnet test Eduvo.Tests/Eduvo.Tests.csproj`. The browser checks above were run with a temporary local harness. No screenshots or recordings are included.

## Follow-up repairs and defensive review

| Impact and evidence | Change | Verification |
| --- | --- | --- |
| High. Admin account creation persisted the submitted password directly. Legacy Admin edit actions also saved whole bound account records. | Reuse the existing salted password hash for newly created accounts and update explicit fields in the legacy actions. | The fixture verifies both new account types against the password verifier and confirms that each hash has a different salt. Existing account credentials were not migrated or rewritten. |
| High. Admin and Seller AJAX views interpolated stored text into HTML. | Encode displayed text and image attributes, remove a product name from an inline JavaScript argument, and encode dynamic text in confirmation email. | Chromium rendered a test product name containing angle brackets, quotes and an ampersand as text through the Admin product workflow. |
| High. Product create actions could accept an image path from form data. Image cleanup used the stored path before the database update. | Only upload code sets a new image path. Cleanup runs after a successful database update and only for supported image files in the product upload directory. WEBP uploads now require the container type as well as the RIFF signature. | The suite verifies signature and path handling. Chromium uploaded, displayed and removed a disposable product image through Admin product management. |
| High. Checkout loaded tracked stock before its transaction and could subtract the combined quantity more than once when a cart had multiple rows for one product. | Read cart and stock inside the serializable transaction, validate and decrement per product, and return the shopper to the cart on a database conflict. The demo checkout now collects only the request identifier and antiforgery token. | The automated checkout uses two cart rows for one product and verifies one transaction, the calculated total, promotion use, the exact stock decrement, and no second transaction on replay. In SQL Server, two shoppers competed for one unit. One order completed, the other shopper returned to the cart, and stock reached zero. |
| Medium. Logout changed session state on a GET, protected role pages shared the Admin login challenge, and the Shopper checks did not recheck account status. | Use antiforgery-protected POST logout forms, route each challenge to its own sign-in page, and match the role cookie to the session identity. Recheck that the shopper account is enabled. | Chromium signed in and out of each role and reached the correct sign-in page afterward. |
| Medium. Seller and Shopper verification codes shared session keys and used the session lifetime as their only expiry. | Separate the challenges and reset state by role. A challenge expires after 10 minutes, allows five attempts, and is consumed on successful verification. | Browser registration and password recovery succeeded with Mailpit, including sign-in with the replacement test password. Concurrent verification and rate limits across sessions remain open. |
| Medium. Some AJAX errors included raw exception messages. | Return a short user-facing message and log the exception on the server. | The updated application compiles and the tested AJAX workflows still report successful operations. Exception branches were reviewed in source. |
| Functional. Promotion assignment after sign-in queued inserts without saving them. | Persist assignments when the eligibility query finds promotions. | The integration test signs in, finds the automatically assigned promotion, and checks the recorded discounted order. Chromium also applied the seeded promotion against SQL Server and displayed RM 2,399.04 before completing checkout. |
| Usability. The Admin toolbar clipped at phone width and the sidebar had no control to reopen it. The Shopper order button appeared before the promotion and total on phones. | Add an accessible Admin menu button and a constrained, scrollable results region. Put the demo order action after the order summary and promotion. | Chromium confirmed the Admin toolbar fit at 390 pixels and verified keyboard dismissal of the open menu. Desktop screenshots and the Admin phone view are captured under [screenshots](screenshots). |

The follow-up suite passed all 23 tests. The final browser checks used a separately migrated SQL Server database in the existing Codespace container and reused the loopback Mailpit instance.

The source review found EF Core LINQ queries throughout the reviewed request paths, with no raw SQL construction in the controllers. The NuGet audit of the application’s direct and transitive packages reported no advisories from the configured source on 18 November 2026. That result covers NuGet packages. The separately vendored and CDN browser dependencies still need their own inventory and update cycle. [NuGet vulnerability data](https://api.nuget.org/v3/index.json) · [jQuery security support policy](https://github.com/jquery/jquery/blob/main/SECURITY.md)

### Remaining work

- This is a private demo. Review HTTPS and cookie deployment settings, distributed session and key storage, and database access before considering any public service.
- Verification code limits are per challenge in the current session. Durable rate limiting, stricter account recovery controls, upload decoding and scanning, and dependency pinning remain open.
- Broad concurrent load, every Admin AJAX failure branch, the unused legacy Admin CRUD URLs, and a full accessibility audit remain unverified. The interface review used Chromium at desktop and phone widths with targeted keyboard interaction.
- The read-only audit and test results support the observed flows. No portfolio video has been recorded.

### Recording storyboard

Use a private Codespace and a clean disposable demo database with fictional accounts and product images. Rehearse before recording, and open Mailpit from the private Ports panel.

| Segment | Show on screen |
| --- | --- |
| 0:00–0:20 | Introduce the 2025 DSE207 project and why it was revisited. Show the three roles and the original MVC structure. |
| 0:20–0:55 | Sign in as Admin, open a listing, change its status, and show the refreshed row. Show the Seller Management status label. |
| 0:55–1:30 | Sign in as Seller, create or edit a sample listing, and show the saved stock in Manage Products. |
| 1:30–2:15 | As Shopper, add the listing to the cart, apply an assigned promotion, place a demo order, and show its transaction in order history. |
| 2:15–2:45 | Use the password recovery form and Mailpit on a throwaway account. Pause or crop while entering the code and password. |
| 2:45–3:15 | Show the test command and verified result. Close with the difference between the private coursework demo and a service ready for public use. |

Capture the Admin product list, Seller product cards and Shopper checkout using sample data. Keep repository settings, Codespace URLs, passwords, verification codes, and any real email addresses out of the recording.
