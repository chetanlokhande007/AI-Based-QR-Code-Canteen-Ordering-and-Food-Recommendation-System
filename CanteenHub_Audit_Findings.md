# CANTEENHUB — AUDIT FINDINGS LOG & REMEDIATION PLAN

**Date:** October 28, 2026  
**Auditor:** Senior Software Architect & Security Auditor  
**Repository:** `D:\CanteenManagementSystem`  
**Git Branch:** `feature/multi-canteen-management`  

---

## FINDINGS SUMMARY DASHBOARD

| Finding ID | Severity | Category | Title | Affected File / Component | Remediation Priority |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **SEC-001** | **CRITICAL** | Security / Data Isolation | Unauthenticated Public Exposure of All Canteens & Owner Identities | `CanteensController.cs:24-28` | **P0** |
| **ARCH-001**| **CRITICAL** | Architecture / Routing | Missing Customer QR Ordering Web Portal (`/menu`) | `app.routes.ts:8-16` | **P0** |
| **DATA-001**| **HIGH** | Data Integrity / Concurrency | Missing DB Transaction Around Multi-Item Stock Deductions | `OrderService.cs:92-173` | **P0** |
| **SEC-002** | **HIGH** | Authorization / Multi-Tenant | Inconsistent Multi-Canteen Authorization in Payments Controller | `PaymentsController.cs:127-133` | **P1** |
| **PERF-001**| **HIGH** | Performance / DB Storage | Uncompressed High-Volume Base64 Image Storage in Relational DB | `canteen-setup.ts:139-160` | **P1** |
| **UI-001**  | **MEDIUM** | Frontend / UX | Non-Functional "Phantom" Navigation in Owner Sidebar | `dashboard.html:73-125` | **P2** |
| **FEAT-001**| **MEDIUM** | Business Logic / Metrics | Dashboard Computes Revenue from Unpaid / Cancelled Orders | `dashboard.ts:98-115` | **P2** |
| **PERF-002**| **MEDIUM** | Database / Query Efficiency | Unbounded `GetAllByCanteenAsync` Lacks Pagination | `OrderRepository.cs:55-75` | **P3** |
| **OPS-001** | **MEDIUM** | DevOps / Readiness | Missing Containerization (Dockerfile / Compose) & CI Pipelines | Root Repository | **P3** |
| **CFG-001** | **MEDIUM** | Configuration / Security | Hardcoded Secrets and Localhost URLs in Production Configurations | `environment.ts`, `appsettings.json` | **P3** |

---

## DETAILED FINDINGS LOG

### [CRITICAL] SEC-001: Unauthenticated Public Exposure of All Canteens & Owner Identities
- **Finding ID:** SEC-001
- **Title:** Unauthenticated Public Exposure of All Canteens & Owner Identities
- **Severity:** CRITICAL
- **Confidence Level:** Confirmed
- **Affected Module:** Backend Canteen API
- **Evidence:** [`CanteenManagementSystem/Controllers/CanteensController.cs:24-28`](file:///d:/CanteenManagementSystem/CanteenManagementSystem/Controllers/CanteensController.cs#L24-L28)
  ```csharp
  [HttpGet]
  [ProducesResponseType(typeof(IEnumerable<CanteenDto>), StatusCodes.Status200OK)]
  public async Task<IActionResult> GetAll()
      => Ok(await _service.GetAllAsync());
  ```
- **Explanation:** The endpoint `GET /api/canteens` has no authentication or authorization attribute. Calling it anonymously returns every canteen record in the PostgreSQL database, exposing internal primary keys (`CanteenId`), owning `OwnerId`, direct contact phone numbers, emails, addresses, and large Base64 images.
- **Business Impact:** High-risk confidential business data leak. An attacker can enumerate all owners, scrape contact details, and calculate company-wide tenant numbers.
- **Reproduction Method:** Run `curl http://localhost:5151/api/canteens` without any Authorization header. It returns HTTP 200 with the full array of canteens.
- **Remediation:** 
  1. Add `[Authorize(Roles = "SuperAdmin")]` to `GET /api/canteens`.
  2. Create a safe, public endpoint `GET /api/canteens/{id}/public-info` returning strictly `CanteenId`, `Name`, `Location`, and `OperatingHoursJson` for the QR ordering screen.
- **Regression Risks:** None for owner dashboard, as the dashboard uses `GET /api/canteens/my` (`[Authorize(Roles = "CanteenOwner")]`).
- **Suggested Test:** In `tests/Canteen.Tests/ApiIntegrationTests.cs`, assert `GET /api/canteens` returns 401 Unauthorized for anonymous requests.

---

### [CRITICAL] ARCH-001: Missing Customer QR Ordering Web Portal (`/menu`)
- **Finding ID:** ARCH-001
- **Title:** Missing Customer QR Ordering Web Portal (`/menu`)
- **Severity:** CRITICAL
- **Confidence Level:** Confirmed
- **Affected Module:** Angular Frontend Routing & Customer Ordering
- **Evidence:** 
  - [`CanteenFrontend/canteen-ui/src/app/app.routes.ts:8-16`](file:///d:/CanteenManagementSystem/CanteenFrontend/canteen-ui/src/app/app.routes.ts#L8-L16)
  - [`CanteenManagementSystem/appsettings.json:26`](file:///d:/CanteenManagementSystem/CanteenManagementSystem/appsettings.json#L26)
  - [`Canteen.BAL/Services/QrCodeService.cs:20`](file:///d:/CanteenManagementSystem/CanteenManagementSystem/Canteen.BAL/Services/QrCodeService.cs#L20)
- **Explanation:** The QR codes generated for physical tables encode URLs such as `http://localhost:4200/menu?canteenId=2&tableId=5&token=abc123xyz`. In Angular's router configuration, route `/menu` does not exist. Scanning any table QR code causes the browser to trigger the catch-all wildcard `** -> /login`. Furthermore, no customer-facing menu browsing, cart, or order placement components exist in `src/app/features/`.
- **Business Impact:** The primary purpose of the application (customer table-side ordering via QR code) is inoperative in the web application.
- **Reproduction Method:** Open `http://localhost:4200/menu?canteenId=2&tableId=1&token=test` in a browser. Observe immediate redirection to `http://localhost:4200/login`.
- **Remediation:** Build a standalone `CustomerMenu` component at path `/menu`. Read query params (`canteenId`, `tableId`, `token`), load categories and products from `GET /api/canteens/{id}/categories` and `products`, implement a responsive cart drawer, and submit orders via `POST /api/orders?qrToken={token}`.
- **Regression Risks:** None to owner features.
- **Suggested Test:** Vitest component test asserting `/menu` loads without requiring authentication.

---

### [HIGH] DATA-001: Missing DB Transaction Around Multi-Item Stock Deductions
- **Finding ID:** DATA-001
- **Title:** Missing DB Transaction Around Multi-Item Stock Deductions
- **Severity:** HIGH
- **Confidence Level:** Confirmed
- **Affected Module:** Order Processing Service
- **Evidence:** [`Canteen.BAL/Services/OrderService.cs:92-173`](file:///d:/CanteenManagementSystem/CanteenManagementSystem/Canteen.BAL/Services/OrderService.cs#L92-L173)
- **Explanation:** Multi-item orders execute `DeductStockAsync` sequentially in a loop. If an exception occurs, a manual memory-based `RestoreStockAsync` loop runs. However, if the process terminates, database connectivity drops, or `_orderRepo.AddAsync` throws an exception, stock already deducted is never rolled back.
- **Business Impact:** Ghost inventory loss, unfulfilled orders, and corrupted stock numbers during high concurrency or network disruptions.
- **Reproduction Method:** Submit an order with multiple items where persistence fails midway. Observe that earlier items have deducted stock in the `Products` table with no corresponding `Order` record.
- **Remediation:** Enclose the entire loop and order persistence in an EF Core `IDbContextTransaction`:
  ```csharp
  await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
  // ... deduct stock ...
  // ... save order ...
  await tx.CommitAsync();
  ```
- **Regression Risks:** Minimal; ensures ACID consistency.
- **Suggested Test:** Add unit test in `tests/Canteen.Tests/StockConcurrencyTests.cs` verifying rollback when order persistence throws.

---

### [HIGH] SEC-002: Inconsistent Multi-Canteen Authorization in Payments Controller
- **Finding ID:** SEC-002
- **Title:** Inconsistent Multi-Canteen Authorization in Payments Controller
- **Severity:** HIGH
- **Confidence Level:** Confirmed
- **Affected Module:** Backend Payments Controller
- **Evidence:** [`CanteenManagementSystem/Controllers/PaymentsController.cs:127-133`](file:///d:/CanteenManagementSystem/CanteenManagementSystem/Controllers/PaymentsController.cs#L127-L133)
  ```csharp
  var ownerCanteenId = User.GetCanteenId();
  if (ownerCanteenId == null || ownerCanteenId.Value != canteenId)
  {
      return Forbid();
  }
  ```
- **Explanation:** In `PaymentsController.cs:129`, the controller checks only the `CanteenId` claim in the caller's JWT. Unlike `CategoriesController`, `ProductsController`, `TablesController`, and `OrdersController`, it does not check whether the authenticated owner owns the canteen via `_canteenService.IsOwnerAuthorizedAsync(ownerId, canteenId)`.
- **Business Impact:** An owner managing multiple canteens who queries payments for a canteen different from their token's active claim receives a false `403 Forbidden` response.
- **Reproduction Method:** Log in as an owner with Canteen 1 and Canteen 2. Without calling switch-canteen, issue a request to `GET /api/canteens/2/orders/{id}/payment`. Observe 403 Forbidden.
- **Remediation:** Refactor `PaymentsController` to inject `ICanteenService` and call `await _canteenService.IsOwnerAuthorizedAsync(ownerId, canteenId)`.
- **Regression Risks:** None.
- **Suggested Test:** Add integration test in `tests/Canteen.Tests/PaymentTests.cs` validating cross-canteen payment retrieval by legitimate owner.

---

### [HIGH] PERF-001: High-Volume Base64 Image Storage in Relational DB
- **Finding ID:** PERF-001
- **Title:** High-Volume Base64 Image Storage in Relational DB
- **Severity:** HIGH
- **Confidence Level:** Confirmed
- **Affected Module:** Canteen Setup & Image Storage
- **Evidence:** [`CanteenFrontend/canteen-ui/src/app/features/canteen-setup/canteen-setup.ts:139-160`](file:///d:/CanteenManagementSystem/CanteenFrontend/canteen-ui/src/app/features/canteen-setup/canteen-setup.ts#L139-L160)
- **Explanation:** Canteen setup encodes user photos into raw base64 data URLs (up to 5MB) and stores them directly in `Canteens.ImageUrl` in PostgreSQL. Live API queries return over 1,400 lines of base64 text for single records.
- **Business Impact:** Bloats database table size, degrades PostgreSQL buffer cache, and consumes substantial network bandwidth on every API query.
- **Remediation:** Implement file upload endpoint `POST /api/files/upload` returning static URL paths. Store images on disk or cloud object storage.
- **Regression Risks:** Existing base64 images in database should remain backward-compatible until migrated.
- **Suggested Test:** Verify `ImageUrl` can accept both static URLs (`/uploads/photo.jpg`) and legacy base64 strings.

---

### [MEDIUM] UI-001: Non-Functional "Phantom" Navigation in Owner Sidebar
- **Finding ID:** UI-001
- **Title:** Non-Functional "Phantom" Navigation in Owner Sidebar
- **Severity:** MEDIUM
- **Confidence Level:** Confirmed
- **Affected Module:** Angular Frontend Dashboard Navigation
- **Evidence:** 
  - [`dashboard.html:73-125`](file:///d:/CanteenManagementSystem/CanteenFrontend/canteen-ui/src/app/features/dashboard/dashboard.html#L73-L125)
  - [`my-canteens.html:70-100`](file:///d:/CanteenManagementSystem/CanteenFrontend/canteen-ui/src/app/features/canteens/my-canteens/my-canteens.html#L70-L100)
- **Explanation:** Sidebar links for *Tables & QR Codes*, *Orders*, *Kitchen Display*, *Payments*, *Reports*, *Staff*, and *Inventory* have no corresponding Angular components. Clicking "Tables & QR Codes" opens the Category modal; other links are inert or redirect to `/dashboard`.
- **Business Impact:** Severe UX confusion for canteen owners attempting to manage kitchen operations, tables, or orders.
- **Remediation:** Provide real components for core features (Tables and KDS) or display explicit "Under Construction / Coming Soon" indicators.

---

### [MEDIUM] FEAT-001: Dashboard Computes Revenue from Unpaid & Cancelled Orders
- **Finding ID:** FEAT-001
- **Title:** Dashboard Computes Revenue from Unpaid & Cancelled Orders
- **Severity:** MEDIUM
- **Confidence Level:** Confirmed
- **Affected Module:** Angular Frontend Dashboard Metrics
- **Evidence:** [`dashboard.ts:98-106`](file:///d:/CanteenManagementSystem/CanteenFrontend/canteen-ui/src/app/features/dashboard/dashboard.ts#L98-L106)
  ```typescript
  readonly todayRevenue = computed(() => {
    const list = this.orders();
    const todayOrders = list.filter((o) => this.isOrderFromToday(o.createdAt));
    return todayOrders.reduce((sum, o) => sum + (o.totalAmount || 0), 0);
  });
  ```
- **Explanation:** `todayRevenue` filters orders strictly by date. If an order is in status `Cancelled` or `PaymentStatus == "Pending"`, its amount is still summed into the owner's displayed revenue metric.
- **Business Impact:** Misleads owners by inflating revenue with cancelled or abandoned orders.
- **Remediation:** Update filter condition to:
  `o.paymentStatus === 'Paid' || (o.status !== 'Cancelled' && o.status !== 'Pending')`.

---

## PRIORITIZED REMEDIATION ROADMAP

```
+────────────────────────────────────────────────────────────────────────────+
| PHASE 1: IMMEDIATE CRITICAL REPAIR (P0)                                    |
| 1. [ARCH-001] Build Customer QR Ordering Portal (/menu)                    |
| 2. [SEC-001] Restrict GET /api/canteens and create public info endpoint    |
| 3. [DATA-001] Enclose OrderService.CreateAsync in DB Transaction           |
+────────────────────────────────────────────────────────────────────────────+
                                      │
                                      ▼
+────────────────────────────────────────────────────────────────────────────+
| PHASE 2: SECURITY & STORAGE IMPROVEMENTS (P1)                              |
| 1. [SEC-002] Unify multi-canteen authorization in PaymentsController       |
| 2. [PERF-001] Externalize image uploads to disk/cloud storage              |
| 3. [FEAT-001] Filter revenue calculations by paid/active status            |
+────────────────────────────────────────────────────────────────────────────+
                                      │
                                      ▼
+────────────────────────────────────────────────────────────────────────────+
| PHASE 3: OPERATIONAL CORE SCREENS (P2)                                     |
| 1. [UI-001] Build Tables & QR Code management view (/tables)               |
| 2. [UI-001] Build Kitchen Display System view (/kds)                       |
| 3. Clean up sidebar navigation links in Dashboard & My Canteens            |
+────────────────────────────────────────────────────────────────────────────+
                                      │
                                      ▼
+────────────────────────────────────────────────────────────────────────────+
| PHASE 4: PRODUCTION PACKAGING & CONFIGURATION (P3)                         |
| 1. [OPS-001] Add Dockerfile & docker-compose for backend and frontend      |
| 2. [CFG-001] Setup environment.prod.ts and appsettings production configs   |
| 3. [PERF-002] Add pagination to Orders API and PostgreSQL indexes          |
+────────────────────────────────────────────────────────────────────────────+
```
