# FULL APPLICATION AUDIT REPORT — CANTEENHUB
**Universal Multi-Tenant Canteen Management System**
**Date of Audit:** October 28, 2026  
**Auditor:** Senior Software Architect, Technical Lead, Security Auditor, QA Lead  
**Audit Target Repository:** `D:\CanteenManagementSystem`  
**Git Branch:** `feature/multi-canteen-management` (Commit: `7711ba3`)  
**Audit Standard:** Skeptical, Evidence-Driven Architectural & Security Inspection  

---

## 1. EXECUTIVE SUMMARY

An exhaustive, evidence-based technical audit was conducted on the **CanteenHub** application across its ASP.NET Core backend (.NET 10), Angular 21 frontend, PostgreSQL/EF Core database layer, SignalR real-time messaging pipeline, Razorpay payment processing subsystem, and multi-tenant security architecture.

### Summary Verdict
The application demonstrates **strong engineering in its core backend foundation**:
1. **Solid Architecture in Backend Core:** A clean 4-tier separation (`CanteenManagementSystem`, `Canteen.BAL`, `Canteen.DAL`, `Canteen.Model`), robust DTO modeling, password hashing (PBKDF2-HMAC-SHA256 with 100,000 iterations), cryptographic token generation, and composite foreign key database constraints that physically prevent cross-tenant product-category and order-table poisoning.
2. **Comprehensive Backend Test Suite:** The backend test suite in `tests/Canteen.Tests` is robust and passed completely: **78 tests executed, 78 passed, 0 failed, 0 skipped**.
3. **Production Build Success:** The Angular 21 frontend compiles cleanly (`npm run build` completed with zero TypeScript or bundling errors, generating optimized bundles in `dist/canteen-ui`). Frontend unit tests run via `ng test --no-watch` execute 3 unit tests with 100% pass rate.

However, the audit identified **four critical architectural and business gaps that prevent production readiness**:
1. **Critical Feature Discrepancy ("Phantom Modules"):** While the sidebar UI displays navigation options for *Tables & QR Codes*, *Orders*, *Kitchen Display System (KDS)*, *Payments & Settlements*, *Reports & Analytics*, *Staff Management*, and *Inventory*, **these frontend screens do not exist**. Their sidebar links are either inert, point back to `/dashboard`, or open unrelated modals.
2. **Missing Customer-Facing Ordering Web App:** The backend includes a complete public QR ordering engine (`TablesController`, `OrdersController`, and `QrCodeService` generating URLs like `http://localhost:4200/menu?canteenId=X&tableId=Y&token=Z`), but **the Angular frontend has no `/menu` route or component**. When a customer scans a table QR code, the browser hits the Angular catch-all route (`** -> /login`). Customers currently cannot browse the menu, add items to a cart, or place orders from the web interface.
3. **Public Operational Data Exposure (Information Leakage):** The endpoint `GET /api/canteens` is completely unauthenticated and exposes all canteens in the system, including internal `OwnerId`s, phone numbers, contact emails, and multi-megabyte base64 images.
4. **Database Transaction & Stock Concurrency Defect:** Although `ProductRepository.DeductStockAsync` executes atomic row-level SQL updates, multi-item order placement in `OrderService.CreateAsync` does not run inside an `IDbContextTransaction`. If the server crashes or fails during multi-item orders, stock allocations are vulnerable to inconsistent states. Furthermore, large Base64-encoded image payloads (up to 5MB) are written directly into PostgreSQL columns, risking severe database I/O degradation.

---

## 2. SCOPE AND AUDIT LIMITATIONS

### Inspected Modules & Artifacts
- **Backend Projects:**
  - `CanteenManagementSystem` (API, Controllers, SignalR Hubs, Notification Services, Program.cs)
  - `Canteen.BAL` (Business Logic Layer, Interfaces, Domain Services, Custom Exceptions)
  - `Canteen.DAL` (Data Access Layer, EF Core DbContext, Repositories, Migrations)
  - `Canteen.Model` (Domain Entities, DTOs)
  - `tests/Canteen.Tests` (Integration, Security, Multi-Canteen, Concurrency, and Payment test suites)
- **Frontend Workspace:**
  - `CanteenFrontend/canteen-ui` (Angular 21 Standalone Components, Signals, Reactive Forms, Services, Guards, Interceptors, Styles)
- **Database & Data Store:**
  - PostgreSQL schema definitions, EF Core Migrations (10 migration files), Constraints, and live instance data on port 5432.
- **External Integrations:**
  - Razorpay payment gateway client, webhook handlers, and test mock implementations.
  - SignalR WebSocket / Long-Polling connection management.
  - QRCoder SVG/PNG barcode generation.

### Verified Environment & Executed Commands
- **Git State:** `git status` -> Branch: `feature/multi-canteen-management`, clean working tree.
- **Backend Build & Tests:** `dotnet test tests/Canteen.Tests/Canteen.Tests.csproj -c Release` -> **78 Passed, 0 Failed, 0 Skipped** (2.1s duration).
- **Frontend Build:** `npm run build` in `CanteenFrontend/canteen-ui` -> Build success (0 errors).
- **Frontend Unit Tests:** `npm test -- --no-watch` -> **3 Passed, 0 Failed** (6.58s duration).
- **Live Endpoint Probing:**
  - `GET http://localhost:5151/api/canteens` -> HTTP 200 OK (Exposes active canteens and OwnerIds).
  - `GET http://localhost:5151/api/canteens/2/orders` (unauthenticated) -> HTTP 401 Unauthorized (Verified secured).
  - `GET http://localhost:5151/swagger/v1/swagger.json` -> HTTP 200 OK.

### Explicit Audit Limitations
- Live Razorpay production transactions could not be verified because real credentials are intentionally excluded from development configuration (`Razorpay:KeyId = "REPLACE_IN_USER_SECRETS"`). The mock implementation `MockPaymentService` was verified instead.
- Load testing under high concurrency (>500 simultaneous users) was not conducted as per non-destructive audit rules.
- Production deployment environments (e.g., Kubernetes, AWS, or Azure hosting) were not available for live network inspection.

---

## 3. ARCHITECTURE OVERVIEW

```
+-----------------------------------------------------------------------------+
|                               ANGULAR 21 SPA                                |
|  Routes: /login, /canteen-setup, /my-canteens, /dashboard                   |
|  State: Angular Signals + Services (Auth, Canteen, Order, Product, Theme)   |
|  Interceptors: AuthInterceptor (Bearer token injection, 401 redirect)      |
|  Real-time: SignalRService (@microsoft/signalr)                             |
+-----------------------------------------------------------------------------+
                                      | HTTP REST / WebSocket
                                      v
+-----------------------------------------------------------------------------+
|                          ASP.NET CORE 10 WEB API                            |
|  Controllers: Auth, Canteens, Categories, Products, Tables, Orders, Payments|
|  Middleware: Global Exception Handler, CORS, HTTPS Redirection, JWT Bearer |
|  Real-time Hub: OrderHub (/hubs/orders) - Canteen and Order Tracking Groups|
+-----------------------------------------------------------------------------+
                                      | Dependency Injection
                                      v
+-----------------------------------------------------------------------------+
|                         BUSINESS ACCESS LAYER (BAL)                         |
|  Services: AuthService, CanteenService, OrderService, ProductService, etc.  |
|  Integrations: RazorpayPaymentService / MockPaymentService, QrCodeService   |
|  Security: PBKDF2 Password Hashing, JWT Generator, Anti-BOLA validations    |
+-----------------------------------------------------------------------------+
                                      | Repositories
                                      v
+-----------------------------------------------------------------------------+
|                          DATA ACCESS LAYER (DAL)                            |
|  EF Core 9.0.1 / Npgsql 9.0.4 - CanteenDbContext                           |
|  Repositories: Canteen, Owner, Category, Product, Table, Order, Payment     |
+-----------------------------------------------------------------------------+
                                      | SQL
                                      v
+-----------------------------------------------------------------------------+
|                            POSTGRESQL DATABASE                              |
|  Tables: Owners, Canteens, Categories, Products, Tables, Orders, OrderItems,|
|          Payments, PaymentWebhookEvents                                     |
|  Composite FKs: (CanteenId, CategoryId), (CanteenId, TableId)               |
|  Check Constraints: Price >= 0, StockQuantity >= 0                          |
+-----------------------------------------------------------------------------+
```

### End-to-End Request Lifecycles
1. **User Authentication Flow:**
   - User inputs credentials on Angular `/login`.
   - `AuthService.login()` issues `POST /api/auth/login`.
   - `AuthController.Login` invokes `AuthService.LoginAsync()`, which queries `OwnerRepository.GetByEmailAsync()` in PostgreSQL.
   - Password is verified using `CryptographicOperations.FixedTimeEquals` against the PBKDF2 hash.
   - On match, `GenerateJwtToken()` embeds claims (`NameIdentifier`, `Email`, `Name`, `Role`, and optional `CanteenId`).
   - Token is returned to Angular, stored in `localStorage` (`canteen_auth_user`), and set in Angular signal `currentUser`.
   - Angular router navigates to `/dashboard` if a canteen is active, or `/canteen-setup` if none is assigned.

2. **Multi-Canteen Switching Flow:**
   - On `/my-canteens` or the dashboard canteen dropdown, owner clicks "Switch Canteen".
   - Angular invokes `AuthService.switchCanteen(canteenId)` -> `POST /api/auth/switch-canteen/{canteenId}`.
   - Backend verifies ownership via `_canteenRepository.IsOwnerAuthorizedAsync(ownerId, canteenId)`.
   - Active `Owner.CanteenId` is updated in PostgreSQL, and a refreshed JWT with the new `CanteenId` claim is returned.
   - Angular updates `localStorage`, resets dashboard signals, and loads the new canteen's metrics.

3. **Customer Ordering Flow (Backend Contract):**
   - Customer scans physical table QR code (`/menu?canteenId=X&tableId=Y&token=Z`).
   - Customer submits cart via `POST /api/orders?qrToken={token}`.
   - `OrderService.CreateAsync()` verifies table availability and token validity.
   - Products are validated for tenant membership (Anti-BOLA check).
   - Stock is deducted row-by-row with SQL conditional update.
   - Server calculates totals, generates a unique order number (`ORD-yyyyMMdd-XXXX`) and an unguessable 128-bit hex `TrackingToken`.
   - Order is saved and `OrderNotificationService` broadcasts `NewOrderPlaced` event to SignalR group `canteen_{canteenId}`.
   - Frontend Gap: The customer UI for this flow is completely missing.

---

## 4. APPLICATION FEATURE INVENTORY

| Module / Feature | Backend Endpoints & Components | Frontend UI Route / Screen | Actual Verification Status | Evidence / Notes |
| :--- | :--- | :--- | :--- | :--- |
| **Owner Registration** | `POST /api/auth/register` (`AuthController.cs:29`) | `/login` (Register tab) | **Verified Working** | Hash: PBKDF2; JWT returned; test passes in `ApiIntegrationTests.cs`. |
| **Owner Login & Logout** | `POST /api/auth/login` (`AuthController.cs:52`) | `/login` | **Verified Working** | Verified with live database authentication; token stored in localStorage. |
| **Password Reset** | `POST /api/auth/forgot-password`, `POST /api/auth/reset-password` | `/login` (Forgot Password tab) | **Verified Working** | 32-byte crypto token generated; verified in `ApiIntegrationTests.cs`. |
| **Canteen Setup Wizard** | `POST /api/canteens` (`CanteensController.cs:60`) | `/canteen-setup` | **Verified Working** | Multi-step form (Details, Address, Operating Hours, Image, Review). |
| **My Canteens Management**| `GET /api/canteens/my` (`CanteensController.cs:34`) | `/my-canteens` | **Verified Working** | Cards show status, menu items, tables, today orders; tested live. |
| **Canteen Switching** | `POST /api/auth/switch-canteen/{id}` (`AuthController.cs:118`) | Header dropdown & `/my-canteens` | **Verified Working** | Issues new JWT, clears and reloads canteen state; verified live. |
| **Owner Dashboard** | Composite metrics from `/orders`, `/products`, `/categories` | `/dashboard` | **Verified Working** | Live metrics calculate from real orders, revenue, product counts. |
| **Category Management** | CRUD `/api/canteens/{id}/categories` | `/dashboard` (Category Modal) | **Verified Working** | Modal supports create/view; verified in `ApiIntegrationTests.cs`. |
| **Food Item Management**| CRUD `/api/canteens/{id}/products` | `/dashboard` (Food Items Modal) | **Verified Working** | Supports create, stock, category assignment, price validation. |
| **Tables & QR Generation**| CRUD `/api/canteens/{id}/tables`, `/qrcode` | **No Dedicated Screen** | **Partially Implemented** | Backend complete; frontend sidebar opens Category modal instead. |
| **Customer QR Menu** | `GET /api/canteens/{id}/categories`, `products` | **Missing (`/menu` route)** | **Broken / Missing** | Scanning table QR code redirects to `/login`. No customer menu UI. |
| **Customer Order Creation**| `POST /api/orders?qrToken={token}` | **No Customer Cart/Checkout UI**| **Backend Only** | Backend fully functional; no customer frontend exists. |
| **Order Tracking (Customer)**| `GET /api/orders/track/{token}` | **Missing Screen** | **Backend Only** | Unguessable token tracking works on API; no UI page. |
| **Kitchen Display System** | SignalR `OrderHub.cs`, `/hubs/orders` | **Missing Dedicated Screen** | **Partially Implemented** | Backend SignalR works; dashboard listens for orders, but no KDS screen. |
| **Razorpay Payments** | `POST /api/payments/create-order`, `verify`, `webhook` | **No Customer Payment UI** | **Backend Only** | Server-calculated amounts, signature verification, webhooks tested. |
| **Staff & Roles** | Role claims exist in JWT (`CanteenOwner`) | **No Screen or API** | **Missing** | No user management endpoints or staff creation UI exist. |
| **Inventory Management** | `StockQuantity` column on `Product` | **No Dedicated Screen** | **Partially Implemented** | Stock edited only via food item modal; no stock log or alerts screen. |
| **Reports & Analytics** | No Reports Controller | **No Dedicated Screen** | **Missing** | Sidebar link is dead; no backend reporting queries exist. |
| **AI Recommendations** | None | None | **Missing** | Not implemented in frontend or backend. |

---

## 5. CRITICAL AND HIGH-SEVERITY FINDINGS

### [CRITICAL] FINDING SEC-001: Public Exposure of Sensitive Operational & Tenant Data
- **Severity:** CRITICAL
- **Confidence Level:** Confirmed (Verified via live curl and controller inspection)
- **Affected Module:** Backend Canteen API (`CanteensController.cs:24-28`)
- **Evidence:** `[HttpGet] public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());`
- **Explanation:** The endpoint `GET /api/canteens` has no `[Authorize]` attribute. Calling this endpoint anonymously returns the complete list of all registered canteens across all owners in the database. The returned JSON exposes:
  1. Internal database primary keys (`CanteenId`).
  2. The owning owner's identity (`OwnerId`).
  3. Private contact telephone numbers (`ContactPhone`) and emails (`ContactEmail`).
  4. Physical addresses and operating hours.
  5. Multi-megabyte raw Base64 image payloads.
- **Business Impact:** Any competitor or unauthenticated scraper can extract the entire customer list, owner identities, contact info, and business operating data across all tenants.
- **Remediation:** Remove public `GetAll` or restrict it to authenticated system administrators (`[Authorize(Roles = "SuperAdmin")]`). For customer table ordering, expose only a scoped public endpoint `GET /api/canteens/{id}/public-info` returning strictly `CanteenId`, `Name`, `Location`, and public opening hours.

---

### [CRITICAL] FINDING ARCH-001: Missing Customer Ordering Route and UI Portal
- **Severity:** CRITICAL
- **Confidence Level:** Confirmed (Inspected `app.routes.ts`, `app/features/`, and `QrCodeService.cs`)
- **Affected Module:** Angular Frontend Routing & Customer Ordering
- **Evidence:** 
  - `QrCodeService.cs:20`: `GenerateTableMenuUrl` produces `${_baseMenuUrl}?canteenId={canteenId}&tableId={tableId}&token={qrToken}`.
  - `appsettings.json:26`: `"BaseMenuUrl": "http://localhost:4200/menu"`.
  - `app.routes.ts:8-16`:
    ```typescript
    export const routes: Routes = [
      { path: '', redirectTo: 'login', pathMatch: 'full' },
      { path: 'login', component: Login },
      { path: 'dashboard', component: Dashboard, canActivate: [authGuard] },
      { path: 'my-canteens', component: MyCanteens, canActivate: [authGuard] },
      { path: 'canteens', redirectTo: 'my-canteens', pathMatch: 'full' },
      { path: 'canteen-setup', component: CanteenSetup, canActivate: [authGuard] },
      { path: '**', redirectTo: 'login' }
    ];
    ```
- **Explanation:** The QR codes generated for physical tables point to `http://localhost:4200/menu`. Because `/menu` is not registered in Angular's routes, the catch-all wildcard `**` immediately redirects any customer scanning a table QR code to `/login`. Furthermore, no customer-facing menu, cart, checkout, or tracking components exist in the frontend.
- **Business Impact:** The primary revenue-generating flow of the system (customers scanning QR codes at tables and placing food orders) is completely broken in the web application.
- **Remediation:** Implement a standalone customer ordering portal at route `/menu` accepting query parameters `canteenId`, `tableId`, and `token`. It must load the public menu, support cart operations, submit orders via `POST /api/orders?qrToken={token}`, and navigate to an order status/payment screen.

---

### [HIGH] FINDING DATA-001: Missing Transaction Scope Around Multi-Item Stock Deductions & Order Creation
- **Severity:** HIGH
- **Confidence Level:** Confirmed (Inspected `OrderService.cs:92-173`)
- **Affected Module:** Backend Order Processing (`OrderService.cs`)
- **Evidence:**
  `OrderService.CreateAsync` executes stock deductions item-by-item:
  ```csharp
  foreach (var item in consolidatedItems) {
      var success = await _productRepo.DeductStockAsync(canteenId, item.ProductId, item.Quantity);
      if (!success) {
          foreach (var (pId, qty) in deducted) {
              await _productRepo.RestoreStockAsync(canteenId, pId, qty);
          }
          throw new InsufficientStockException(...);
      }
      deducted.Add((item.ProductId, item.Quantity));
  }
  // Order saved AFTER loop without a database transaction:
  var created = await _orderRepo.AddAsync(order);
  ```
- **Explanation:** Although `DeductStockAsync` executes an atomic `UPDATE "Products" ... WHERE "StockQuantity" >= {quantity}`, the multi-item operation is managed in application memory with a manual compensating rollback loop. If the application server crashes, loses database connectivity, or fails during `_orderRepo.AddAsync(order)`, previous stock deductions are never restored. Stock is permanently lost without an associated order record.
- **Business Impact:** Phantom stock losses during server hiccups or database timeouts, leading to inventory discrepancies and unfulfilled orders.
- **Remediation:** Enclose the entire validation, stock deduction, and order persistence in an explicit EF Core database transaction:
  `using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);` with automatic rollback on unhandled exceptions.

---

### [HIGH] FINDING SEC-002: Inconsistent Multi-Tenant Ownership Authorization in Payments Controller
- **Severity:** HIGH
- **Confidence Level:** Confirmed (Inspected `PaymentsController.cs:127-133` vs `CategoriesController.cs:24-39`)
- **Affected Module:** Backend Payments (`PaymentsController.cs`)
- **Evidence:**
  In `PaymentsController.cs:129-133`:
  ```csharp
  var ownerCanteenId = User.GetCanteenId();
  if (ownerCanteenId == null || ownerCanteenId.Value != canteenId)
  {
      return Forbid();
  }
  ```
  In `CategoriesController.cs:24-39` and `OrdersController.cs:27-42`:
  ```csharp
  var ownerCanteenId = User.GetCanteenId();
  if (ownerCanteenId.HasValue && ownerCanteenId.Value == canteenId) return true;
  var ownerId = User.GetOwnerId();
  if (ownerId.HasValue && await _canteenService.IsOwnerAuthorizedAsync(ownerId.Value, canteenId)) return true;
  return false;
  ```
- **Explanation:** In `PaymentsController`, the check relies solely on the `CanteenId` claim in the JWT. If an owner manages multiple canteens (e.g., Canteen 2 and Canteen 6) and makes a request for Canteen 6 while their active JWT was minted for Canteen 2, `PaymentsController` returns `403 Forbidden`, even though they legitimately own Canteen 6. Conversely, other controllers correctly query `IsOwnerAuthorizedAsync(ownerId, canteenId)` to verify true ownership.
- **Business Impact:** Legitimate owners managing multiple canteens experience authorization failures when inspecting payment records unless they perform an explicit context switch first.
- **Remediation:** Refactor `PaymentsController` to inject `ICanteenService` and use the uniform `IsAuthorizedAsync(canteenId)` pattern used across other controllers.

---

### [HIGH] FINDING PERF-001: High-Volume Base64 Image Storage Directly in PostgreSQL Rows
- **Severity:** HIGH
- **Confidence Level:** Confirmed (Inspected `canteen-setup.ts:139-160`, `Canteen.cs:33`, and live API output)
- **Affected Module:** Database Schema & Canteen Setup Form
- **Evidence:** 
  In `canteen-setup.ts`, uploaded banner images are converted to `data:image/jpeg;base64,...` strings.
  Live API response for `GET /api/canteens` returned a payload containing over **1,400 lines of base64 text** for a single record.
- **Explanation:** EF Core stores these base64 image strings directly in the `Canteens.ImageUrl` text column. Whenever queries fetch canteen records (such as `GetAll()`, `GetById()`, or `GetOwnerCanteensAsync()`), Postgres must stream megabytes of image text across the wire, severely bloating memory allocations, slowing down query response times, and risking out-of-memory errors on concurrent dashboard refreshes.
- **Business Impact:** As more canteens are added, dashboard and canteen listings will experience drastic slowdowns and excessive database bandwidth consumption.
- **Remediation:** Store images on disk or cloud object storage (e.g., AWS S3, Cloudflare R2, Azure Blob), saving only the resulting CDN/static URL string in the database column.

---

### [MEDIUM] FINDING UI-001: Non-Functional "Phantom" Navigation in Owner Sidebar
- **Severity:** MEDIUM
- **Confidence Level:** Confirmed (Inspected `dashboard.html:73-125` and `my-canteens.html:70-100`)
- **Affected Module:** Angular Frontend Dashboard & Navigation
- **Evidence:**
  In `dashboard.html`:
  - Line 73: `<!-- Tables & QR Codes --> <a class="nav-item" (click)="openMenuModal('categories')">`
  - Line 90: `<!-- Orders --> <a class="nav-item">` (No action)
  - Line 99: `<!-- Kitchen Display --> <a class="nav-item">` (No action)
  - Line 112: `<!-- Payments --> <a class="nav-item">` (No action)
  - Line 120: `<!-- Reports --> <a class="nav-item">` (No action)
  In `my-canteens.html`:
  - Lines 71-100: All sidebar links (Tables, Orders, KDS, Payments, Reports, Staff, Inventory) point directly to `routerLink="/dashboard"`.
- **Explanation:** The UI presents navigation buttons for features that lack dedicated screens. Clicking "Tables & QR Codes" unexpectedly opens the Category creation modal, while clicking "Orders" or "Kitchen Display" produces no reaction or redirects to the dashboard home.
- **Business Impact:** Misleads users and creates a confusing, unpolished user experience.
- **Remediation:** Either implement dedicated views for these modules or add disabled badges (e.g., `<span class="badge">Coming Soon</span>`) with user-friendly placeholder modals until screens are developed.

---

## 6. DETAILED MODULE-BY-MODULE FINDINGS

### A. Authentication & Identity
- **Strengths:** 
  - Standard PBKDF2-HMAC-SHA256 password hashing with 100,000 iterations and cryptographically random 16-byte salt (`AuthService.cs:276-285`).
  - Fixed-time byte comparison (`CryptographicOperations.FixedTimeEquals`) protects against side-channel timing attacks on login and password reset.
  - JWT token includes standard claims (`NameIdentifier`, `Email`, `Role`, `CanteenId`) and enforces minimum 32-byte key length on startup (`Program.cs:85-88`).
  - Session refresh (`/api/auth/refresh`) and active canteen switching (`/api/auth/switch-canteen/{id}`) properly issue refreshed tokens.
- **Weaknesses:**
  - Token refresh relies on valid unexpired tokens; there is no persistent Refresh Token mechanism with token rotation in PostgreSQL. If an owner's session expires after 60 minutes, they must log in again.
  - No account lockout mechanism after repeated failed login attempts (susceptible to brute-force attacks if not rate-limited at reverse proxy).

### B. Canteen Management & Multi-Canteen
- **Strengths:**
  - One owner can own multiple canteens (`Canteen.OwnerId -> Owner.OwnerId` with `DeleteBehavior.Cascade`).
  - `GET /api/canteens/my` computes real-time counts for active menu items, tables, and today's orders per canteen.
  - Wizard form (`/canteen-setup`) validates required business fields (name, location, phone, email, operating hours schedule).
  - Migration `20261027140000_AddMultiCanteenSupport` successfully backfilled pre-existing canteens to their owners.
- **Weaknesses:**
  - In `Canteen.cs:39`, `OwnerId` is declared nullable (`int?`). While this facilitated non-breaking migrations, newly created canteens should strictly require an owner.
  - `GET /api/canteens` publicly exposes all canteens to unauthenticated callers (Finding SEC-001).

### C. Dashboard
- **Strengths:**
  - Pure Angular 21 computed signals derive statistics strictly from active API data (`todayOrdersCount`, `todayRevenue`, `activeKitchenOrdersCount`, `totalMenuItemsCount`).
  - Signals reset cleanly during canteen switching (`switchCanteenContext`), preventing cross-canteen data bleeding.
  - Loading skeletons and informative zero-state graphics display when a canteen has no orders or menu items.
- **Weaknesses:**
  - Revenue calculation computes from all orders where date matches today, regardless of whether `PaymentStatus == "Paid"` or `Status == "Cancelled"`. Cancelled unpaid orders should not contribute to operational revenue metrics.

### D. Categories & Food Items
- **Strengths:**
  - Enforces database-level multi-tenant isolation via composite alternate keys `(CanteenId, CategoryId)`. A product in Canteen A cannot reference a category belonging to Canteen B.
  - Check constraints in PostgreSQL (`"Price" >= 0` and `"StockQuantity" >= 0`) enforce domain invariants at the storage layer.
  - Category deletion is guarded: returns HTTP 409 Conflict if products are attached.
- **Weaknesses:**
  - Category and product listings lack pagination. If a canteen has 500 items, all 500 records are fetched in a single query.

### E. Tables, QR Codes, and Customer Ordering
- **Strengths:**
  - Table numbers are unique per canteen (`IX_Tables_CanteenId_TableNumber`).
  - Cryptographically secure 16-byte random QR tokens (`Convert.ToHexString(RandomNumberGenerator.GetBytes(16))`).
  - Server-side price calculation in `OrderService.CreateAsync` ignores client-submitted prices and derives totals strictly from current product prices.
  - Order numbers are human-readable and scoped per canteen (`ORD-yyyyMMdd-XXXX`).
  - Orders receive an unguessable 128-bit hex `TrackingToken` allowing customers to poll or subscribe to order status without logging in.
- **Weaknesses:**
  - No frontend customer UI exists at `/menu` (Finding ARCH-001).
  - Stock deductions across multi-item orders are not wrapped in a database transaction (Finding DATA-001).

### F. Kitchen Display System (KDS) & Real-Time Updates
- **Strengths:**
  - `OrderHub.cs` auto-joins authenticated owners to room `canteen_{canteenId}` derived from JWT claims.
  - Anonymous customers join room `order_{orderId}` only after validating their `trackingToken` using constant-time string comparison (`OrderHub.cs:88`).
  - Dispatches events: `NewOrderPlaced`, `PaymentConfirmed`, `OrderStatusUpdated`.
- **Weaknesses:**
  - No dedicated full-screen KDS UI component exists for kitchen staff.
  - Real-time hub connection fallback is configured for WebSockets and LongPolling, but Redis backplane is not configured for horizontal multi-server scaling.

### G. Payments & Settlements
- **Strengths:**
  - Razorpay order creation calculates amount in paise strictly from backend order totals (`RazorpayPaymentService.cs:111`).
  - HMAC-SHA256 signature verification validates authenticity before confirming payment status.
  - Idempotency enforced on webhooks via `PaymentWebhookEvents` table index on `EventId`.
  - Development mock service `MockPaymentService` is strictly prevented from executing in production by startup validation in `Program.cs:52`.
- **Weaknesses:**
  - Authorization check in `PaymentsController.GetPayment` is inconsistent with the rest of the application (Finding SEC-002).
  - Webhook secret verification is not fully implemented in the mock mode and relies on development settings.

### H. Staff Management & Roles
- **Status:** **Missing**
- Currently, only the `CanteenOwner` role exists in the database (`Owner.Role`). There are no entities or APIs for staff members (Cashier, Chef, Waiter) with granular permissions.

### I. Inventory Management
- **Status:** **Partially Implemented**
- `Product.StockQuantity` manages stock levels, but there is no dedicated stock adjustment ledger, low-stock threshold alerting, or supplier management module.

### J. Reports & Analytics
- **Status:** **Missing**
- Basic daily totals appear on the dashboard, but historical revenue reports, export to CSV/PDF, popular item analytics, and tax summaries are not implemented.

### K. AI Recommendations
- **Status:** **Missing**
- The repository name references AI recommendations, but no recommendation service, ML model, or OpenAI/Gemini integration currently exists in the codebase.

---

## 7. DATABASE AND MIGRATION REVIEW

### Schema & Entity Relationships
```
[Owners] 1 ──── * [Canteens] 1 ──── * [Categories]
                       │                     │
                       │ 1                   │ 1
                       │                     │
                       ├───── * [Products] <─┘ (Composite FK: CanteenId + CategoryId)
                       │           │
                       │ 1         │ 1
                       │           │
                       ├───── * [Tables]     │
                       │           │         │
                       │ 1         │ 1       │
                       │           │         │
                       └───── * [Orders]     │ (Composite FK: CanteenId + TableId)
                                   │ 1       │
                                   │         │
                                   *         *
                              [OrderItems] ──┘
```

### Migration History & Consistency
EF Core migration history in `Canteen.DAL/Migrations` contains 10 sequential migrations:
1. `20261023131046_InitialCreate`
2. `20261023134152_AddOwnerEntity`
3. `20261024131304_AddOrdersAndCompositeKeys`
4. `20261026092208_AddPaymentStatusToOrders`
5. `20261026095742_AddPaymentEntities`
6. `20261026120032_AddOrderTrackingToken`
7. `20261027093147_AddPasswordResetTokenToOwner`
8. `20261027122704_AddCanteenDetailedAttributes`
9. `20261027133700_AddCanteenImageUrl`
10. `20261027140000_AddMultiCanteenSupport`

### Database Audit Findings
1. **Model Snapshot Consistency:** The snapshot `CanteenDbContextModelSnapshot.cs` is fully aligned with migration 10. `PendingModelChangesWarning` was suppressed in `Program.cs:25` to prevent startup termination during hot reload.
2. **Cascading Deletes:** Deleting an `Owner` cascades to delete their `Canteens`. However, canteens restrict deletion if active orders, tables, or products exist (`OnDelete(DeleteBehavior.Restrict)`), preventing orphaned records.
3. **Monetary Precision:** All currency fields (`Price`, `TotalAmount`, `UnitPrice`, `Amount`) are configured with `decimal(18,2)`.
4. **Missing Indexes:** 
   - `Orders.CreatedAt` and `Orders.Status` lack a composite index, which will cause sequential scans as order volume grows during daily dashboard filtering.
   - `Products.IsAvailable` lacks an index.

---

## 8. SECURITY ASSESSMENT

### Multi-Tenant Isolation
- **Tenant Verification Pattern:** Verified that `CategoriesController`, `ProductsController`, `TablesController`, and `OrdersController` implement `IsAuthorizedAsync(canteenId)`:
  - Validates whether caller's JWT `CanteenId` claim matches the route parameter.
  - If mismatched, falls back to checking database ownership via `_canteenService.IsOwnerAuthorizedAsync(ownerId, canteenId)`.
  - If neither matches, returns `403 Forbidden`.
- **Database Boundary:** Composite foreign keys physically block cross-tenant poisoning. Even if an attacker tampers with a product payload to specify Category ID 42 from Canteen B, PostgreSQL rejects the insert because `(CanteenId, CategoryId)` does not match.

### Authentication & JWT Security
- Cryptographic keys: Validates minimum 32 bytes (256 bits).
- Expiration: Defaults to 60 minutes.
- Password hashing: PBKDF2-HMAC-SHA256 with 100,000 iterations.
- Vulnerability: Unauthenticated `GET /api/canteens` leaks all canteens and owner IDs (Finding SEC-001).

### CORS & Network Security
- In `Program.cs:130-151`, CORS is restricted to origins configured in `Cors:AllowedOrigins` (`http://localhost:3000`, `http://localhost:5173`, `http://localhost:4200`). Wildcard origins (`*`) are correctly rejected when credentials are enabled.

---

## 9. API INVENTORY AND INTEGRATION STATUS

| Controller | Method & Route | Auth Required | Tenant Scoped | Calling Frontend Service / UI Screen | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `AuthController` | `POST /api/auth/register` | Anonymous | N/A | `AuthService.register()` / `/login` | Verified Working |
| `AuthController` | `POST /api/auth/login` | Anonymous | N/A | `AuthService.login()` / `/login` | Verified Working |
| `AuthController` | `POST /api/auth/refresh` | `Authorize` | Yes | `AuthService.getProfile()` | Verified Working |
| `AuthController` | `GET /api/auth/me` | `Authorize` | Yes | `AuthService.getProfile()` / `/canteen-setup` | Verified Working |
| `AuthController` | `POST /api/auth/switch-canteen/{id}`| `CanteenOwner`| Yes | `AuthService.switchCanteen()` / Header | Verified Working |
| `AuthController` | `POST /api/auth/forgot-password` | Anonymous | N/A | `AuthService.forgotPassword()` / `/login` | Verified Working |
| `AuthController` | `POST /api/auth/reset-password` | Anonymous | N/A | `AuthService.resetPassword()` / `/login` | Verified Working |
| `CanteensController` | `GET /api/canteens` | **Anonymous** | **No** | None (Unused by UI) | **Security Risk (SEC-001)** |
| `CanteensController` | `GET /api/canteens/my` | `CanteenOwner`| Yes | `CanteenService.getMyCanteens()` / `/my-canteens` | Verified Working |
| `CanteensController` | `GET /api/canteens/{id}` | Anonymous | Yes | `CanteenService.getById()` / `/dashboard` | Verified Working |
| `CanteensController` | `POST /api/canteens` | `CanteenOwner`| Yes | `CanteenService.create()` / `/canteen-setup` | Verified Working |
| `CanteensController` | `PUT /api/canteens/{id}` | `CanteenOwner`| Yes | `CanteenService.update()` | Implemented |
| `CanteensController` | `DELETE /api/canteens/{id}` | `CanteenOwner`| Yes | None | Implemented (No UI) |
| `CategoriesController`| `GET /api/canteens/{id}/categories`| Anonymous | Yes | `CategoryService.getAllByCanteen()` / `/dashboard`| Verified Working |
| `CategoriesController`| `POST /api/canteens/{id}/categories`| `CanteenOwner`| Yes | `CategoryService.create()` / Modal | Verified Working |
| `CategoriesController`| `DELETE /api/canteens/{id}/categories/{catId}`| `CanteenOwner`| Yes | `CategoryService.delete()` / Modal | Verified Working |
| `ProductsController` | `GET /api/canteens/{id}/products`| Anonymous | Yes | `ProductService.getAllByCanteen()` / `/dashboard`| Verified Working |
| `ProductsController` | `POST /api/canteens/{id}/products`| `CanteenOwner`| Yes | `ProductService.create()` / Modal | Verified Working |
| `ProductsController` | `DELETE /api/canteens/{id}/products/{pId}`| `CanteenOwner`| Yes | `ProductService.delete()` / Modal | Verified Working |
| `TablesController` | `GET /api/canteens/{id}/tables` | Anonymous | Yes | **None in UI** | Backend Only |
| `TablesController` | `POST /api/canteens/{id}/tables`| `CanteenOwner`| Yes | **None in UI** | Backend Only |
| `TablesController` | `GET /api/canteens/{id}/tables/{tId}/qrcode`| Anonymous | Yes | **None in UI** | Backend Only |
| `OrdersController` | `POST /api/orders?qrToken={token}`| Anonymous | Yes | **None in UI (Customer ordering missing)** | Backend Only |
| `OrdersController` | `GET /api/orders/track/{token}`| Anonymous | Yes | **None in UI** | Backend Only |
| `OrdersController` | `GET /api/canteens/{id}/orders` | `CanteenOwner`| Yes | `OrderService.getOrdersByCanteen()` / `/dashboard`| Verified Working |
| `OrdersController` | `PATCH /api/canteens/{id}/orders/{oId}/status`| `CanteenOwner`| Yes| `OrderService.updateOrderStatus()` / `/dashboard`| Verified Working |
| `PaymentsController` | `POST /api/payments/create-order`| Anonymous | Yes | **None in UI** | Backend Only |
| `PaymentsController` | `POST /api/payments/verify` | Anonymous | Yes | **None in UI** | Backend Only |
| `PaymentsController` | `POST /api/payments/webhook`| Anonymous | Yes | Razorpay Webhook Engine | Backend Only |
| `PaymentsController` | `GET /api/canteens/{id}/orders/{oId}/payment`| `CanteenOwner`| Inconsistent | **None in UI** | Bug (SEC-002) |

---

## 10. TESTING RESULTS

### Backend Test Suite Execution
- **Command:** `dotnet test tests/Canteen.Tests/Canteen.Tests.csproj -c Release`
- **Execution Date:** October 28, 2026
- **Test Runner:** Microsoft.NET.Test.Sdk 17.13.0 / xUnit 2.9.3
- **Test Framework:** In-Memory / Relational WebApplicationFactory
- **Total Tests:** 78
- **Passed:** 78
- **Failed:** 0
- **Skipped:** 0
- **Execution Duration:** 2.1 seconds
- **Key Test Suites Passed:**
  - `ApiIntegrationTests.cs`: Registration, login, profile, category CRUD, product CRUD, 401/403 security boundary tests.
  - `MultiCanteenTests.cs`: Multi-canteen creation per owner, cross-tenant isolation, canteen switching.
  - `OrderManagementTests.cs`: Order lifecycle transitions (Pending -> Preparing -> Ready -> Completed -> Cancelled).
  - `PaymentTests.cs`: Order creation, HMAC signature verification, idempotency on webhook events, mock provider execution.
  - `QrCodeTests.cs`: QR URL generation, PNG binary barcode structure.
  - `SecurityDefectTests.cs`: Anti-BOLA product injection prevention, negative price/stock prevention.
  - `SignalRNotificationTests.cs`: Hub room join, kitchen notification dispatch, tracking room authorization.
  - `StockConcurrencyTests.cs`: Concurrent stock decrement verification.

### Frontend Test & Build Execution
- **Production Bundle Build:** `npm run build`
  - Output Directory: `dist/canteen-ui`
  - Status: Success (0 errors, 0 warnings)
  - Initial Bundle Size: ~161 kB raw
- **Frontend Unit Tests:** `npm test -- --no-watch`
  - Runner: Vitest 4.1.12 with Angular Build Application
  - Total Tests: 3
  - Passed: 3 (`app.spec.ts`: 2 tests passed; `login.spec.ts`: 1 test passed)
  - Duration: 6.58 seconds

---

## 11. PERFORMANCE AND SCALABILITY

### Measured Observations
- Frontend initial bundle size is lightweight (161 kB total initial bundle), ensuring fast initial load times under 1 second on standard networks.
- Backend database queries for dashboard metrics currently execute sequentially on the client (`forkJoin` of orders, products, and categories).

### Code-Review Inferred Risks
1. **Unbounded Result Sets:**
   `_orderService.GetAllByCanteenAsync(canteenId)` executes `.ToListAsync()` without a `Take(N)` or date filter. As a canteen accumulates thousands of orders over months, fetching the entire list on every dashboard load will degrade backend memory and network performance.
2. **Sequential Query Execution in Stock Deduction:**
   `OrderService.CreateAsync` executes an individual SQL update for every distinct item in an order. For an order with 10 items, 10 separate round-trips to PostgreSQL are executed.
3. **Database Bloat from Base64 Images:**
   Storing 5MB base64 images inside the relational table prevents efficient caching and degrades database buffer cache performance.

---

## 12. PRODUCTION READINESS EVALUATION

| Operational Category | Current Status | Finding / Evaluation | Readiness Gate |
| :--- | :--- | :--- | :--- |
| **Secrets Management** | ⚠️ Incomplete | `appsettings.json` contains placeholders (`REPLACE_ME...`). Must use Environment Variables / Key Vault in production. | **Blocker** |
| **Database Migrations** | ⚠️ Incomplete | Development runs `db.Database.Migrate()` on startup. In production with multiple instances, this causes migration lock deadlocks. | **Blocker** |
| **Containerization** | ❌ Missing | No `Dockerfile` or `docker-compose.yml` exists for backend or frontend. | **Blocker** |
| **CI/CD Automation** | ❌ Missing | No GitHub Actions or automated deployment pipelines configured. | **High Risk** |
| **Health Checks** | ❌ Missing | No ASP.NET Core Health Checks (`/health`) configured for load balancers. | **High Risk** |
| **Observability & Logging** | ⚠️ Basic | Basic console logging via `ILogger`. No structured Serilog, OpenTelemetry, or Seq sink configured. | **Medium Risk** |
| **CORS Configuration** | ⚠️ Development | Hardcoded localhost ports in `appsettings.json`. Production domain must be injected dynamically. | **Blocker** |
| **Environment Swapping**| ❌ Missing | Angular `environment.ts` hardcodes `http://localhost:5151` with no production replacement configuration. | **Blocker** |

---

## 13. TECHNICAL DEBT

1. **Suppression of EF Core Model Warnings:**
   In `Program.cs:25`, `options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))` is active. While this prevents crashes during development, it masks underlying model differences.
2. **Missing Customer UI Components:**
   The architectural split between owner management and customer ordering is incomplete in the frontend project.
3. **Absence of Dedicated Screen Views:**
   KDS, Tables, Orders, and Payments reside entirely in the backend with no matching Angular feature components.
4. **Direct Base64 Image Uploads:**
   Image handling lacks an abstraction layer (`IFileStorageService`) for uploading to S3, GCS, or local static files.

---

## 14. RECOMMENDED REMEDIATION PLAN

### Priority P0: Critical Security & Functionality Blockers
- **TASK-P0-1: Implement Customer QR Ordering Portal (`/menu`)**
  - *Problem:* Customers scanning table QR codes get redirected to `/login` (Finding ARCH-001).
  - *Proposed Change:* Create standalone Angular component `CustomerMenuComponent` at route `/menu`. Parse `canteenId`, `tableId`, `token` query parameters. Display categories, products, cart drawer, checkout modal, and order placement integration with `POST /api/orders?qrToken={token}`.
  - *Complexity:* **L (Large)**
  - *Acceptance Criteria:* Scanning `http://localhost:4200/menu?canteenId=2&tableId=1&token=XYZ` loads the canteen menu, allows adding items to cart, and successfully places an order.
  - *Regression Tests:* Automated frontend test verifying route activation without authentication.

- **TASK-P0-2: Secure Unauthenticated Canteen Listing Endpoint**
  - *Problem:* `GET /api/canteens` leaks all canteens, owner IDs, phone numbers, and base64 images (Finding SEC-001).
  - *Proposed Change:* Restrict `GET /api/canteens` to `[Authorize(Roles = "SuperAdmin")]`. Create public endpoint `GET /api/canteens/{id}/public-info` returning strictly non-sensitive fields (`CanteenId`, `Name`, `Location`, `OperatingHoursJson`).
  - *Complexity:* **S (Small)**
  - *Acceptance Criteria:* Unauthenticated calls to `GET /api/canteens` return 401 Unauthorized. Public endpoint returns only public metadata.
  - *Regression Tests:* Integration test asserting 401 on `GET /api/canteens`.

- **TASK-P0-3: Wrap Multi-Item Stock Deductions in Database Transaction**
  - *Problem:* Multi-item orders execute outside an EF Core transaction, risking permanent stock inconsistencies if errors occur (Finding DATA-001).
  - *Proposed Change:* Wrap stock deduction and order persistence in `OrderService.CreateAsync` with `using var tx = await _context.Database.BeginTransactionAsync()`.
  - *Complexity:* **M (Medium)**
  - *Acceptance Criteria:* If any product fails or order persistence fails, all database changes rollback cleanly.
  - *Regression Tests:* Unit test simulating persistence failure and confirming unchanged stock quantities.

### Priority P1: Security & Architecture Corrections
- **TASK-P1-1: Unify Multi-Tenant Authorization in Payments Controller**
  - *Problem:* `PaymentsController.GetPayment` checks only token claim `CanteenId`, failing for valid owners of multiple canteens (Finding SEC-002).
  - *Proposed Change:* Inject `ICanteenService` and use `IsAuthorizedAsync(canteenId)` checking both claim and `_canteenService.IsOwnerAuthorizedAsync(ownerId, canteenId)`.
  - *Complexity:* **S (Small)**
  - *Acceptance Criteria:* Owner managing Canteen A and B can view payments for both canteens.
  - *Regression Tests:* Automated integration test verifying payment lookup across multiple owned canteens.

- **TASK-P1-2: Externalize Image Storage from PostgreSQL Columns**
  - *Problem:* Base64 images stored in database bloat rows and degrade query performance (Finding PERF-001).
  - *Proposed Change:* Implement `IFileStorageService` saving images to `wwwroot/uploads` (or cloud storage) and save relative URLs in `ImageUrl`.
  - *Complexity:* **M (Medium)**
  - *Acceptance Criteria:* Canteen and product images save as file URLs; database columns store only URLs.

### Priority P2: Functional Gaps & User Experience
- **TASK-P2-1: Implement Dedicated Kitchen Display System (KDS) Screen**
  - *Proposed Change:* Build `/kds` route in Angular. Connect to `SignalRService`, display live order cards grouped by status (Pending, Preparing, Ready), and provide 1-click status update buttons.
  - *Complexity:* **M (Medium)**

- **TASK-P2-2: Implement Dedicated Tables & QR Code Screen**
  - *Proposed Change:* Build `/tables` route in Angular. Allow owner to create tables, view list, and download/print generated QR codes.
  - *Complexity:* **M (Medium)**

- **TASK-P2-3: Fix Sidebar Navigation Links**
  - *Proposed Change:* Update inert links in `dashboard.html` and `my-canteens.html` to navigate to real screens or show "Coming Soon" badges.
  - *Complexity:* **S (Small)**

### Priority P3: Performance & Operational Readiness
- **TASK-P3-1: Add Pagination and Date Filtering to Orders API**
  - *Proposed Change:* Update `GET /api/canteens/{id}/orders` with `pageIndex`, `pageSize`, `startDate`, and `status` query parameters.
  - *Complexity:* **M (Medium)**

- **TASK-P3-2: Dockerization & Production Environment Configuration**
  - *Proposed Change:* Add `Dockerfile` for backend and frontend. Add `docker-compose.yml` for local container testing. Configure Angular production environment replacements.
  - *Complexity:* **M (Medium)**

---

## 15. DEVELOPMENT ROADMAP & MILESTONES

```
+----------------------------------------------------------------------------+
| MILESTONE 1: Security & Transactional Hardening (P0 / P1)                  |
| - Secure GET /api/canteens (SEC-001)                                       |
| - Wrap OrderService in IDbContextTransaction (DATA-001)                     |
| - Fix PaymentsController multi-canteen check (SEC-002)                     |
| Validation Gate: 100% backend test pass rate + zero anonymous data leaks    |
+----------------------------------------------------------------------------+
                                      │
                                      ▼
+----------------------------------------------------------------------------+
| MILESTONE 2: Customer Ordering Flow Completion (P0)                        |
| - Build Angular /menu component with table QR token parsing                |
| - Implement Customer Cart & Checkout                                       |
| - Implement Customer Order Status Tracking view                            |
| Validation Gate: End-to-end QR scan -> order placed -> visible in DB       |
+----------------------------------------------------------------------------+
                                      │
                                      ▼
+----------------------------------------------------------------------------+
| MILESTONE 3: Operations & Kitchen Workflow (P2)                            |
| - Build Kitchen Display System (KDS) screen at /kds                        |
| - Build Tables & QR Code management screen at /tables                      |
| - Connect live SignalR updates between Customer, Kitchen, and Dashboard    |
| Validation Gate: Status update on KDS instantly updates customer screen    |
+----------------------------------------------------------------------------+
                                      │
                                      ▼
+----------------------------------------------------------------------------+
| MILESTONE 4: Production Packaging & Containerization (P3)                  |
| - Create Dockerfile and docker-compose definitions                         |
| - Externalize file uploads (IFileStorageService)                           |
| - Setup GitHub Actions CI/CD pipeline                                      |
| Validation Gate: Clean Docker build and production deployment verification |
+----------------------------------------------------------------------------+
```

---

## 16. FINAL ASSESSMENT

The CanteenHub system possesses a **strong, professional backend core** with exemplary architectural layering, robust unit/integration test coverage (78/78 passing), clean password hashing and cryptographic controls, and well-designed relational constraints.

However, **it is not yet ready for production deployment** due to:
1. The absence of the customer ordering web interface (`/menu`), which renders physical table QR codes unusable.
2. The public exposure of all canteen and owner records on `GET /api/canteens`.
3. The lack of an explicit database transaction around multi-item stock reservations.
4. Non-functional navigation links pointing to unimplemented frontend screens.

Once the prioritized P0 and P1 remediation tasks are executed, CanteenHub will achieve the security, reliability, and functional completeness required for a commercial multi-tenant food service platform.

---

## 17. EVIDENCE APPENDIX

### Key Source Files & Evidence Locations
- **Backend Entry & Middleware:** [`CanteenManagementSystem/Program.cs`](file:///d:/CanteenManagementSystem/CanteenManagementSystem/Program.cs#L20-L265)
- **Authentication Controller:** [`CanteenManagementSystem/Controllers/AuthController.cs`](file:///d:/CanteenManagementSystem/CanteenManagementSystem/Controllers/AuthController.cs#L24-L173)
- **Canteens Controller:** [`CanteenManagementSystem/Controllers/CanteensController.cs`](file:///d:/CanteenManagementSystem/CanteenManagementSystem/Controllers/CanteensController.cs#L23-L123)
- **Orders Controller:** [`CanteenManagementSystem/Controllers/OrdersController.cs`](file:///d:/CanteenManagementSystem/CanteenManagementSystem/Controllers/OrdersController.cs#L45-L164)
- **Payments Controller:** [`CanteenManagementSystem/Controllers/PaymentsController.cs`](file:///d:/CanteenManagementSystem/CanteenManagementSystem/Controllers/PaymentsController.cs#L26-L138)
- **Order Processing Service:** [`Canteen.BAL/Services/OrderService.cs`](file:///d:/CanteenManagementSystem/CanteenManagementSystem/Canteen.BAL/Services/OrderService.cs#L40-L173)
- **Razorpay Integration Service:** [`Canteen.BAL/Services/RazorpayPaymentService.cs`](file:///d:/CanteenManagementSystem/CanteenManagementSystem/Canteen.BAL/Services/RazorpayPaymentService.cs#L54-L200)
- **SignalR Order Hub:** [`CanteenManagementSystem/Hubs/OrderHub.cs`](file:///d:/CanteenManagementSystem/CanteenManagementSystem/Hubs/OrderHub.cs#L30-L98)
- **Database Context & Model:** [`Canteen.DAL/Data/CanteenDbContext.cs`](file:///d:/CanteenManagementSystem/CanteenManagementSystem/Canteen.DAL/Data/CanteenDbContext.cs#L33-L310)
- **Multi-Canteen Migration:** [`Canteen.DAL/Migrations/20261027140000_AddMultiCanteenSupport.cs`](file:///d:/CanteenManagementSystem/CanteenManagementSystem/Canteen.DAL/Migrations/20261027140000_AddMultiCanteenSupport.cs#L11-L50)
- **Frontend App Routes:** [`CanteenFrontend/canteen-ui/src/app/app.routes.ts`](file:///d:/CanteenManagementSystem/CanteenFrontend/canteen-ui/src/app/app.routes.ts#L8-L16)
- **Dashboard Component:** [`CanteenFrontend/canteen-ui/src/app/features/dashboard/dashboard.ts`](file:///d:/CanteenManagementSystem/CanteenFrontend/canteen-ui/src/app/features/dashboard/dashboard.ts#L27-L120)
- **Dashboard Template & Sidebar:** [`CanteenFrontend/canteen-ui/src/app/features/dashboard/dashboard.html`](file:///d:/CanteenManagementSystem/CanteenFrontend/canteen-ui/src/app/features/dashboard/dashboard.html#L22-L125)
- **My Canteens Template & Sidebar:** [`CanteenFrontend/canteen-ui/src/app/features/canteens/my-canteens/my-canteens.html`](file:///d:/CanteenManagementSystem/CanteenFrontend/canteen-ui/src/app/features/canteens/my-canteens/my-canteens.html#L22-L105)
- **Test Project:** [`CanteenManagementSystem/tests/Canteen.Tests/Canteen.Tests.csproj`](file:///d:/CanteenManagementSystem/CanteenManagementSystem/tests/Canteen.Tests/Canteen.Tests.csproj)
