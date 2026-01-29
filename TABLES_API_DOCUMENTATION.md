# Tables API Documentation

## Overview
Complete table and seat management API extracted from the WPF Dashboard functionality. This API provides full restaurant table service capabilities including table status management, seat selection, and transaction tracking.

## Files Created

### 1. Models/TableDTOs.cs
**Purpose**: Data Transfer Objects for tables and seats

**DTOs Included**:
- `TableDTO` - Complete table information
- `CreateTableDTO` - Create new table
- `UpdateTableDTO` - Update table properties
- `UpdateTableStatusDTO` - Change table status
- `ReserveTableDTO` - Reserve a table
- `SeatDTO` - Seat information
- `CreateSeatsDTO` - Create seats for a table
- `UpdateSeatStatusDTO` - Change seat status
- `SelectSeatsDTO` - Select multiple seats
- `TableWithSeatsDTO` - Table with all seats
- `TableAvailabilityDTO` - Table availability summary

### 2. Services/ITableService.cs
**Purpose**: Service interface defining all table operations

**Key Methods**:
- Table CRUD operations
- Table status management (Available, Occupied, Unpaid, Reserved, etc.)
- Seat management
- Transaction-based status updates
- Combined operations (table with seats, availability)

### 3. Services/TableService.cs
**Purpose**: Implementation of table service with database operations

**Features**:
- Async database operations
- Automatic seat creation/management
- Transaction-aware status updates
- Capacity-based seat management
- Comprehensive error handling

### 4. Controllers/TablesController.cs
**Purpose**: REST API endpoints for table management

**Endpoint Groups**:
1. **Table Endpoints** (CRUD)
2. **Table Status Endpoints** (status management)
3. **Seat Endpoints** (seat management)
4. **Combined Endpoints** (complex queries)
5. **Initialization** (setup)

## API Endpoints

### Table Management

#### Get All Tables
```http
GET /api/tables
```
Returns all active tables with their current status.

#### Get Available Tables
```http
GET /api/tables/available
```
Returns only tables with "Available" status.

#### Get Table by ID
```http
GET /api/tables/{tableId}
```

#### Get Table by Number
```http
GET /api/tables/number/{tableNumber}
```

#### Create Table
```http
POST /api/tables
Content-Type: application/json

{
  "tableNumber": 1,
  "tableName": "Table 1",
  "capacity": 4,
  "description": "Main dining area",
  "size": "Medium",
  "shape": "Square"
}
```
**Note**: Automatically creates seats based on capacity.

#### Update Table
```http
PUT /api/tables/{tableId}
Content-Type: application/json

{
  "tableName": "VIP Table 1",
  "capacity": 6,
  "description": "VIP section"
}
```
**Note**: Adjusts seats if capacity changes.

#### Delete Table
```http
DELETE /api/tables/{tableId}
```
Soft delete - sets `isActive = 0`.

### Table Status Management

#### Update Table Status
```http
PATCH /api/tables/{tableId}/status
Content-Type: application/json

{
  "status": "Occupied",
  "customerId": "cust-123",
  "transactionId": "trans-456"
}
```
**Valid Statuses**: Available, Occupied, Unpaid, Reserved, Cleaning, OutOfOrder

#### Clear Table
```http
POST /api/tables/{tableId}/clear
```
Sets table to "Available" and clears customer/transaction info.

#### Reserve Table
```http
POST /api/tables/{tableId}/reserve
Content-Type: application/json

{
  "customerId": "cust-123",
  "reservationDate": "2024-01-30T19:00:00Z"
}
```

#### Set Table Not Available
```http
POST /api/tables/{tableId}/not-available
```

#### Set Table Unpaid
```http
POST /api/tables/{tableId}/unpaid?customerId=cust-123&transactionId=trans-456
```

#### Check Pending Transactions
```http
GET /api/tables/{tableId}/has-pending
```
Returns `true` if table has pending/delayed transactions.

#### Update Status from Transactions
```http
POST /api/tables/{tableId}/update-status-from-transactions
```
Automatically sets table to "Unpaid" if pending transactions exist, otherwise "Available".

#### Update All Tables Status
```http
POST /api/tables/update-all-status-from-transactions
```
Updates all tables based on their pending transactions.

### Seat Management

#### Get Table Seats
```http
GET /api/tables/{tableId}/seats
```
Returns all active seats for a table.

#### Get Seat by ID
```http
GET /api/tables/seats/{seatId}
```

#### Create Seats
```http
POST /api/tables/seats
Content-Type: application/json

{
  "tableId": "table-123",
  "numberOfSeats": 6
}
```

#### Update Seat Status
```http
PATCH /api/tables/seats/{seatId}/status
Content-Type: application/json

{
  "status": "Occupied",
  "customerId": "cust-123"
}
```
**Valid Statuses**: Available, Occupied, Reserved, Not Available

#### Select Multiple Seats
```http
POST /api/tables/seats/select
Content-Type: application/json

{
  "tableId": "table-123",
  "seatNumbers": [1, 2, 3],
  "customerId": "cust-123"
}
```
Marks specified seats as "Occupied".

#### Clear Seat
```http
POST /api/tables/seats/{seatId}/clear
```
Sets seat to "Available".

#### Clear All Seats
```http
POST /api/tables/{tableId}/seats/clear-all
```
Clears all seats for a table.

### Combined Operations

#### Get Table with Seats
```http
GET /api/tables/{tableId}/with-seats
```
Returns table info plus all seats with occupancy counts.

**Response**:
```json
{
  "table": { /* TableDTO */ },
  "seats": [ /* SeatDTO[] */ ],
  "occupiedSeatsCount": 2,
  "availableSeatsCount": 2
}
```

#### Get Tables Availability
```http
GET /api/tables/availability
```
Returns availability summary for all tables.

**Response**:
```json
[
  {
    "tableId": "table-123",
    "displayName": "Table 1",
    "isAvailable": true,
    "totalSeats": 4,
    "availableSeats": 4,
    "occupiedSeats": 0,
    "status": "Available",
    "hasPendingTransactions": false
  }
]
```

### Initialization

#### Initialize Default Tables
```http
POST /api/tables/initialize
```
Creates 3 default tables (for testing/setup). Only runs if no tables exist.

## Database Schema

### Tables Table
```sql
CREATE TABLE Tables (
    tableId TEXT PRIMARY KEY,
    tableName TEXT,
    tableNumber INTEGER NOT NULL,
    capacity INTEGER DEFAULT 4,
    status TEXT DEFAULT 'Available',
    currentCustomerId TEXT,
    currentTransactionId TEXT,
    description TEXT,
    size TEXT,
    shape TEXT,
    reservationDate DATETIME,
    isActive BOOLEAN DEFAULT 1,
    createdDate DATETIME DEFAULT CURRENT_TIMESTAMP,
    modifiedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
    -- ... sync fields
);
```

### Seats Table
```sql
CREATE TABLE Seats (
    seatId TEXT PRIMARY KEY,
    tableId TEXT NOT NULL,
    seatNumber INTEGER NOT NULL,
    status TEXT DEFAULT 'Available',
    customerId TEXT,
    isActive BOOLEAN DEFAULT 1,
    createdDate DATETIME DEFAULT CURRENT_TIMESTAMP,
    modifiedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
    -- ... sync fields
    FOREIGN KEY (tableId) REFERENCES Tables(tableId) ON DELETE CASCADE,
    UNIQUE(tableId, seatNumber)
);
```

## Key Features

### 1. Automatic Seat Management
- Seats are automatically created when a table is created
- Seats are adjusted when table capacity changes
- Extra seats are deactivated (not deleted)

### 2. Transaction-Aware Status
- Tables track pending transactions
- Status automatically updates based on transaction state
- Prevents clearing tables with unpaid orders

### 3. Flexible Status System
**Table Statuses**:
- `Available` - Ready for customers
- `Occupied` - Currently in use
- `Unpaid` - Has pending transactions
- `Reserved` - Reserved for future use
- `Cleaning` - Being cleaned
- `OutOfOrder` - Not available for use

**Seat Statuses**:
- `Available` - Ready for customer
- `Occupied` - Customer seated
- `Reserved` - Reserved seat
- `Not Available` - Out of service

### 4. Multi-Seat Selection
- Select specific seats at a table
- Track individual seat occupancy
- Useful for large tables with partial occupancy

### 5. Comprehensive Availability Tracking
- Real-time availability status
- Seat occupancy counts
- Pending transaction awareness

## Integration with Dashboard

The API mirrors the WPF Dashboard functionality:

1. **Table Selection** - `GET /api/tables/available`
2. **Seat Selection** - `POST /api/tables/seats/select`
3. **Order Placement** - Updates table status to "Occupied"
4. **Payment** - Updates status based on pending transactions
5. **Table Clearing** - `POST /api/tables/{tableId}/clear`

## Usage Examples

### Complete Table Service Flow

```javascript
// 1. Get available tables
const tables = await fetch('/api/tables/available').then(r => r.json());

// 2. Select a table
const tableId = tables[0].tableId;

// 3. Select specific seats
await fetch('/api/tables/seats/select', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    tableId: tableId,
    seatNumbers: [1, 2],
    customerId: 'cust-123'
  })
});

// 4. Update table status to occupied
await fetch(`/api/tables/${tableId}/status`, {
  method: 'PATCH',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    status: 'Occupied',
    customerId: 'cust-123',
    transactionId: 'trans-456'
  })
});

// 5. After payment, update based on pending transactions
await fetch(`/api/tables/${tableId}/update-status-from-transactions`, {
  method: 'POST'
});

// 6. If no pending transactions, table is automatically cleared
```

### Check Table Availability

```javascript
// Get detailed availability for all tables
const availability = await fetch('/api/tables/availability').then(r => r.json());

// Find tables with at least 4 available seats
const largeTables = availability.filter(t => t.availableSeats >= 4);
```

## Error Handling

All endpoints return consistent error responses:

```json
{
  "message": "Error description",
  "error": "Detailed error message"
}
```

**Common HTTP Status Codes**:
- `200 OK` - Success
- `201 Created` - Resource created
- `204 No Content` - Successful deletion
- `400 Bad Request` - Invalid input
- `404 Not Found` - Resource not found
- `500 Internal Server Error` - Server error

## Service Registration

Add to `Program.cs`:

```csharp
builder.Services.AddScoped<ITableService, TableService>();
```

## Testing

### Initialize Test Data
```http
POST /api/tables/initialize
```

### Verify Setup
```http
GET /api/tables
```

Should return 3 default tables with 4 seats each.

## Notes

1. **No Database Creation**: The API uses the existing database created by `SQLiteDatabaseHelper.cs`
2. **Async Operations**: All operations are async for better performance
3. **Soft Deletes**: Tables and seats are never hard-deleted
4. **Transaction Safety**: Status updates are transaction-aware
5. **Seat Consistency**: Seat count always matches table capacity

## Future Enhancements

Potential additions:
- Floor plan integration
- Table merging/splitting
- Waitlist management
- Reservation system
- Table rotation optimization
- Service time tracking
