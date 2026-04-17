# PottaAPI

A RESTful API server for Potta Finance, providing backend services for restaurant and retail point-of-sale operations. Built with ASP.NET Core 8.0 and SQLite.

## ⚠️ CRITICAL: Dependency on Potta Finance

**PottaAPI CANNOT run standalone!** It requires Potta Finance to be installed first because:

1. **Shared Database** - PottaAPI reads/writes to the SQLite database created by Potta Finance
2. **Shared Images** - Product images are stored in Potta Finance's directory
3. **Database Schema** - The database structure is created and managed by Potta Finance

**Installation Order:**
1. ✅ Install/Clone Potta Finance FIRST
2. ✅ Run Potta Finance at least once to create the database
3. ✅ Then install/clone PottaAPI
4. ✅ Configure PottaAPI to point to Potta Finance's database location

**Quick Setup for Developers:**
```bash
# 1. Clone both in same directory
mkdir C:\Projects && cd C:\Projects
git clone <repo> "Potta Finance"
git clone <repo> PottaAPI

# 2. Run Potta Finance first (creates database)
cd "Potta Finance"
dotnet run
# Complete setup, then close

# 3. Configure PottaAPI paths
cd ..\PottaAPI
# Edit appsettings.Development.json:
#   "SearchPaths": ["../Potta Finance/Database"]
#   "ImageBasePath": "../Potta Finance/Database/Images"

# 4. Run PottaAPI
dotnet run
```

## Overview

PottaAPI serves as the backend infrastructure for Potta Finance desktop and mobile applications, handling data synchronization, business logic, and providing a comprehensive REST API for all POS operations. It acts as a bridge between the desktop application and mobile devices, allowing multiple devices to access the same data.

## Features

- **RESTful API** - Complete REST API for POS operations
- **SQLite Database** - Lightweight, file-based database
- **Real-time Sync** - Synchronize data across multiple devices
- **Image Serving** - Static file serving for product images
- **Swagger Documentation** - Interactive API documentation
- **Validation** - FluentValidation for request validation
- **Rate Limiting** - IP-based rate limiting for security
- **CORS Support** - Configurable cross-origin resource sharing
- **Logging** - Serilog structured logging
- **Health Checks** - API health monitoring endpoints

## Technology Stack

- **.NET 8.0** - Modern .NET framework
- **ASP.NET Core** - Web API framework
- **SQLite** - Embedded database
- **Serilog** - Structured logging
- **FluentValidation** - Request validation
- **Swashbuckle** - Swagger/OpenAPI documentation
- **AspNetCoreRateLimit** - Rate limiting middleware

## Prerequisites

- **Potta Finance MUST be installed first** - This is non-negotiable!
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Windows, macOS, or Linux operating system
- Potta Finance database file (created by running Potta Finance)

## Installation

### Step 0: Install Potta Finance First (REQUIRED)

Before installing PottaAPI, you MUST:

1. Install Potta Finance desktop application
2. Run Potta Finance at least once
3. Complete initial setup (creates database)
4. Note the database location (usually `C:\ProgramData\Potta Finance\Database\PottaFinance.db`)

### Step 1: Clone the Repository

**IMPORTANT:** Clone PottaAPI in the SAME parent directory as Potta Finance for easier path configuration.

Recommended structure:
```
C:\Projects\
├── Potta Finance\          ← Clone this first
│   └── Database\
│       ├── PottaFinance.db
│       └── Images\
└── PottaAPI\               ← Clone this second
```

```bash
# Navigate to your projects directory
cd C:\Projects

# Clone Potta Finance first (if not already done)
git clone <potta-finance-repo-url> "Potta Finance"

# Run Potta Finance to create database
cd "Potta Finance"
dotnet run
# Complete setup, then close

# Clone PottaAPI in the same parent directory
cd ..
git clone <pottaapi-repo-url> PottaAPI
cd PottaAPI
```

### 2. Restore Dependencies

```bash
dotnet restore
```

### Step 3: Configure Database and Image Paths

This is the MOST CRITICAL step! PottaAPI must know where to find Potta Finance's database and images.

Copy the example configuration:

```bash
cp appsettings.json appsettings.Development.json
```

Edit `appsettings.Development.json` and configure paths:

#### Scenario A: Both Projects in Same Parent Directory (Recommended)

If your structure is:
```
C:\Projects\
├── Potta Finance\
└── PottaAPI\
```

Use relative paths:

```json
{
  "Database": {
    "FileName": "PottaFinance.db",
    "SearchPaths": [
      "../Potta Finance/Database",
      "C:\\ProgramData\\Potta Finance\\Database",
      "C:\\Users\\{YourUsername}\\AppData\\Local\\Potta Finance\\Database"
    ]
  },
  "Api": {
    "Port": 5000,
    "Title": "PottaAPI",
    "Version": "1.3.4",
    "Description": "REST API for Potta Finance POS System",
    "ImageBasePath": "../Potta Finance/Database/Images"
  }
}
```

#### Scenario B: Projects in Different Locations

If Potta Finance is installed elsewhere, use absolute paths:

```json
{
  "Database": {
    "FileName": "PottaFinance.db",
    "SearchPaths": [
      "C:\\ProgramData\\Potta Finance\\Database",
      "C:\\Users\\YourUsername\\AppData\\Local\\Potta Finance\\Database",
      "D:\\MyProjects\\Potta Finance\\Database"
    ]
  },
  "Api": {
    "ImageBasePath": "C:\\ProgramData\\Potta Finance\\Database\\Images"
  }
}
```

#### Scenario C: Development Setup

For development with both projects cloned:

```json
{
  "Database": {
    "SearchPaths": [
      "../Potta Finance/Database",
      "../Potta Finance/bin/Debug/net8.0-windows/Database",
      "C:\\ProgramData\\Potta Finance\\Database"
    ]
  },
  "Api": {
    "ImageBasePath": "../Potta Finance/Database/Images"
  }
}
```

### Step 4: Verify Database Location

Before running the API, verify the database exists:

**Windows:**
```powershell
# Check common locations
Test-Path "C:\ProgramData\Potta Finance\Database\PottaFinance.db"
Test-Path "$env:LOCALAPPDATA\Potta Finance\Database\PottaFinance.db"

# Or search for it
Get-ChildItem -Path C:\ -Filter "PottaFinance.db" -Recurse -ErrorAction SilentlyContinue
```

**Linux/Mac:**
```bash
# Search for database
find ~ -name "PottaFinance.db" 2>/dev/null
```

If database is not found:
1. Run Potta Finance application
2. Complete initial setup
3. Close Potta Finance
4. Search again for database file
5. Update `SearchPaths` in configuration

## Running the API

### Development Mode

```bash
dotnet run
```

Or with hot reload:

```bash
dotnet watch run
```

### Production Mode

```bash
dotnet run --configuration Release
```

### Build Executable

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

The executable will be in `bin/Release/net8.0/win-x64/publish/`

## API Endpoints

### Base URL

```
http://localhost:5000
```

### Documentation

- **Swagger UI**: `http://localhost:5000/swagger`
- **OpenAPI Spec**: `http://localhost:5000/swagger/v1/swagger.json`

### Health Checks

- `GET /health` - Overall health status
- `GET /health/ready` - Readiness check
- `GET /health/live` - Liveness check

### Core Endpoints

#### Customers
- `GET /api/customers` - List all customers
- `GET /api/customers/{id}` - Get customer by ID
- `POST /api/customers` - Create new customer
- `PUT /api/customers/{id}` - Update customer
- `DELETE /api/customers/{id}` - Delete customer

#### Items (Products)
- `GET /api/items` - List all items
- `GET /api/items/{id}` - Get item by ID
- `POST /api/items` - Create new item
- `PUT /api/items/{id}` - Update item
- `DELETE /api/items/{id}` - Delete item

#### Orders
- `GET /api/orders` - List all orders
- `GET /api/orders/{id}` - Get order by ID
- `POST /api/orders` - Create new order
- `PUT /api/orders/{id}` - Update order
- `DELETE /api/orders/{id}` - Delete order

#### Tables
- `GET /api/tables` - List all tables
- `GET /api/tables/{id}` - Get table by ID
- `POST /api/tables` - Create new table
- `PUT /api/tables/{id}/status` - Update table status
- `PUT /api/tables/{id}/seats/{seatNumber}/status` - Update seat status

#### Staff
- `GET /api/staff` - List all staff
- `GET /api/staff/{id}` - Get staff by ID
- `POST /api/staff/login` - Staff authentication

#### Floor Plans
- `GET /api/floorplans` - List all floor plans
- `GET /api/floorplans/{id}` - Get floor plan by ID
- `POST /api/floorplans` - Create floor plan
- `PUT /api/floorplans/{id}` - Update floor plan
- `DELETE /api/floorplans/{id}` - Delete floor plan

#### Restaurant Operations
- `POST /api/restaurant/move-order` - Move order between tables
- `POST /api/restaurant/transfer-server` - Transfer table to another server
- `POST /api/restaurant/shift-handover` - Perform shift handover
- `POST /api/restaurant/print-bill` - Print bill for table
- `POST /api/restaurant/pay-entire-bill` - Pay entire table bill
- `POST /api/restaurant/add-notes` - Add notes to order

#### Taxes
- `GET /api/taxes` - List all taxes
- `POST /api/taxes` - Create tax
- `PUT /api/taxes/{id}` - Update tax
- `DELETE /api/taxes/{id}` - Delete tax

#### Discounts
- `GET /api/discounts` - List all discounts
- `POST /api/discounts` - Create discount
- `PUT /api/discounts/{id}` - Update discount
- `DELETE /api/discounts/{id}` - Delete discount

#### Network
- `GET /api/network/info` - Get network information
- `GET /api/network/qr` - Get QR code for mobile connection

#### Images
- `GET /images/{filename}` - Serve product images

## Configuration

### Database Options

```json
{
  "Database": {
    "FileName": "PottaFinance.db",
    "SearchPaths": [
      "C:\\ProgramData\\Potta Finance\\Database",
      "C:\\Users\\{Username}\\AppData\\Local\\Potta Finance\\Database"
    ]
  }
}
```

### API Options

```json
{
  "Api": {
    "Port": 5000,
    "Title": "PottaAPI",
    "Version": "1.3.4",
    "Description": "REST API for Potta Finance POS System",
    "ImageBasePath": "../Potta Finance/Database/Images"
  }
}
```

### CORS Options

```json
{
  "Cors": {
    "PolicyName": "PottaPolicy",
    "AllowedOrigins": ["*"],
    "AllowedMethods": ["*"],
    "AllowedHeaders": ["*"],
    "AllowCredentials": false
  }
}
```

### Rate Limiting

```json
{
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": true,
    "StackBlockedRequests": false,
    "RealIpHeader": "X-Real-IP",
    "ClientIdHeader": "X-ClientId",
    "HttpStatusCode": 429,
    "GeneralRules": [
      {
        "Endpoint": "*",
        "Period": "1s",
        "Limit": 10
      },
      {
        "Endpoint": "*",
        "Period": "1m",
        "Limit": 100
      }
    ]
  }
}
```

### Logging

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/pottaapi-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7
        }
      }
    ]
  }
}
```

## Architecture

### Project Structure

```
PottaAPI/
├── Configuration/       # Configuration classes
│   ├── ApiOptions.cs
│   ├── CorsOptions.cs
│   └── DatabaseOptions.cs
├── Controllers/         # API controllers
│   ├── CustomersController.cs
│   ├── ItemsController.cs
│   ├── OrdersController.cs
│   ├── TablesController.cs
│   ├── StaffController.cs
│   ├── FloorPlansController.cs
│   ├── RestaurantOperationsController.cs
│   ├── TaxesController.cs
│   ├── DiscountsController.cs
│   └── NetworkController.cs
├── Models/              # DTOs and data models
│   ├── CustomerDTOs.cs
│   ├── ItemDTOs.cs
│   ├── OrderDTOs.cs
│   ├── TableDTOs.cs
│   ├── StaffDTOs.cs
│   ├── FloorPlanDTOs.cs
│   ├── RestaurantOperationsDTOs.cs
│   ├── TaxDTOs.cs
│   ├── DiscountDTOs.cs
│   └── NetworkDTOs.cs
├── Services/            # Business logic services
│   ├── DatabaseService.cs
│   ├── CustomerService.cs
│   ├── ItemService.cs
│   ├── OrderService.cs
│   ├── TableService.cs
│   ├── StaffService.cs
│   ├── FloorPlanService.cs
│   ├── RestaurantOperationsService.cs
│   ├── TaxService.cs
│   ├── DiscountService.cs
│   └── ConnectionStringProvider.cs
├── Validators/          # FluentValidation validators
│   ├── CreateFloorPlanValidator.cs
│   ├── UpdateTableStatusValidator.cs
│   ├── UpdateSeatStatusValidator.cs
│   ├── StaffLoginValidator.cs
│   ├── MoveOrderValidator.cs
│   ├── TransferServerValidator.cs
│   ├── ShiftHandoverValidator.cs
│   ├── PrintBillValidator.cs
│   └── AddNotesValidator.cs
├── Middleware/          # Custom middleware
│   └── GlobalExceptionHandlerMiddleware.cs
├── wwwroot/             # Static files
├── appsettings.json     # Configuration
└── Program.cs           # Application entry point
```

### Design Patterns

- **Dependency Injection** - Services registered in DI container
- **Repository Pattern** - Data access abstraction
- **DTO Pattern** - Data transfer objects for API
- **Middleware Pattern** - Request/response pipeline
- **Options Pattern** - Strongly-typed configuration

## How It Works

### 1. Database Connection

The API connects to the Potta Finance SQLite database:

1. Searches configured paths for database file
2. Establishes connection using connection string provider
3. Validates database schema on startup
4. Logs connection status

### 2. Request Processing

1. Client sends HTTP request
2. Rate limiting middleware checks request limits
3. CORS middleware validates origin
4. Request reaches controller
5. FluentValidation validates request data
6. Service layer processes business logic
7. Database operations executed
8. Response returned to client

### 3. Image Serving

Product images are served as static files:

1. Images stored in configured directory
2. Accessed via `/images/{filename}` endpoint
3. CORS headers added for cross-origin access
4. Cached for performance

### 4. Error Handling

Global exception handler catches all errors:

1. Logs error details
2. Returns standardized error response
3. Includes error code and message
4. Hides sensitive information in production

### 5. Validation

FluentValidation validates all requests:

1. Validator registered for each DTO
2. Automatic validation before controller action
3. Returns 400 Bad Request with validation errors
4. Detailed error messages for debugging

## Troubleshooting

### Database Not Found

**Error:** `Database file not found` or `✗ Database connection failed`

This is the most common error! Follow these steps:

**Step 1: Verify Potta Finance is Installed**
```powershell
# Windows - Check if Potta Finance exists
Test-Path "C:\Program Files\Potta Finance\Potta Finance.exe"
Test-Path "C:\ProgramData\Potta Finance"
```

**Step 2: Run Potta Finance to Create Database**
1. Launch Potta Finance application
2. Complete initial setup wizard
3. Create at least one product (to ensure database is populated)
4. Close Potta Finance
5. Database should now exist

**Step 3: Find the Database**
```powershell
# Windows - Search for database
Get-ChildItem -Path "C:\ProgramData" -Filter "PottaFinance.db" -Recurse -ErrorAction SilentlyContinue
Get-ChildItem -Path "$env:LOCALAPPDATA" -Filter "PottaFinance.db" -Recurse -ErrorAction SilentlyContinue
```

**Step 4: Update Configuration**

Once you find the database, update `appsettings.Development.json`:

```json
{
  "Database": {
    "SearchPaths": [
      "C:\\Actual\\Path\\To\\Database",  // ← Use the path you found
      "../Potta Finance/Database"
    ]
  }
}
```

**Step 5: Verify Paths are Correct**

The API logs will show where it's searching:
```
Database search paths: C:\ProgramData\Potta Finance\Database, C:\Users\...
```

Make sure one of these paths contains `PottaFinance.db`

**Step 6: Check File Permissions**
```powershell
# Windows - Check if API can read the database
icacls "C:\ProgramData\Potta Finance\Database\PottaFinance.db"
```

The API needs Read/Write permissions to the database file.

### Port Already in Use

**Error:** `Address already in use`

**Solution:**
1. Change port in `appsettings.json`
2. Stop other applications using port 5000
3. Use `netstat -ano | findstr :5000` to find process

### Images Not Loading

**Error:** `404 Not Found` for images or `✗ Desktop app image folder not found`

This means PottaAPI can't find Potta Finance's image directory.

**Step 1: Verify Images Exist in Potta Finance**
```powershell
# Windows - Check if images directory exists
Test-Path "C:\ProgramData\Potta Finance\Database\Images"
Get-ChildItem "C:\ProgramData\Potta Finance\Database\Images" | Select-Object -First 5
```

**Step 2: Check API Configuration**

The `ImageBasePath` in `appsettings.json` must point to Potta Finance's image folder:

```json
{
  "Api": {
    "ImageBasePath": "../Potta Finance/Database/Images"  // Relative path
    // OR
    "ImageBasePath": "C:\\ProgramData\\Potta Finance\\Database\\Images"  // Absolute path
  }
}
```

**Step 3: Verify Path Resolution**

When API starts, check the logs:
```
✓ Serving images from: C:\Full\Resolved\Path\To\Images
```

If you see:
```
✗ Desktop app image folder not found at: ...
```

Then the path is wrong. Update `ImageBasePath` to the correct location.

**Step 4: Test Image Access**

Once API is running:
```bash
# Test image endpoint
curl http://localhost:5000/images/test.png
```

**Common Path Issues:**

| Your Setup | Correct ImageBasePath |
|------------|----------------------|
| Both projects in `C:\Projects\` | `../Potta Finance/Database/Images` |
| Potta Finance installed | `C:\\ProgramData\\Potta Finance\\Database\\Images` |
| Development build | `../Potta Finance/bin/Debug/net8.0-windows/Database/Images` |

### CORS Errors

**Error:** `CORS policy blocked`

**Solution:**
1. Add client origin to `AllowedOrigins`
2. Use `"*"` for development (not recommended for production)
3. Ensure `AllowCredentials` matches client configuration

## Development

### Adding New Endpoint

1. Create DTO in `Models/`
2. Create validator in `Validators/`
3. Add service method in `Services/`
4. Create controller action in `Controllers/`
5. Register validator in `Program.cs`

### Testing API

Use Swagger UI for interactive testing:

```
http://localhost:5000/swagger
```

Or use curl:

```bash
# Get all customers
curl http://localhost:5000/api/customers

# Create customer
curl -X POST http://localhost:5000/api/customers \
  -H "Content-Type: application/json" \
  -d '{"name":"John Doe","email":"john@example.com"}'
```

## Security Considerations

- **Rate Limiting** - Prevents abuse and DoS attacks
- **Validation** - Prevents injection attacks
- **CORS** - Controls cross-origin access
- **Error Handling** - Hides sensitive information
- **Logging** - Audit trail for security events

## Performance

- **Connection Pooling** - SQLite connection reuse
- **Response Caching** - Cache frequently accessed data
- **Static File Caching** - Browser caching for images
- **Async Operations** - Non-blocking I/O operations

## Deployment

### Windows Service

Install as Windows service using NSSM or sc.exe:

```bash
sc create PottaAPI binPath="C:\Path\To\PottaAPI.exe"
sc start PottaAPI
```

### Docker (Optional)

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY bin/Release/net8.0/publish/ .
ENTRYPOINT ["dotnet", "PottaAPI.dll"]
```

### IIS (Optional)

1. Publish application
2. Install ASP.NET Core Hosting Bundle
3. Create IIS site pointing to publish folder
4. Configure application pool for .NET Core

## Version

Current Version: **1.3.4.5**

## License

Proprietary - Potta Finance Professional Edition

## Support

For support and questions, contact: support@pottafinance.com
