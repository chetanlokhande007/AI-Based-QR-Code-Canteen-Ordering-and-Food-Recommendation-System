# AI-Based QR Code Canteen Ordering and Food Recommendation System

An enterprise-ready, multi-tenant QR-code-based canteen ordering and menu management backend API built with **ASP.NET Core (.NET 10)**, **Entity Framework Core**, **PostgreSQL (Npgsql)**, and **JWT Bearer Authentication**.

---

## 🚀 Features

- **Multi-Tenant Security Architecture**: Strict isolation preventing Broken Object-Level Authorization (BOLA / IDOR) across canteen owners.
- **Role-Based JWT Authentication**: Secure HMAC-SHA256 token issuance with PBKDF2 password hashing (100,000 iterations + cryptographically secure salt).
- **Canteen & Menu Management**: Full CRUD operations for canteens, categories, and products with price & stock integrity constraints.
- **QR-Code Table Ordering**: Unique QR tokens mapped per table for frictionless customer ordering.
- **Centralized Error Handling**: Production-hardened global exception middleware preventing sensitive stack trace leaks.
- **Config-Driven CORS**: Flexible cross-origin resource sharing policy for seamless frontend integration.
- **Input Validation**: Robust DataAnnotations on all request DTOs enforcing length, regex, and bounds constraints.

---

## 🏗️ Architecture

The solution follows clean multi-tier (N-Tier) separation of concerns:

```
CanteenManagementSystem/
├── CanteenManagementSystem/      # Presentation Layer (Controllers, Middleware, Extensions, Swagger)
├── Canteen.BAL/                  # Business Access Layer (Services, Business Rules, Interfaces)
├── Canteen.DAL/                  # Data Access Layer (EF Core DbContext, Repositories, Migrations)
└── Canteen.Model/                # Domain Entities and Data Transfer Objects (DTOs)
```

---

## 📋 Prerequisites

- **[.NET 10.0 SDK](https://dotnet.microsoft.com/download)**
- **[PostgreSQL 14+](https://www.postgresql.org/download/)**
- **Git**

---

## ⚙️ Configuration & Setup

### 1. Clone the Repository
```bash
git clone https://github.com/chetanlokhande007/AI-Based-QR-Code-Canteen-Ordering-and-Food-Recommendation-System.git
cd AI-Based-QR-Code-Canteen-Ordering-and-Food-Recommendation-System
```

### 2. Configure Local Secrets (`appsettings.Development.json`)
The committed `appsettings.json` contains non-sensitive templates. Create an `appsettings.Development.json` file in the root project directory (this file is git-ignored):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=CanteenManagementDb;Username=postgres;Password=YOUR_LOCAL_DB_PASSWORD"
  },
  "Jwt": {
    "Key": "YOUR_STRONG_SECRET_KEY_AT_LEAST_32_BYTES_LONG",
    "Issuer": "CanteenManagementSystem",
    "Audience": "CanteenManagementUsers",
    "ExpiryMinutes": "60"
  },
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "http://localhost:5173"
    ]
  }
}
```

### 3. Apply Database Migrations
Ensure PostgreSQL is running and your database is created, then apply EF Core migrations:

```bash
dotnet ef database update --project Canteen.DAL --startup-project CanteenManagementSystem
```

### 4. Run the API
```bash
dotnet run --project CanteenManagementSystem
```

Navigate to:
- **Swagger UI**: `https://localhost:7198/swagger` (or `http://localhost:5177/swagger`)

---

## 📡 API Reference

### 🔐 Authentication (`/api/auth`)
| Method | Endpoint | Access | Description |
|---|---|---|---|
| `POST` | `/api/auth/register` | Public | Register a new canteen owner with unassigned canteen (`CanteenId = null`, Returns 201 Created) |
| `POST` | `/api/auth/login` | Public | Authenticate owner and receive JWT token (includes `CanteenId` claim if assigned) |

### 🏬 Canteens (`/api/canteens`)
| Method | Endpoint | Access | Description |
|---|---|---|---|
| `GET` | `/api/canteens` | Public | List all active canteens |
| `GET` | `/api/canteens/{id}` | Public | Get details of a specific canteen |
| `POST` | `/api/canteens` | CanteenOwner | Create a new canteen (Binds to caller's owner account) |
| `PUT` | `/api/canteens/{id}` | CanteenOwner | Update canteen (Caller must own the canteen) |
| `DELETE` | `/api/canteens/{id}` | CanteenOwner | Remove canteen (Caller must own the canteen) |

### 📂 Categories (`/api/canteens/{canteenId}/categories`)
| Method | Endpoint | Access | Description |
|---|---|---|---|
| `GET` | `/api/canteens/{canteenId}/categories` | Public | List categories for a canteen |
| `GET` | `/api/canteens/{canteenId}/categories/{categoryId}` | Public | Get category details |
| `POST` | `/api/canteens/{canteenId}/categories` | CanteenOwner | Create category for caller's canteen |
| `PUT` | `/api/canteens/{canteenId}/categories/{categoryId}` | CanteenOwner | Update category (Tenant verified) |
| `DELETE` | `/api/canteens/{canteenId}/categories/{categoryId}` | CanteenOwner | Delete category (Tenant verified) |

### 🍔 Products (`/api/products`)
| Method | Endpoint | Access | Description |
|---|---|---|---|
| `GET` | `/api/products/canteen/{canteenId}` | Public | List products for a canteen |
| `GET` | `/api/products/canteen/{canteenId}/category/{categoryId}` | Public | List products for a category |
| `GET` | `/api/products/{id}` | Public | Get product details |
| `POST` | `/api/products` | CanteenOwner | Add product to caller's canteen (Category verified) |
| `PUT` | `/api/products/{id}` | CanteenOwner | Update product details/stock (Tenant & category verified) |
| `DELETE` | `/api/products/{id}` | CanteenOwner | Delete product (Tenant verified) |

### 🪑 Tables & QR Codes (`/api/canteens/{canteenId}/tables`)
| Method | Endpoint | Access | Description |
|---|---|---|---|
| `GET` | `/api/canteens/{canteenId}/tables` | Public | List tables for a canteen |
| `GET` | `/api/canteens/{canteenId}/tables/{tableId}` | Public | Get table details |
| `GET` | `/api/canteens/{canteenId}/tables/qr/{token}` | Public | Customer QR code lookup to load table menu |
| `POST` | `/api/canteens/{canteenId}/tables` | CanteenOwner | Add a table to caller's canteen (Unique table number verified) |
| `PUT` | `/api/canteens/{canteenId}/tables/{tableId}` | CanteenOwner | Update table details (Unique table number verified) |
| `DELETE` | `/api/canteens/{canteenId}/tables/{tableId}` | CanteenOwner | Delete table (Tenant verified) |

---

## 🛡️ Security Highlights

1. **Anti-BOLA/IDOR Enforcement**: All mutation operations retrieve `CanteenId` directly from the authenticated JWT claims (`User.GetCanteenId()`) rather than client-supplied payload parameters, strictly preventing cross-tenant modifications.
2. **Safe Secrets**: Environment configs and database credentials are excluded from source control.
3. **Structured Global Error Responses**: Unhandled exceptions are logged and caught by ASP.NET Core exception handlers returning uniform JSON error responses without leaking internal query or stack trace details.

---

## 📄 License
This project is licensed under the MIT License.
