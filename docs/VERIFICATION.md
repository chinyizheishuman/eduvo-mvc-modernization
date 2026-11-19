# Geek Lab | Verification record

This is a dated record of **what was checked**, not a blanket security certification. The source is public, but the app checks below used **private, local/loopback demo environments**.

## Evidence at a glance

| Evidence | Scope | Important boundary |
| --- | --- | --- |
| **29 current xUnit cases** | Counted from `[Fact]` and `[Theory]` / `[InlineData]` in the four files under `Eduvo.Tests`; the most recent recorded run reported 29 passed | A source count is not a fresh test execution. HTTP integration tests replace SQL Server with EF Core InMemory. |
| **SQL Server + Chromium** | Earlier sessions exercised Seller stock edits, Admin activation, Shopper catalogue, promotion and order history | These checks ran against the demo state described below, not necessarily the newest checkout build. |
| **Last-unit SQL Server scenario** | Two shoppers competed for one unit: one order completed, the other returned to cart; final stock zero | A recorded focused scenario, not a sustained concurrency/load test. |
| **GitHub Actions** | [Build workflow](../.github/workflows/build.yml) restores, builds and tests the .NET projects on push and PR | Presence of the workflow does not attest that the latest run succeeded. |
| **Visual evidence** | Seven actual desktop/phone PNGs in [screenshots](screenshots) | Screenshots show specific runs; they don't prove untouched workflows or security properties. |

### Why other test counts appear below

**15 → 19 → 23 → 24 → 29** refers to successive test-suite checkpoints during the 18–19 November restoration. The **29-case suite** is the current documented checkpoint, not the sum of these numbers. Older entries remain below so the chronology and their specific browser evidence are not lost.

For a fresh result, run:

```bash
dotnet test Eduvo.Tests/Eduvo.Tests.csproj
```

Do not label that command successful until it has actually completed in your environment.


## Checkout and repository review

| Failure or gap | Cause and change | Evidence |
| --- | --- | --- |
| The order could record a different total from the one the Shopper reviewed. | The review rendered saved cart prices and a stored discount while submission read current prices and promotion eligibility. Checkout now saves the reviewed lines and discount with the session token and rechecks them inside the stock transaction. A change sends the Shopper back to review before any order or stock update. | Three MVC integration cases reproduced the failure and passed after the fix. They cover a changed price, changed quantity, and an expired promotion, with a successful confirmation at the revised total. |
| Seller records could round independently and differ by a cent from checkout. | Round GST and discount once for the order, then allocate whole cents to the existing seller records. The final allocation includes any remaining cent. | The two-seller MVC case reproduced a fractional-cent total and now verifies that the records sum to RM 0.17 with RM 0.01 GST. SQL Server multi-seller rounding was not rerun. |
| Cart quantity and remove failures were silent. | The Cart view now announces server and request errors through its alert region. It shows live listing prices, labels controls, and keeps the remove action available for unavailable items. | The .NET suite built the views and verified the cart and checkout HTTP routes. The updated cart controls still need a fresh browser interaction check. |
| The old Admin product-create URL returned HTTP 500. | The old action asked MVC for a missing Razor view. It now uses the signed-in Admin product page and opens the existing Add Product form. | Chromium reproduced the HTTP 500 before the repair. The new MVC test verifies the fixed route renders the form and token and the normal list still starts with the form closed. The new auto-open behaviour was not rerun in Chromium. |

The suite passed all **29 tests**. A separate read-only Chromium pass used the existing Codespace app on loopback. It confirmed all three sign-ins and loaded Admin product, seller and shopper management, Seller dashboard and inventory, and Shopper catalogue, cart, promotions and order history. That running app used an earlier build. The new checkout behaviour was exercised through the current MVC integration tests.

## Portfolio interface and response hardening

Chromium screenshots under [screenshots](screenshots) were captured on 18 November 2026 from the running MVC app, with a fresh database in the private SQL Server container. Desktop captures are 1,440 × 900 and phone captures are 390 × 844. The demo catalogue reuses product imagery from the existing coursework assets. The earlier README PNGs also matched the previous Chromium harness's raw output byte for byte.

| Failure or gap | Cause and change | Evidence |
| --- | --- | --- |
| Admin search results could lose listing descriptions. On phones, the table hid status and action buttons off-screen. | The Admin search action returned a narrower projection than the listing action. The page now filters the full loaded results by text and status. Phone rows keep the product name, price, stock, status and actions together. | Chromium found the pending sample by its description and displayed that description in the result. The 390-pixel capture shows the status and row actions together. |
| Seller inventory made stock and listing status hard to scan. | Replaced the photo-first grid with inventory rows and counts for shop-visible and pending listings. | In Chromium the Seller changed the pending sample from five to six units. The quantity persisted, and the Shopper saw six units after Admin activation. |
| Catalogue feedback appeared far from the product and faded out. Checkout did little to distinguish the final total from the breakdown. | Added catalogue search, bounded quantity controls, persistent per-product feedback and a checkout summary that puts the total before the order action. | Chromium verified the search empty state, adjusted quantity, added a Steam Deck, applied `DEMO10`, placed the demo order, and confirmed RM 2,399.04 in order history. |
| Authenticated pages and JSON could be stored by caches when an action supplied no cache directive. The application lacked explicit framing and MIME-sniffing headers. | Apply `Cache-Control: no-store` after authentication and add framing, MIME-sniffing and referrer protections. Static assets retain their own caching behavior. | The HTTP integration test checks authenticated HTML and JSON headers and verifies that CSS stays outside the no-store policy. Chromium also confirmed the Admin JSON headers. |

Build completed with zero warnings and zero errors. The .NET suite passed all **24 tests**. This browser pass covered the listing and checkout paths above. Account recovery, SQL Server inventory contention, and accessibility beyond the targeted checks have their own evidence and limits below.

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

Run the regression suite with `dotnet test Eduvo.Tests/Eduvo.Tests.csproj`. The browser checks in this section used a temporary local harness.

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

- The published repository contains a local-only demo, not a public service. Review HTTPS and cookie deployment settings, distributed session and key storage, and database access before considering any public service.
- Verification code limits are per challenge in the current session. Durable rate limiting, stricter account recovery controls, upload decoding and scanning, and dependency pinning remain open.
- Several unlinked legacy Admin edit, delete, detail, and status routes still reference absent Razor views. The supported Admin flows use the modals and AJAX actions on the management pages. Broad concurrent load and full accessibility remain unverified. The interface review used Chromium at desktop and phone widths with targeted keyboard interaction.
- NuGet reported no known advisories for the application’s direct or transitive packages from its configured source. Vendored jQuery Validation 1.19.5 is covered by an upstream advisory. The current Razor views have no references to that script, so the version match alone does not establish a reachable issue. [GitHub advisory](https://github.com/advisories/GHSA-rrj2-ph5q-jxw2)
- The recording pass stopped before it produced a complete video. The screenshots remain the portfolio evidence.

### Recording storyboard

The [demo guide](DEMO.md#record-the-walkthrough) lists the route, action, and timing for each shot. The [screenshot index](DEMO.md#screenshot-locations) links to the browser captures. The Shopper order-history capture shows the same SQL Server demo order at RM 2,399.04 and explicitly states that no payment was collected. The old receipt template displayed a generic payment confirmation even for simulated orders.

The read-only demo check passed against the running app and Mailpit. The launcher refused to start against the occupied SQL port, and the existing Docker container IDs matched before and after. Shell checks with stubbed commands covered cleanup on normal exit and after Mailpit startup failure. A later isolated run used alternate loopback ports in the same Linux Codespace. The launcher created the disposable services, applied all four migrations, and passed `check-demo.sh`. The test launcher and its listeners were closed afterward. A default-port rerun was avoided because the existing demo still owned those ports.
