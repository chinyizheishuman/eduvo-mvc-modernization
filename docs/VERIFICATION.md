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
