# PottaAPI - Local POS API for Mobile Integration

## Overview
PottaAPI is a local ASP.NET Core Web API that enables mobile apps to integrate with your WPF POS system. It provides endpoints for menu synchronization, staff authentication, order management, and table service.

## Features

### 🍽️ Menu Management
- **Products**: Get all active menu items with pricing, inventory, and images
- **Categories**: Retrieve product categories for menu organization
- **Bundles**: Access bundle items with component details
- **Variations**: Fetch product variations (sizes, colors, etc.)

### 👥 Staff Authentication
- **Daily Codes**: Staff login using rotating daily codes from main POS
- **Code Validation**: Automatic expiration after 24 hours
- **Staff Info**: Access to staff details and permissions

### 🪑 Table Service
- **Table Status**: View and update table availability
- **Reservations**: Manage table reservations
- **Customer Assignment**: Link customers to tables

### 📱 Order Management
- **Mobile Orders**: Create orders from mobile apps
- **Waiting Transactions**: Queue orders for kitchen/preparation
- **Order Status**: Track order progress (Pending → Processing → Ready → Completed)

### 🔄 Data Synchronization
- **Full Sync**: Complete data download for offline mobile apps
- **Health Check**: API status and database connectivity
- **Sync Info**: Data counts and timestamps

## API Endpoints

### Menu & Products
```
GET /api/menu/items          - Get all menu items
GET /api/menu/categories     - Get all categories
GET /api/menu/bundles        - Get all bundle items
GET /api/menu/variations     - Get all product variations
GET /api/menu/sync          - Get complete menu data
```

### Staff Authentication
```
GET /api/staff              - Get all active staff
POST /api/staff/login       - Validate staff daily code
GET /api/staff/codes        - Get staff codes for sync
```

### Tables
```
GET /api/tables             - Get all tables
GET /api/tables/available   - Get available tables only
PUT /api/tables/{id}/status - Update table status
```

### Orders
```
POST /api/orders            - Create new order
GET /api/orders/waiting     - Get all waiting transactions
GET /api/orders/pending     - Get pending orders only
PUT /api/orders/waiting/{id}/status - Update order status
DELETE /api/orders/waiting/{id} - Delete order
```

### Customers
```
GET /api/customers          - Get all customers
GET /api/customers/{id}     - Get specific customer
```

### Synchronization
```
GET /api/sync/info          - Get sync information
GET /api/sync/full          - Get complete sync data
GET /api/sync/health        - Health check
```

## Setup Instructions

### 1. Prerequisites
- .NET 8.0 SDK
- Access to your WPF POS SQLite database

### 2. Installation
```bash
cd "c:\Users\Thuram Jr\source\repos\PottaPOS\Potta Finance\PottaAPI"
dotnet restore
dotnet build
```

### 3. Configuration
The API automatically connects to your existing SQLite database (`pottadb.db`) in the WPF application directory.

### 4. Running the API
```bash
dotnet run
```
The API will start on `http://localhost:5001`

### 5. Testing
Visit `http://localhost:5001/swagger` to see the interactive API documentation.

### 6. Access Points
- **API Base URL**: `http://localhost:5001`
- **Swagger UI**: `http://localhost:5001/swagger`
- **Health Check**: `http://localhost:5001/api/sync/health`

## Mobile App Integration

### Authentication Flow
1. Mobile app requests staff daily codes: `GET /api/staff/codes`
2. Staff enters their daily code in mobile app
3. App validates code: `POST /api/staff/login`
4. If valid, staff can access POS functions

### Data Sync Strategy
1. **Initial Sync**: `GET /api/sync/full` - Download all data
2. **Periodic Sync**: `GET /api/sync/info` - Check for updates
3. **Health Check**: `GET /api/sync/health` - Verify connectivity

### Order Flow
1. Mobile app creates order: `POST /api/orders`
2. Order appears in WaitingTransactions table
3. Main POS system processes the order
4. Status updates via: `PUT /api/orders/waiting/{id}/status`

## Offline-First Mobile Strategy

### Recommended Architecture
```
Mobile App (Android/iOS)
├── Local SQLite Database (Mirror of POS)
├── Sync Service (Background)
├── Order Queue (Local storage)
└── Network Manager (Connectivity detection)
```

### Sync Process
1. **Download Data**: Menu, categories, staff codes, tables, customers
2. **Store Locally**: SQLite database in mobile app
3. **Work Offline**: Create orders locally
4. **Upload When Connected**: Sync pending orders to main POS

### Benefits
- ✅ Works without internet connection
- ✅ Fast performance (local data)
- ✅ Automatic sync when network available
- ✅ No dependency on cloud services
- ✅ Data privacy (local network only)

## Security Considerations

### Network Security
- API runs on local network only
- No external internet access required
- Staff authentication via daily rotating codes

### Data Protection
- All data stays within your local network
- No cloud storage or external APIs
- Direct database access from trusted devices only

## Troubleshooting

### Common Issues
1. **Database Connection Failed**
   - Ensure WPF app database exists
   - Check file permissions
   - Verify database path

2. **API Not Accessible**
   - Check Windows Firewall settings
   - Ensure port 5000 is available
   - Verify network connectivity

3. **Staff Code Invalid**
   - Codes expire after 24 hours
   - Generate new codes in main POS system
   - Check staff is active

### Logs
API logs are displayed in the console when running. Check for error messages and connection status.

## Development

### Adding New Endpoints
1. Create DTO models in `Models/DTOs.cs`
2. Add service methods in `Services/DatabaseService.cs`
3. Create controller in `Controllers/`
4. Update interface in `Services/IDatabaseService.cs`

### Database Schema
The API uses your existing SQLite database schema. Key tables:
- `Products` - Menu items
- `BundleItems` - Bundle products
- `ProductVariations` - Product variants
- `Categories` - Product categories
- `Staff` - Staff with daily codes
- `Tables` - Restaurant tables
- `Customer` - Customer information
- `WaitingTransactions` - Mobile orders

## Support
For issues or questions, refer to the main POS system documentation or contact your system administrator.
