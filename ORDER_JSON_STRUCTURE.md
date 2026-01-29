# Order JSON Structure - Complete Documentation

## Overview
This document explains the complete JSON structure for orders (WaitingTransactions) stored in the database, matching the WPF app's CartItem model exactly.

## Complete Cart Item JSON Structure

```json
[
  {
    "ProductId": "RCP-20260124212137-7bc58606",
    "Name": "Jellof Rice",
    "Quantity": 1,
    "Price": 5000.0,
    "Discount": 0.0,
    "TaxId": "",
    "Taxable": true,
    "StaffId": null,
    "IsCompleted": false,
    "CreatedDate": "2026-01-28T13:53:43.3646685+01:00",
    "AppliedModifiers": [
      {
        "ModifierId": "e08e578e-7244-429e-8641-5219fd8a1794",
        "ModifierName": "Extra Chicken",
        "PriceChange": 3000.0,
        "RecipeId": null
      }
    ],
    "ModifierSelectionId": "af599ab9-4598-4a7f-a776-b49831b78bc8",
    "UnitType": "Base",
    "UnitsPerPackage": 1.0,
    "IsBundle": false,
    "IsRecipe": false,
    "SubTotal": 8000.0,
    "Total": 8000.0,
    "TaxAmount": 0.0
  }
]
```

## Field Descriptions

### Core Product Fields
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `ProductId` | string | ✅ Yes | Unique product identifier (e.g., "RCP-xxx" for recipes, "PRD-xxx" for products) |
| `Name` | string | ✅ Yes | Display name of the product |
| `Quantity` | int | ✅ Yes | Number of units ordered (must be >= 1) |
| `Price` | decimal | ✅ Yes | Base price per unit (before modifiers) |
| `Discount` | decimal | ❌ No | Discount amount applied to this item (default: 0) |

### Tax Fields
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `TaxId` | string | ❌ No | Tax rate identifier (e.g., "TAX001") |
| `Taxable` | bool | ❌ No | Whether this item is taxable (default: true) |
| `TaxAmount` | decimal | ❌ No | Calculated tax amount for this item (default: 0) |

### Staff & Status Fields
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `StaffId` | int? | ❌ No | ID of staff member who added this item |
| `IsCompleted` | bool | ❌ No | Whether this item has been prepared/completed (default: false) |
| `CreatedDate` | DateTime | ✅ Yes | When this cart item was created (for ordering) |

### Modifier Fields
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `AppliedModifiers` | array | ❌ No | List of modifiers applied to this item (see structure below) |
| `ModifierSelectionId` | string | ❌ No | Unique ID for this specific modifier combination (GUID) |

### Multi-Unit Pricing Fields
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `UnitType` | string | ❌ No | "Base" or "Package" (default: "Base") |
| `UnitsPerPackage` | decimal | ❌ No | Number of base units in a package (default: 1.0) |

### Bundle/Recipe Fields
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `IsBundle` | bool | ❌ No | Whether this is a bundle item (default: false) |
| `IsRecipe` | bool | ❌ No | Whether this is a recipe item (default: false) |

### Calculated Fields
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `SubTotal` | decimal | ✅ Yes | (Price × Quantity) - Discount |
| `Total` | decimal | ✅ Yes | SubTotal + Modifier costs (before tax) |

## Applied Modifier Structure

Each modifier in the `AppliedModifiers` array has this structure:

```json
{
  "ModifierId": "e08e578e-7244-429e-8641-5219fd8a1794",
  "ModifierName": "Extra Chicken",
  "PriceChange": 3000.0,
  "RecipeId": null
}
```

### Modifier Fields
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `ModifierId` | string | ✅ Yes | Unique identifier for the modifier (GUID) |
| `ModifierName` | string | ✅ Yes | Display name of the modifier |
| `PriceChange` | decimal | ✅ Yes | Price adjustment (positive = add cost, negative = discount) |
| `RecipeId` | string? | ❌ No | Recipe ID if this modifier affects recipe components |

## Complete Order (WaitingTransaction) Structure

The complete order stored in the database:

```json
{
  "TransactionId": "M20260129123456",
  "StaffId": 1,
  "CustomerId": "CUST001",
  "TableId": "TBL001",
  "TableNumber": 5,
  "TableName": "Table 5",
  "Status": "Pending",
  "CreatedDate": "2026-01-29T12:34:56",
  "ModifiedDate": "2026-01-29T12:34:56",
  "CartItems": "[{...}]"  // JSON string of cart items array
}
```

## Examples

### Example 1: Simple Item (No Modifiers)
```json
{
  "ProductId": "PRD-001",
  "Name": "Coca Cola",
  "Quantity": 2,
  "Price": 500.0,
  "Discount": 0.0,
  "TaxId": "TAX001",
  "Taxable": true,
  "StaffId": 1,
  "IsCompleted": false,
  "CreatedDate": "2026-01-29T12:00:00",
  "AppliedModifiers": [],
  "ModifierSelectionId": null,
  "UnitType": "Base",
  "UnitsPerPackage": 1.0,
  "IsBundle": false,
  "IsRecipe": false,
  "SubTotal": 1000.0,
  "Total": 1000.0,
  "TaxAmount": 192.5
}
```

### Example 2: Item with Modifiers
```json
{
  "ProductId": "RCP-001",
  "Name": "Burger",
  "Quantity": 1,
  "Price": 3000.0,
  "Discount": 0.0,
  "TaxId": "TAX001",
  "Taxable": true,
  "StaffId": 1,
  "IsCompleted": false,
  "CreatedDate": "2026-01-29T12:05:00",
  "AppliedModifiers": [
    {
      "ModifierId": "MOD-001",
      "ModifierName": "Extra Cheese",
      "PriceChange": 500.0,
      "RecipeId": null
    },
    {
      "ModifierId": "MOD-002",
      "ModifierName": "No Onions",
      "PriceChange": 0.0,
      "RecipeId": null
    }
  ],
  "ModifierSelectionId": "550e8400-e29b-41d4-a716-446655440000",
  "UnitType": "Base",
  "UnitsPerPackage": 1.0,
  "IsBundle": false,
  "IsRecipe": true,
  "SubTotal": 3000.0,
  "Total": 3500.0,
  "TaxAmount": 673.08
}
```

### Example 3: Package Unit Item
```json
{
  "ProductId": "PRD-002",
  "Name": "Water Bottles",
  "Quantity": 2,
  "Price": 5000.0,
  "Discount": 0.0,
  "TaxId": "TAX001",
  "Taxable": true,
  "StaffId": 1,
  "IsCompleted": false,
  "CreatedDate": "2026-01-29T12:10:00",
  "AppliedModifiers": [],
  "ModifierSelectionId": null,
  "UnitType": "Package",
  "UnitsPerPackage": 12.0,
  "IsBundle": false,
  "IsRecipe": false,
  "SubTotal": 10000.0,
  "Total": 10000.0,
  "TaxAmount": 1923.08
}
```

### Example 4: Bundle Item
```json
{
  "ProductId": "BND-001",
  "Name": "📦 Family Meal (Bundle)",
  "Quantity": 1,
  "Price": 15000.0,
  "Discount": 0.0,
  "TaxId": "TAX001",
  "Taxable": true,
  "StaffId": 1,
  "IsCompleted": false,
  "CreatedDate": "2026-01-29T12:15:00",
  "AppliedModifiers": [],
  "ModifierSelectionId": null,
  "UnitType": "Base",
  "UnitsPerPackage": 1.0,
  "IsBundle": true,
  "IsRecipe": false,
  "SubTotal": 15000.0,
  "Total": 15000.0,
  "TaxAmount": 2884.62
}
```

## Calculation Logic

### SubTotal Calculation
```
SubTotal = (Price × Quantity) - Discount
```

### Total Calculation (with Modifiers)
```
ModifierTotal = Sum of all AppliedModifiers[].PriceChange
Total = SubTotal + ModifierTotal
```

### Tax Calculation
```
TaxAmount = Total × (TaxRate / (100 + TaxRate))
```
Example: For 19.25% tax rate:
```
TaxAmount = Total × (19.25 / 119.25)
```

### Base Unit Quantity (for Inventory)
```
BaseUnitQuantity = Quantity × UnitsPerPackage
```

## Important Notes

1. **Modifier Matching**: Items with different modifiers are treated as separate cart lines, even if they have the same ProductId
2. **ModifierSelectionId**: Used to uniquely identify items with the same product but different modifier combinations
3. **CreatedDate**: Used to maintain cart item order (items are displayed in the order they were added)
4. **IsCompleted**: Used in kitchen display systems to track which items have been prepared
5. **UnitType & UnitsPerPackage**: Support multi-unit pricing (e.g., selling by bottle or by case)
6. **IsBundle vs IsRecipe**: Bundles show "📦" prefix, recipes display like normal products

## API Endpoints

### Create Order
```http
POST /api/orders
Content-Type: application/json

{
  "staffId": 1,
  "customerId": "CUST001",
  "tableId": "TBL001",
  "tableNumber": 5,
  "tableName": "Table 5",
  "items": [
    {
      "productId": "RCP-001",
      "name": "Burger",
      "quantity": 1,
      "price": 3000.0,
      "discount": 0.0,
      "taxable": true,
      "taxId": "TAX001",
      "appliedModifiers": [
        {
          "modifierId": "MOD-001",
          "modifierName": "Extra Cheese",
          "priceChange": 500.0
        }
      ],
      "unitType": "Base",
      "unitsPerPackage": 1.0,
      "isBundle": false,
      "isRecipe": true
    }
  ]
}
```

### Response
```json
{
  "success": true,
  "message": "Order created successfully with ID: M20260129123456",
  "data": "M20260129123456",
  "timestamp": "2026-01-29T12:34:56"
}
```

## Database Storage

The cart items are stored as a JSON string in the `CartItems` column of the `WaitingTransactions` table:

```sql
CREATE TABLE WaitingTransactions (
    TransactionId TEXT PRIMARY KEY,
    CartItems TEXT NOT NULL,           -- JSON array of cart items
    CustomerId TEXT,
    TableId TEXT,
    TableNumber INTEGER,
    TableName TEXT,
    StaffId INTEGER,
    Status TEXT DEFAULT 'Pending',
    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
    ModifiedDate DATETIME DEFAULT CURRENT_TIMESTAMP
)
```

The `CartItems` column contains the complete JSON array with all fields preserved exactly as shown in the examples above.
