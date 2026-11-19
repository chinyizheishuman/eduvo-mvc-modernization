# Geek Lab demo

## Start and check the app

From the repository root in a fresh private Codespace or local Bash environment with Docker and the .NET SDK/runtime described in the [README](../README.md):

```bash
./scripts/start-demo.sh
```

The script starts disposable SQL Server and Mailpit services, applies the migrations, seeds sample listings and starts the MVC app. Use the throwaway sign-in details from the startup terminal. Keep ports **5067** and **8025** private if you forward them in Codespaces. SQL Server and SMTP stay on loopback.

In another terminal, check the running demo before opening the browser:

```bash
./scripts/check-demo.sh
```

For a nondefault setup, pass the app port first and the Mailpit web port second. The check makes signed-out HTTP requests to the role pages, tests their sign-in redirects, and checks the shared stylesheet and Mailpit API. The browser walkthrough below covers signed-in workflows and database changes.

Closing the launcher stops the disposable containers it started for that run. If the SQL port is occupied by a differently named demo, use free loopback ports and keep the existing demo running:

```bash
EDUVO_APP_PORT=5072 EDUVO_SQL_PORT=1434 EDUVO_SMTP_PORT=1026 EDUVO_MAILPIT_PORT=8026 ./scripts/start-demo.sh
# In a second terminal
./scripts/check-demo.sh 5072 8026
```

The launcher also reserves the container names `eduvo-sql` and `eduvo-mailpit`. If those names already belong to a demo, use that instance or a fresh workspace.

## Record the walkthrough

**No spoken narration required.** For a short recruiter-facing cut, capture real screen activity and add only a few captions:

| Approx. clip | Show | On-screen caption |
| --- | --- | --- |
| 0:00–0:15 | Seller inventory edit | "An old coursework app. Restored and tested." |
| 0:15–0:30 | Admin activates the pending Demo Laptop | "Pending → Active" |
| 0:30–0:50 | Shopper catalogue, Steam Deck cart and promotion | "Three roles, one listing state change; checkout is a separate demo." |
| 0:50–1:10 | Test terminal and order history | "Tests and real database checks, with documented limits." |

Record **actual footage**, not fabricated test output; skip or revise any shot that fails. The longer 2:30 storyboard below gives room for an interview walkthrough. A completed recording has **not** yet been published.


| Time | Location | What to show |
| --- | --- | --- |
| 0:00–0:20 | Shopper catalogue · `/User/Products` | Introduce the 2025 DSE207 project and why you returned to it. Start with the real shop on screen. |
| 0:20–0:50 | Seller inventory · `/Seller/ManageProducts` | Open the pending Demo Laptop listing and set stock from five to six. Return to the list and show the saved quantity and Pending status. |
| 0:50–1:15 | Admin product management · `/Admin/Index` | Filter to Pending and activate the listing. Switch to the Shopper catalogue, search for Demo Laptop and show six in stock. |
| 1:15–1:55 | Shopper cart, checkout and order history · `/User/Cart`, `/User/Checkout`, `/User/TransactionHistory` | Add one Steam Deck to an empty cart. Apply `DEMO10`, point to RM 2,399.04, place the demo order and find the same amount in order history. Use a fresh demo for a retake after consuming the promotion. |
| 1:55–2:30 | Test terminal and browser | Run the regression suite. Explain the last-unit check in two sentences, then close on one improvement you want to make next. |

For an optional recovery clip, open `/User/ForgotPassword` and use a throwaway account with Mailpit on port **8025**. Pause capture during sign-in and when entering verification codes. Keep private URLs and inbox details out of the recording. Save the draft locally as `geek-lab-walkthrough.mp4` and review it before adding a video link to the README.

## Screenshot locations

Capture the catalogue, Admin and Seller views before activating the pending demo listing. Capture checkout after applying the promotion and order history after placing the order. Current captures use Chromium at **1440 × 900** desktop and **390 × 844** phone widths.

| Frame | Repository path |
| --- | --- |
| Shopper catalogue | [`docs/screenshots/shopper-catalogue.png`](screenshots/shopper-catalogue.png) |
| Seller inventory | [`docs/screenshots/seller-products.png`](screenshots/seller-products.png) |
| Admin product management | [`docs/screenshots/admin-products.png`](screenshots/admin-products.png) · [Phone](screenshots/admin-mobile.png) |
| Checkout with `DEMO10` applied | [`docs/screenshots/shopper-checkout.png`](screenshots/shopper-checkout.png) · [Phone](screenshots/shopper-mobile.png) |
| Recorded demo order | [`docs/screenshots/shopper-order-history.png`](screenshots/shopper-order-history.png) |
