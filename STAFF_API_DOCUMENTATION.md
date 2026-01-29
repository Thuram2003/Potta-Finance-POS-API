# Staff API Documentation

## Overview
Complete RESTful API for staff management, authentication, and daily code generation. Extracted from the WPF application's `StaffDatabaseService` and follows the same business logic.

## Database Schema

### Staff Table
```sql
CREATE TABLE Staff (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FirstName TEXT NOT NULL,
    LastName TEXT NOT NULL,
    Email TEXT,
    Phone TEXT,
    Address TEXT,
    DateOfBirth TEXT,
    Gender TEXT,
    City TEXT,
    State TEXT,
    Country TEXT,
    DailyCode TEXT NOT NULL,
    CodeGeneratedDate DATETIME NOT NULL,
    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
    IsActive BOOLEAN DEFAULT 1
);
```

## API Endpoints

### 1. Get All Staff
Get all staff members (active and inactive).

**Endpoint:** `GET /api/staff`

**Response:**
```json
[
  {
    "id": 1,
    "firstName": "John",
    "lastName": "Doe",
    "fullName": "John Doe",
    "email": "john.doe@example.com",
    "phone": "+1234567890",
    "address": "123 Main St",
    "dateOfBirth": "1990-01-15",
    "gender": "Male",
    "city": "New York",
    "state": "NY",
    "country": "USA",
    "dailyCode": "1234",
    "codeGeneratedDate": "2026-01-29T10:00:00",
    "createdDate": "2025-01-01T00:00:00",
    "isActive": true,
    "isCodeExpired": false,
    "needsCodeRegeneration": false,
    "codeAge": "2 hour(s) ago"
  }
]
```

### 2. Get Filtered Staff
Get staff members with filtering options.

**Endpoint:** `GET /api/staff/filter`

**Query Parameters:**
- `isActive` (boolean, optional): Filter by active status
- `searchTerm` (string, optional): Search in name, email, phone
- `codeExpired` (boolean, optional): Filter by code expiration status

**Examples:**
```http
GET /api/staff/filter?isActive=true
GET /api/staff/filter?searchTerm=john
GET /api/staff/filter?codeExpired=true
GET /api/staff/filter?isActive=true&searchTerm=doe
```

**Response:** Same as Get All Staff

### 3. Get Active Staff
Get only active staff members.

**Endpoint:** `GET /api/staff/active`

**Response:** Same as Get All Staff (filtered to active only)

### 4. Get Staff by ID
Get specific staff member by ID.

**Endpoint:** `GET /api/staff/{id}`

**Example:** `GET /api/staff/1`

**Response:**
```json
{
  "id": 1,
  "firstName": "John",
  "lastName": "Doe",
  "fullName": "John Doe",
  "email": "john.doe@example.com",
  "phone": "+1234567890",
  "address": "123 Main St",
  "dateOfBirth": "1990-01-15",
  "gender": "Male",
  "city": "New York",
  "state": "NY",
  "country": "USA",
  "dailyCode": "1234",
  "codeGeneratedDate": "2026-01-29T10:00:00",
  "createdDate": "2025-01-01T00:00:00",
  "isActive": true,
  "isCodeExpired": false,
  "needsCodeRegeneration": false,
  "codeAge": "2 hour(s) ago"
}
```

### 5. Get Staff by Email
Get specific staff member by email address.

**Endpoint:** `GET /api/staff/email/{email}`

**Example:** `GET /api/staff/email/john.doe@example.com`

**Response:** Same as Get Staff by ID

### 6. Create Staff
Create a new staff member.

**Endpoint:** `POST /api/staff`

**Request Body:**
```json
{
  "firstName": "Jane",
  "lastName": "Smith",
  "email": "jane.smith@example.com",
  "phone": "+1234567891",
  "address": "456 Oak Ave",
  "dateOfBirth": "1992-05-20",
  "gender": "Female",
  "city": "Los Angeles",
  "state": "CA",
  "country": "USA",
  "isActive": true
}
```

**Response:**
```json
{
  "success": true,
  "message": "Staff member created successfully",
  "staff": {
    "id": 2,
    "firstName": "Jane",
    "lastName": "Smith",
    "fullName": "Jane Smith",
    "email": "jane.smith@example.com",
    "phone": "+1234567891",
    "address": "456 Oak Ave",
    "dateOfBirth": "1992-05-20",
    "gender": "Female",
    "city": "Los Angeles",
    "state": "CA",
    "country": "USA",
    "dailyCode": "5678",
    "codeGeneratedDate": "2026-01-29T12:00:00",
    "createdDate": "2026-01-29T12:00:00",
    "isActive": true,
    "isCodeExpired": false,
    "needsCodeRegeneration": false,
    "codeAge": "Just now"
  }
}
```

**Notes:**
- Daily code is automatically generated (4-digit random number)
- Email uniqueness is validated
- All fields except firstName and lastName are optional

### 7. Update Staff
Update existing staff member.

**Endpoint:** `PUT /api/staff/{id}`

**Example:** `PUT /api/staff/1`

**Request Body:**
```json
{
  "firstName": "John",
  "lastName": "Doe",
  "email": "john.doe.updated@example.com",
  "phone": "+1234567890",
  "address": "789 New St",
  "dateOfBirth": "1990-01-15",
  "gender": "Male",
  "city": "New York",
  "state": "NY",
  "country": "USA",
  "isActive": true
}
```

**Response:**
```json
{
  "success": true,
  "message": "Staff member updated successfully",
  "staff": { /* Updated staff object */ }
}
```

**Notes:**
- Daily code is NOT changed during update
- Email uniqueness is validated (excluding current staff member)
- Use regenerate code endpoint to update daily code

### 8. Delete Staff
Soft delete staff member (sets IsActive to false).

**Endpoint:** `DELETE /api/staff/{id}`

**Example:** `DELETE /api/staff/1`

**Response:**
```json
{
  "success": true,
  "message": "Staff member deleted successfully"
}
```

**Notes:**
- This is a soft delete - staff record remains in database
- Staff member can be reactivated by updating IsActive to true

### 9. Validate Staff Code
Validate staff daily code for login/authentication.

**Endpoint:** `POST /api/staff/validate-code`

**Request Body:**
```json
{
  "dailyCode": "1234"
}
```

**Success Response (200 OK):**
```json
{
  "success": true,
  "message": "Login successful",
  "staff": {
    "id": 1,
    "firstName": "John",
    "lastName": "Doe",
    "fullName": "John Doe",
    "email": "john.doe@example.com",
    "phone": "+1234567890",
    "dailyCode": "1234",
    "codeGeneratedDate": "2026-01-29T10:00:00",
    "createdDate": "2025-01-01T00:00:00",
    "isActive": true,
    "isCodeExpired": false
  }
}
```

**Failure Response (401 Unauthorized):**
```json
{
  "success": false,
  "message": "Invalid daily code",
  "staff": null
}
```

**Expired Code Response (401 Unauthorized):**
```json
{
  "success": false,
  "message": "Daily code has expired. Please request a new code.",
  "staff": null
}
```

**Notes:**
- Code must be exactly 4 digits
- Code must belong to an active staff member
- Code expires after 24 hours
- Returns 401 Unauthorized for invalid or expired codes

### 10. Regenerate Staff Code
Regenerate daily code for specific staff member.

**Endpoint:** `POST /api/staff/{id}/regenerate-code`

**Example:** `POST /api/staff/1/regenerate-code`

**Response:**
```json
{
  "success": true,
  "message": "Daily code regenerated successfully",
  "newCode": "9876",
  "codeGeneratedDate": "2026-01-29T14:00:00"
}
```

**Notes:**
- Generates new 4-digit random code
- Updates CodeGeneratedDate to current time
- Only works for active staff members

### 11. Regenerate All Codes
Regenerate daily codes for all active staff members.

**Endpoint:** `POST /api/staff/regenerate-all-codes`

**Response:**
```json
{
  "success": true,
  "message": "All staff codes regenerated successfully"
}
```

**Notes:**
- Generates new codes for all active staff
- Uses transaction to ensure all-or-nothing operation
- Useful for daily code rotation

### 12. Get Staff Statistics
Get comprehensive staff statistics.

**Endpoint:** `GET /api/staff/statistics`

**Response:**
```json
{
  "totalStaff": 10,
  "activeStaff": 8,
  "inactiveStaff": 2,
  "staffWithExpiredCodes": 3,
  "staffNeedingCodeRegeneration": 5,
  "oldestCodeDate": "2026-01-20T10:00:00",
  "newestCodeDate": "2026-01-29T14:00:00"
}
```

**Notes:**
- Provides overview of staff status
- Helps identify staff needing code regeneration
- Useful for admin dashboards

### 13. Check Email in Use
Check if email address is already in use.

**Endpoint:** `GET /api/staff/check-email/{email}`

**Query Parameters:**
- `excludeStaffId` (integer, optional): Exclude specific staff ID from check

**Examples:**
```http
GET /api/staff/check-email/john.doe@example.com
GET /api/staff/check-email/john.doe@example.com?excludeStaffId=1
```

**Response:**
```json
{
  "email": "john.doe@example.com",
  "inUse": true
}
```

**Notes:**
- Case-insensitive email comparison
- Use excludeStaffId when updating existing staff
- Helps prevent duplicate email addresses

## Daily Code System

### Code Generation
- **Format:** 4-digit random number (1000-9999)
- **Generation:** Automatic on staff creation
- **Regeneration:** Manual via API endpoints

### Code Expiration
- **Expiration Time:** 24 hours from generation
- **Check:** `isCodeExpired` property in staff DTO
- **Validation:** Expired codes are rejected during login

### Code Regeneration Flow
1. **Individual:** Use `POST /api/staff/{id}/regenerate-code`
2. **Bulk:** Use `POST /api/staff/regenerate-all-codes`
3. **Automatic:** Can be scheduled via cron job or task scheduler

## Authentication Flow

### Staff Login Process
```
1. Staff enters 4-digit code
   ↓
2. POST /api/staff/validate-code
   ↓
3. API validates:
   - Code exists
   - Staff is active
   - Code not expired
   ↓
4. Return staff details or error
```

### Example Implementation
```javascript
// Mobile app login
async function staffLogin(code) {
  const response = await fetch('http://api-url/api/staff/validate-code', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ dailyCode: code })
  });
  
  const result = await response.json();
  
  if (result.success) {
    // Store staff details
    localStorage.setItem('staff', JSON.stringify(result.staff));
    // Navigate to dashboard
  } else {
    // Show error message
    alert(result.message);
  }
}
```

## Validation Rules

### Required Fields
- `firstName` (max 100 characters)
- `lastName` (max 100 characters)

### Optional Fields
- `email` (valid email format, max 200 characters, unique)
- `phone` (valid phone format, max 20 characters)
- `address` (max 500 characters)
- `dateOfBirth` (valid date)
- `gender` (max 20 characters)
- `city` (max 100 characters)
- `state` (max 100 characters)
- `country` (max 100 characters)

### Daily Code
- Must be exactly 4 digits
- Automatically generated
- Cannot be manually set

## Error Responses

### 400 Bad Request
```json
{
  "error": "Email address is already in use"
}
```

### 401 Unauthorized
```json
{
  "success": false,
  "message": "Invalid daily code",
  "staff": null
}
```

### 404 Not Found
```json
{
  "error": "Staff member not found"
}
```

### 500 Internal Server Error
```json
{
  "error": "Internal server error",
  "details": "Error message details"
}
```

## Usage Examples

### Create Staff Member
```bash
curl -X POST http://localhost:5001/api/staff \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "Alice",
    "lastName": "Johnson",
    "email": "alice@example.com",
    "phone": "+1234567892",
    "isActive": true
  }'
```

### Validate Staff Code
```bash
curl -X POST http://localhost:5001/api/staff/validate-code \
  -H "Content-Type: application/json" \
  -d '{"dailyCode": "1234"}'
```

### Regenerate Code
```bash
curl -X POST http://localhost:5001/api/staff/1/regenerate-code
```

### Get Active Staff
```bash
curl http://localhost:5001/api/staff/active
```

### Search Staff
```bash
curl "http://localhost:5001/api/staff/filter?searchTerm=john&isActive=true"
```

## Integration with Other Modules

### With Orders
```javascript
// Create order with staff ID
const order = {
  staffId: staff.id,
  customerId: "CUST001",
  items: [...]
};
```

### With Tables
```javascript
// Assign table to staff
const tableSelection = {
  tableId: "table-guid",
  staffId: staff.id
};
```

## Best Practices

1. **Code Rotation:** Regenerate codes daily for security
2. **Email Validation:** Always check email uniqueness before creation/update
3. **Soft Delete:** Use soft delete to maintain historical data
4. **Code Expiration:** Enforce 24-hour expiration for security
5. **Search Optimization:** Use filtered endpoints for better performance
6. **Error Handling:** Always check response status and handle errors gracefully

## Security Considerations

1. **Code Complexity:** 4-digit codes provide 10,000 combinations
2. **Expiration:** 24-hour expiration limits exposure window
3. **Active Status:** Only active staff can authenticate
4. **Rate Limiting:** Consider implementing rate limiting on validation endpoint
5. **HTTPS:** Always use HTTPS in production
6. **Audit Logging:** Log all authentication attempts

## Performance Tips

1. **Caching:** Cache active staff list for frequently accessed data
2. **Indexing:** Ensure database indexes on Email and DailyCode columns
3. **Batch Operations:** Use regenerate-all-codes for bulk updates
4. **Filtering:** Use filtered endpoints instead of client-side filtering
5. **Pagination:** Consider adding pagination for large staff lists

## Testing Checklist

- [ ] Create staff with all fields
- [ ] Create staff with minimal fields
- [ ] Update staff information
- [ ] Delete staff (soft delete)
- [ ] Validate correct code
- [ ] Validate incorrect code
- [ ] Validate expired code
- [ ] Regenerate individual code
- [ ] Regenerate all codes
- [ ] Check email uniqueness
- [ ] Search staff by name
- [ ] Filter by active status
- [ ] Get staff statistics
- [ ] Handle duplicate email on create
- [ ] Handle duplicate email on update
- [ ] Handle non-existent staff ID

## Swagger UI

Access the interactive API documentation at:
```
http://localhost:5001/swagger
```

All endpoints can be tested directly from the Swagger UI interface.
