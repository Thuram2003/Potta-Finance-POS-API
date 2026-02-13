# PottaAPI - Complete API Documentation

**Version:** 1.0  
**Last Updated:** February 9, 2026  
**Base URL:** `http://localhost:5000/api` (Development)

---

## 📋 Table of Contents

1. [Overview](#overview)
2. [Understanding the Desktop Application](#understanding-the-desktop-application)
3. [Items Endpoints (Detailed)](#items-endpoints-detailed)
4. [Other Endpoints](#other-endpoints)
5. [Response Format](#response-format)
6. [Error Handling](#error-handling)

---

## Overview

PottaAPI is a RESTful API designed specifically for **waiter/POS operations** in the Potta Finance restaurant management system. The desktop application (Potta Finance.exe) serves as the **admin tool** for managing all data, while this API provides read-only access for waiters to:

- View available items (products, bundles, recipes)
- Take orders
- Manage tables and floor plans
- Handle customer information
- Process transactions

**Key Principle:** This API is NOT for admin operations. All data management happens in the desktop application.

---

## Understanding the Desktop Application

Before diving into the API, it's crucial to understand how items work in the desktop application, as the API mirrors this functionality.

### Desktop Application Structure

The Potta Finance desktop application is a comprehensive restaurant management system with these main modules:

1. **Dashboard** - Overview of sales, inventory, and operations
2. **Inventory** - Manage ALL items (products, bundles, assemblies, recipes, ingredients, categories, variations, modifiers)
3. **Purchase** - Purchase orders and procurement
4. **Transactions** - Sales transactions and receipts
5. **Customers** - Customer management
6. **Vendors** - Vendor management
7. **Expenses** - Expense tracking
8. **Reports** - Sales, revenue, and analytics

**IMPORTANT:** There is NO separate "Products", "Bundles", or "Recipes" module. Everything related to items is managed in the **Inventory** module through different tabs.

### Item Types in Desktop Application

The desktop application manages **8 types of items**:

#### 1. **Inventory Items** (Regular Products)
- Individual items that can be sold directly with stock tracking
- Examples: Coca-Cola, Hamburger, French Fries
- Can have variations (e.g., Small/Medium/Large)
- Can have multi-unit pricing (e.g., sell by bottle or crate)
- Can be marked as "ingredients" (used in recipes, not sold directly in POS)
- **Database Table:** `Products` with `Type = 'Inventory'`

**Key Fields:**
- `productId` - Unique identifier
- `name` - Product name
- `sku` - Stock Keeping Unit
- `type` - Always "Inventory" for these items
- `salesPrice` - Selling price
- `cost` - Cost price
- `inventoryOnHand` - Current stock
- `hasVariations` - Whether product has variations
- `isIngredient` - If true, used in recipes only (NOT sold directly in POS)
- `status` - Active (1) or Inactive (0)

**Important:** `isIngredient` is a **flag**, not a separate item type. Ingredients are just regular inventory items with this flag set to true.

#### 2. **Service Items** (Non-Inventory Products)
- Services or non-physical items that don't require stock tracking
- Examples: Consultation, Delivery Fee, Installation Service
- Can have variations (e.g., Basic/Premium service levels)
- Cannot be marked as ingredients
- No inventory tracking
- **Database Table:** `Products` with `Type = 'Service'`

**Key Differences from Inventory Items:**
- `inventoryOnHand` - Always 0 (not tracked)
- `reorderPoint` - Not applicable
- `isIngredient` - Always false (services can't be ingredients)

**Use Case:** Restaurant offers delivery service, consultation, or other non-physical items that need to be invoiced but don't have stock.


#### 3. **Product Variations**
- Different versions of the same product
- Examples: Coca-Cola (Regular), Coca-Cola (Diet), Coca-Cola (Zero)
- Examples: T-Shirt (Red-Small, Red-Medium, Blue-Small, Blue-Medium)
- Each variation has its own price, SKU, and inventory
- Defined by attributes (Color, Size, Flavor, etc.) and their values
- **Database Table:** `ProductVariations`

**How Variations Work:**
1. Create a parent product (e.g., "T-Shirt")
2. Define attributes (e.g., "Color", "Size")
3. Define values for each attribute (Color: Red/Blue/Green, Size: S/M/L)
4. Create variations by combining attribute values (Red-S, Red-M, Blue-S, etc.)

**Key Fields:**
- `variationId` - Unique identifier
- `parentProductId` - Links to parent product
- `name` - Variation name
- `sku` - Unique SKU for this variation
- `salesPrice` - Price for this variation
- `inventoryOnHand` - Stock for this variation

**Important:** Only **Inventory items** can have variations. Service items cannot have variations.

#### 4. **Bundles** (Grouped Items)
- Collection of products sold together
- Components CAN be sold individually
- Examples: "Combo Meal" (Burger + Fries + Drink)
- Price can be less than sum of components (discount)
- **Database Table:** `BundleItems` with `structure = 'Bundle'` and `isRecipe = 0`

**Key Characteristics:**
- `structure` = 'Bundle'
- `isRecipe` = 0
- Components are regular products
- Components appear in product listings (can be sold separately)

**Use Case:** Restaurant wants to offer a combo meal at a discounted price, but customers can still order burger, fries, or drink individually.


#### 5. **Assemblies** (Composite Items)
- Collection of products that form a complete unit
- Components CANNOT be sold individually
- Examples: "Laptop Computer" (Screen + Keyboard + Motherboard + Battery)
- Components are hidden from POS
- **Database Table:** `BundleItems` with `structure = 'Assembly'` and `isRecipe = 0`

**Key Characteristics:**
- `structure` = 'Assembly'
- `isRecipe` = 0
- Components are assembly parts
- Components do NOT appear in product listings
- Only the complete assembly can be sold

**Use Case:** Electronics store sells laptops. The screen, keyboard, etc. are tracked as components for inventory purposes, but customers can only buy the complete laptop.

**CRITICAL RULE:** Assembly components must NEVER appear in waiter/POS listings. The API automatically filters them out.

#### 6. **Recipes** (Special Bundles for Food Service)
- Special type of bundle used in food service
- Contains ingredients with quantities
- Has cooking instructions, preparation time, serving size
- Examples: "Margherita Pizza" (Dough + Tomato Sauce + Cheese + Basil)
- Ingredients are typically marked as `isIngredient = true`
- **Database Table:** `BundleItems` with `isRecipe = 1`

**Key Characteristics:**
- `isRecipe` = 1
- `structure` = Can be EITHER 'Assembly' OR 'Bundle'
  - 'Assembly' = Ingredients cannot be sold separately (most common)
  - 'Bundle' = Ingredients can be sold separately (rare)
- Has `servingSize`, `preparationTime`, `cookingInstructions`
- Components are ingredients (products with `isIngredient = true`)
- Used for cost calculation and inventory tracking

**Use Case:** Restaurant needs to track recipe costs, ingredient usage, and prepare dishes. The recipe defines what goes into each dish.

**Important:** The `isRecipe` flag is what makes it a recipe, NOT the structure. Structure determines if components can be sold separately.


#### 7. **Modifiers**
- Customizations that can be applied to products
- Examples: "Extra Cheese", "No Onions", "Large Size", "Add Bacon"
- Can change the price (positive or negative)
- Can be linked to recipes (recipe-based modifiers)
- **Database Table:** `Modifiers`

**Types of Modifiers:**

**A. Price-Only Modifiers:**
- Simple price adjustment
- Example: "Extra Cheese" (+XAF 500)
- `priceChange` = 500
- `recipeId` = NULL

**B. Recipe-Based Modifiers:**
- Links to a recipe for cost calculation
- Example: "Add Bacon" (uses "Bacon Recipe" with cost XAF 800)
- `recipeId` = [recipe ID]
- `useRecipePrice` = true
- `recipeCost` = 800

**Key Fields:**
- `modifierId` - Unique identifier
- `modifierName` - Display name
- `priceChange` - Price adjustment (can be negative)
- `sortOrder` - Display order
- `status` - Active (true) or Inactive (false)
- `recipeId` - Optional link to recipe
- `useRecipePrice` - Use recipe cost instead of priceChange
- `recipeName` - Name of linked recipe (if any)
- `recipeCost` - Cost of linked recipe (if any)

**How Modifiers Work in POS:**
1. Waiter selects a product (e.g., "Hamburger")
2. System shows available modifiers
3. Waiter selects modifiers (e.g., "Extra Cheese", "No Onions")
4. Price is adjusted automatically
5. Order is saved with modifiers

#### 8. **Multi-Unit Pricing** (Package Options)
- Different package sizes/units for the same product or variation
- Examples: Sell Coca-Cola by bottle (330ml) or crate (24 bottles)
- Each package has its own price and SKU
- Can offer bulk discounts
- **Database Table:** `ProductUnitPricing`

**Key Characteristics:**
- Linked to either a product OR a variation (not both)
- `packageName` - Display name (e.g., "Bottle", "Crate", "Case")
- `baseUnit` - Unit of measurement (e.g., "bottle", "can")
- `unitsPerPackage` - How many units in this package
- `packagePrice` - Price for this package
- `isActive` - Whether this option is available

**Example:**
```
Product: Coca-Cola (330ml)
Base Price: XAF 500 per bottle

Package Options:
1. Single Bottle
   - unitsPerPackage: 1
   - packagePrice: XAF 500
   - pricePerUnit: XAF 500

2. 6-Pack
   - unitsPerPackage: 6
   - packagePrice: XAF 2,800
   - pricePerUnit: XAF 467 (6.6% discount)

3. Crate (24 bottles)
   - unitsPerPackage: 24
   - packagePrice: XAF 10,000
   - pricePerUnit: XAF 417 (16.6% discount)
```

**How Multi-Unit Pricing Works in POS:**
1. Waiter selects a product
2. System shows available package options
3. Waiter selects package (e.g., "Crate")
4. Price is calculated based on package
5. Inventory is deducted by units (24 bottles)

---

### Summary of Item Types

| # | Type | Database Table | Key Identifier | Can Be Sold in POS? |
|---|------|----------------|----------------|---------------------|
| 1 | Inventory Items | Products | Type = 'Inventory' | Yes (unless isIngredient = true) |
| 2 | Service Items | Products | Type = 'Service' | Yes |
| 3 | Product Variations | ProductVariations | variationId | Yes |
| 4 | Bundles | BundleItems | structure = 'Bundle', isRecipe = 0 | Yes |
| 5 | Assemblies | BundleItems | structure = 'Assembly', isRecipe = 0 | Yes (but components cannot) |
| 6 | Recipes | BundleItems | isRecipe = 1 | Yes |
| 7 | Modifiers | Modifiers | modifierId | No (applied to products) |
| 8 | Multi-Unit Pricing | ProductUnitPricing | unitPricingId | No (pricing option for products) |

---

### Desktop Application Workflows

**IMPORTANT:** All item management happens in the **Inventory module**. There are NO separate modules for Products, Bundles, or Recipes.

#### Adding an Inventory Item (Desktop)
1. Navigate to **Inventory module**
2. Click "Create New" button
3. Select "**Inventory**" from the dropdown
4. Enter details (name, SKU, price, cost, etc.)
5. Select categories
6. Upload image (optional)
7. Set inventory levels
8. Mark as ingredient if needed (check "Is Ingredient" box)
9. Save

#### Adding a Service Item (Desktop)
1. Navigate to **Inventory module**
2. Click "Create New" button
3. Select "**Service**" from the dropdown
4. Enter details (name, SKU, price, etc.)
5. Select categories
6. Upload image (optional)
7. Save
(Note: No inventory tracking for services)

#### Adding a Bundle (Desktop)
1. Navigate to **Inventory module**
2. Click "Create New" button
3. Select "**Bundle**" from the dropdown
4. Enter bundle details (name, SKU, price, etc.)
5. Add components (search and select products)
6. Set quantities for each component
7. Set bundle price (usually less than sum of components)
8. Save

**Note:** Opens in full-screen mode for better component management.

#### Adding an Assembly (Desktop)
1. Navigate to **Inventory module**
2. Click "Create New" button
3. Select "**Assembly**" from the dropdown
4. Enter assembly details (name, SKU, price, etc.)
5. Add components (search and select products)
6. Set quantities for each component
7. Set assembly price
8. Save

**Note:** Opens in full-screen mode. Components will be automatically hidden from POS listings.

#### Adding a Recipe (Desktop)
1. Navigate to **Inventory module**
2. Click on "**Recipes**" tab
3. Click "Create Recipe" button
4. Enter recipe details (name, SKU, price, etc.)
5. Add ingredients (products marked as ingredients)
6. Set quantities and units for each ingredient
7. Enter cooking instructions
8. Set preparation time and serving size
9. Choose structure (Assembly or Bundle)
10. Save

#### Managing Ingredients (Desktop)
1. Navigate to **Inventory module**
2. Click on "**Ingredients**" tab
3. Click "Create Ingredient" button
4. Enter ingredient details (same as regular product)
5. System automatically sets `isIngredient = true`
6. Save

**Note:** Ingredients are just regular inventory items with the `isIngredient` flag set to true. They appear in a separate tab for convenience.

#### Adding Modifiers (Desktop)
1. Navigate to **Inventory module**
2. Click on "**Modifiers**" tab
3. Click "Add Modifier" button
4. Enter modifier name
5. Set price change OR link to recipe:
   - **Price-Only:** Enter price change amount
   - **Recipe-Based:** Select recipe and check "Use Recipe Price"
6. Set sort order
7. Save

#### Adding Multi-Unit Pricing (Desktop)
1. Navigate to **Inventory module**
2. Select a product from the list
3. Click "Multi-Unit Pricing" tab in product details
4. Click "Add Package Option"
5. For each option:
   - Enter package name (e.g., "6-Pack", "Crate")
   - Set units per package
   - Set package price
   - Set SKU (optional)
6. Save

#### Managing Categories (Desktop)
1. Navigate to **Inventory module**
2. Click on "**Categories**" tab
3. Click "Add Category" button
4. Enter category name and description
5. Save

#### Managing Variations (Desktop)
1. Navigate to **Inventory module**
2. Click on "**Variations**" tab
3. Define attributes (e.g., "Color", "Size")
4. Define values for each attribute (e.g., Red, Blue, Green)
5. Select a product
6. Create variations by combining attribute values
7. Set price, SKU, and inventory for each variation
8. Save


---

## Items Endpoints (Detailed)

All items endpoints are under `/api/items`. These endpoints provide read-only access to items for waiter/POS operations.

### 🔍 General Principles

1. **Waiter-First Design:** Endpoints return only what waiters need to see
2. **Assembly Filtering:** Assembly components are automatically filtered out
3. **Active Items Only:** Only active items (status = 1) are returned
4. **No Ingredients:** Products marked as ingredients are excluded
5. **Simple & Clean:** No complex query parameters needed

---

### 1. Get All Items

**Endpoint:** `GET /api/items`

**Description:** Returns all sellable items (products, bundles, recipes) in one list.

**Use Case:** Display all available items in a single view (not common in POS, but useful for overview).

**What It Returns:**
- All active products (excluding assembly components and ingredients)
- All active bundles (both Bundle and Assembly structures)
- All active recipes

**Response:**
```json
{
  "success": true,
  "message": "Retrieved 45 items",
  "data": [
    {
      "id": "prod-001",
      "name": "Coca-Cola",
      "sku": "COKE-330",
      "type": "Product",
      "description": "330ml Coca-Cola",
      "cost": 300,
      "salesPrice": 500,
      "formattedPrice": "XAF 500",
      "imagePath": "/images/coke.jpg",
      "status": true,
      "taxable": true,
      "taxId": "tax-001",
      "createdDate": "2024-01-15T10:30:00",
      "modifiedDate": "2024-01-15T10:30:00",
      "statusDisplay": "Active",
      "categories": ["Beverages", "Soft Drinks"],
      "categoriesDisplay": "Beverages, Soft Drinks"
    },
    {
      "id": "bundle-001",
      "name": "Combo Meal",
      "sku": "COMBO-001",
      "type": "Bundle",
      "description": "Burger + Fries + Drink",
      "cost": 1500,
      "salesPrice": 2500,
      "formattedPrice": "XAF 2,500",
      "imagePath": "/images/combo.jpg",
      "status": true,
      "taxable": true,
      "taxId": "tax-001",
      "createdDate": "2024-01-20T14:00:00",
      "modifiedDate": "2024-01-20T14:00:00",
      "statusDisplay": "Active",
      "categories": [],
      "categoriesDisplay": "Uncategorized"
    }
  ]
}
```

**Desktop Equivalent:** Inventory module > All Items view (combines products and bundles)


---

### 2. Get All Products

**Endpoint:** `GET /api/items/products`

**Description:** Returns all sellable products (excluding assembly components and ingredients).

**Use Case:** Display products in POS for waiters to select.

**What It Returns:**
- All active products
- Excludes products marked as `isIngredient = 1`
- **CRITICAL:** Automatically excludes assembly components
- Includes products with variations (variations loaded separately)

**Response:**
```json
{
  "success": true,
  "message": "Retrieved 32 products",
  "data": [
    {
      "id": "prod-001",
      "name": "Coca-Cola",
      "sku": "COKE-330",
      "type": "Product",
      "description": "330ml Coca-Cola",
      "cost": 300,
      "salesPrice": 500,
      "formattedPrice": "XAF 500",
      "imagePath": "/images/coke.jpg",
      "status": true,
      "taxable": true,
      "taxId": "tax-001",
      "createdDate": "2024-01-15T10:30:00",
      "modifiedDate": "2024-01-15T10:30:00",
      "statusDisplay": "Active",
      "categories": ["Beverages", "Soft Drinks"],
      "categoriesDisplay": "Beverages, Soft Drinks",
      "inventoryOnHand": 150,
      "reorderPoint": 50,
      "unitOfMeasure": "bottle",
      "hasVariations": false,
      "variationCount": 0,
      "isIngredient": false,
      "costPerUnit": 300,
      "purchaseUnit": "bottle",
      "recipeUnit": "bottle",
      "conversionFactor": 1,
      "purchaseMode": "Standard",
      "isLowStock": false,
      "stockInfo": "150.00 in stock",
      "variationDisplay": "",
      "variations": []
    }
  ]
}
```

**Assembly Component Filtering Example:**

Imagine you have:
- Product: "Laptop Screen" (used in "Laptop Computer" assembly)
- Product: "Coca-Cola" (standalone product)
- Assembly: "Laptop Computer" (contains "Laptop Screen" as component)

**What API Returns:**
- ✅ "Coca-Cola" (standalone product)
- ✅ "Laptop Computer" (complete assembly)
- ❌ "Laptop Screen" (assembly component - FILTERED OUT)

**Desktop Equivalent:** Inventory module > Product list (with isIngredient = 0 filter)


---

### 3. Get Product by ID

**Endpoint:** `GET /api/items/products/{id}`

**Description:** Returns detailed information about a specific product, including variations if any.

**Use Case:** View product details, check stock, see variations.

**Parameters:**
- `id` (path) - Product ID

**Response:**
```json
{
  "success": true,
  "message": "Product retrieved successfully",
  "data": {
    "id": "prod-002",
    "name": "T-Shirt",
    "sku": "TSHIRT-001",
    "type": "Product",
    "description": "Cotton T-Shirt",
    "cost": 2000,
    "salesPrice": 5000,
    "formattedPrice": "XAF 5,000",
    "imagePath": "/images/tshirt.jpg",
    "status": true,
    "taxable": true,
    "taxId": "tax-001",
    "createdDate": "2024-01-15T10:30:00",
    "modifiedDate": "2024-01-15T10:30:00",
    "statusDisplay": "Active",
    "categories": ["Clothing"],
    "categoriesDisplay": "Clothing",
    "inventoryOnHand": 0,
    "reorderPoint": 10,
    "unitOfMeasure": "piece",
    "hasVariations": true,
    "variationCount": 6,
    "isIngredient": false,
    "costPerUnit": 2000,
    "purchaseUnit": "piece",
    "recipeUnit": "piece",
    "conversionFactor": 1,
    "purchaseMode": "Standard",
    "isLowStock": false,
    "stockInfo": "0.00 in stock",
    "variationDisplay": "6 variations",
    "variations": [
      {
        "variationId": "var-001",
        "parentProductId": "prod-002",
        "sku": "TSHIRT-RED-S",
        "name": "Red - Small",
        "cost": 2000,
        "salesPrice": 5000,
        "formattedPrice": "XAF 5,000",
        "inventoryOnHand": 15,
        "reorderPoint": 5,
        "imagePath": "/images/tshirt-red-s.jpg",
        "status": true,
        "attributeValuesDisplay": "Color: Red, Size: Small",
        "fullDisplayName": "T-Shirt - Red - Small",
        "isLowStock": false,
        "stockInfo": "15.00 in stock",
        "attributeValues": {
          "attr-color": "val-red",
          "attr-size": "val-small"
        }
      }
    ]
  }
}
```

**Desktop Equivalent:** Inventory module > Select product > View details


---

### 4. Get Product Variations with Attributes

**Endpoint:** `GET /api/items/products/{productId}/variations`

**Description:** Returns variations for a product with full attribute and value data. This is designed for building ComboBox UI in POS.

**Use Case:** When a waiter selects a product with variations, show ComboBoxes for each attribute (Color, Size, etc.) so they can select the specific variation.

**Parameters:**
- `productId` (path) - Parent product ID

**Response:**
```json
{
  "success": true,
  "message": "Retrieved 6 variations with 2 attributes",
  "data": {
    "productId": "prod-002",
    "productName": "T-Shirt",
    "attributes": [
      {
        "attributeId": "attr-color",
        "attributeName": "Color",
        "values": [
          {
            "valueId": "val-red",
            "valueName": "Red"
          },
          {
            "valueId": "val-blue",
            "valueName": "Blue"
          },
          {
            "valueId": "val-green",
            "valueName": "Green"
          }
        ]
      },
      {
        "attributeId": "attr-size",
        "attributeName": "Size",
        "values": [
          {
            "valueId": "val-small",
            "valueName": "Small"
          },
          {
            "valueId": "val-medium",
            "valueName": "Medium"
          }
        ]
      }
    ],
    "variations": [
      {
        "variationId": "var-001",
        "parentProductId": "prod-002",
        "sku": "TSHIRT-RED-S",
        "name": "Red - Small",
        "cost": 2000,
        "salesPrice": 5000,
        "formattedPrice": "XAF 5,000",
        "inventoryOnHand": 15,
        "reorderPoint": 5,
        "imagePath": "/images/tshirt-red-s.jpg",
        "status": true,
        "attributeValuesDisplay": "Color: Red, Size: Small",
        "fullDisplayName": "T-Shirt - Red - Small",
        "isLowStock": false,
        "stockInfo": "15.00 in stock",
        "attributeValues": {
          "attr-color": "val-red",
          "attr-size": "val-small"
        }
      }
    ]
  }
}
```

**How to Use in POS:**
1. Waiter selects "T-Shirt"
2. System calls this endpoint
3. Build ComboBox for "Color" with values: Red, Blue, Green
4. Build ComboBox for "Size" with values: Small, Medium
5. Waiter selects "Red" and "Small"
6. System finds variation with `attributeValues: {"attr-color": "val-red", "attr-size": "val-small"}`
7. Add that variation to cart

**Desktop Equivalent:** Inventory module > Select product with variations > View variations tab


---

### 5. Get All Bundles

**Endpoint:** `GET /api/items/bundles`

**Description:** Returns all sellable bundles (both Bundle and Assembly structures, excluding recipes).

**Use Case:** Display bundles in POS for waiters to select.

**What It Returns:**
- All active bundles with `structure = 'Bundle'`
- All active assemblies with `structure = 'Assembly'`
- **Excludes** recipes (`isRecipe = 0`)

**Response:**
```json
{
  "success": true,
  "message": "Retrieved 8 bundles",
  "data": [
    {
      "id": "bundle-001",
      "name": "Combo Meal",
      "sku": "COMBO-001",
      "type": "Bundle",
      "description": "Burger + Fries + Drink",
      "cost": 1500,
      "salesPrice": 2500,
      "formattedPrice": "XAF 2,500",
      "imagePath": "/images/combo.jpg",
      "status": true,
      "taxable": true,
      "taxId": "tax-001",
      "createdDate": "2024-01-20T14:00:00",
      "modifiedDate": "2024-01-20T14:00:00",
      "statusDisplay": "Active",
      "categories": [],
      "categoriesDisplay": "Uncategorized",
      "structure": "Bundle",
      "inventoryOnHand": 0,
      "reorderPoint": 0,
      "isRecipe": false,
      "servingSize": 1,
      "preparationTime": 0,
      "cookingInstructions": "",
      "isLowStock": false,
      "bundleInfo": "Bundle (3 items)",
      "recipeTypeDisplay": "Bundle",
      "components": [
        {
          "productId": "prod-003",
          "productName": "Hamburger",
          "quantity": 1,
          "recipeUnit": "piece",
          "productPrice": 1000,
          "totalPrice": 1000,
          "formattedTotalPrice": "XAF 1,000"
        },
        {
          "productId": "prod-004",
          "productName": "French Fries",
          "quantity": 1,
          "recipeUnit": "piece",
          "productPrice": 500,
          "totalPrice": 500,
          "formattedTotalPrice": "XAF 500"
        },
        {
          "productId": "prod-001",
          "productName": "Coca-Cola",
          "quantity": 1,
          "recipeUnit": "bottle",
          "productPrice": 500,
          "totalPrice": 500,
          "formattedTotalPrice": "XAF 500"
        }
      ]
    }
  ]
}
```

**Desktop Equivalent:** Inventory module > Bundle list (with isRecipe = 0 filter)


---

### 6. Get Bundle by ID

**Endpoint:** `GET /api/items/bundles/{id}`

**Description:** Returns detailed information about a specific bundle, including all components.

**Use Case:** View bundle details, see what's included.

**Parameters:**
- `id` (path) - Bundle ID

**Response:** Same structure as bundle in "Get All Bundles" but for a single bundle.

**Desktop Equivalent:** Inventory module > Select bundle > View details

---

### 7. Get All Recipes

**Endpoint:** `GET /api/items/recipes`

**Description:** Returns all recipes (special bundles with cooking instructions).

**Use Case:** Display recipes in POS, view recipe details for kitchen.

**What It Returns:**
- All active bundles with `isRecipe = 1`
- Includes cooking instructions, preparation time, serving size
- Includes ingredient list with quantities

**Response:**
```json
{
  "success": true,
  "message": "Retrieved 12 recipes",
  "data": [
    {
      "id": "recipe-001",
      "name": "Margherita Pizza",
      "sku": "PIZZA-MARG",
      "type": "Recipe",
      "description": "Classic Italian pizza",
      "cost": 1200,
      "salesPrice": 3500,
      "formattedPrice": "XAF 3,500",
      "imagePath": "/images/pizza-margherita.jpg",
      "status": true,
      "taxable": true,
      "taxId": "tax-001",
      "createdDate": "2024-01-25T09:00:00",
      "modifiedDate": "2024-01-25T09:00:00",
      "statusDisplay": "Active",
      "categories": [],
      "categoriesDisplay": "Uncategorized",
      "structure": "Assembly",
      "inventoryOnHand": 0,
      "reorderPoint": 0,
      "isRecipe": true,
      "servingSize": 1,
      "preparationTime": 15,
      "cookingInstructions": "1. Prepare dough\n2. Add tomato sauce\n3. Add mozzarella\n4. Add basil\n5. Bake at 450°F for 12 minutes",
      "isLowStock": false,
      "bundleInfo": "Recipe (1 servings)",
      "recipeTypeDisplay": "Recipe",
      "components": [
        {
          "productId": "ing-001",
          "productName": "Pizza Dough",
          "quantity": 250,
          "recipeUnit": "gram",
          "productPrice": 200,
          "totalPrice": 200,
          "formattedTotalPrice": "XAF 200"
        },
        {
          "productId": "ing-002",
          "productName": "Tomato Sauce",
          "quantity": 100,
          "recipeUnit": "gram",
          "productPrice": 150,
          "totalPrice": 150,
          "formattedTotalPrice": "XAF 150"
        },
        {
          "productId": "ing-003",
          "productName": "Mozzarella Cheese",
          "quantity": 150,
          "recipeUnit": "gram",
          "productPrice": 600,
          "totalPrice": 600,
          "formattedTotalPrice": "XAF 600"
        },
        {
          "productId": "ing-004",
          "productName": "Fresh Basil",
          "quantity": 10,
          "recipeUnit": "gram",
          "productPrice": 50,
          "totalPrice": 50,
          "formattedTotalPrice": "XAF 50"
        }
      ]
    }
  ]
}
```

**Desktop Equivalent:** Inventory module > Recipes tab > Recipe list


---

### 8. Search Items

**Endpoint:** `GET /api/items/search`

**Description:** Search across all items (products, bundles, recipes) by name, SKU, description, or categories.

**Use Case:** Waiter searches for an item in POS.

**Query Parameters:**
- `searchTerm` (optional) - Search text (searches name, SKU, description, categories)
- `page` (optional, default: 1) - Page number
- `pageSize` (optional, default: 50, max: 100) - Items per page

**What It Searches:**
- Product names, SKUs, descriptions, categories
- Bundle names, SKUs, descriptions
- Recipe names, SKUs, descriptions

**What It Excludes:**
- Assembly components (automatically filtered)
- Ingredients (`isIngredient = 1`)
- Inactive items (`status = 0`)

**Example Request:**
```
GET /api/items/search?searchTerm=cola&page=1&pageSize=20
```

**Response:**
```json
{
  "success": true,
  "message": "Found 3 items (showing page 1 of 1)",
  "data": {
    "items": [
      {
        "id": "prod-001",
        "name": "Coca-Cola",
        "sku": "COKE-330",
        "type": "Product",
        "description": "330ml Coca-Cola",
        "cost": 300,
        "salesPrice": 500,
        "formattedPrice": "XAF 500",
        "imagePath": "/images/coke.jpg",
        "status": true,
        "taxable": true,
        "taxId": "tax-001",
        "createdDate": "2024-01-15T10:30:00",
        "modifiedDate": "2024-01-15T10:30:00",
        "statusDisplay": "Active",
        "categories": ["Beverages", "Soft Drinks"],
        "categoriesDisplay": "Beverages, Soft Drinks"
      },
      {
        "id": "prod-005",
        "name": "Pepsi Cola",
        "sku": "PEPSI-330",
        "type": "Product",
        "description": "330ml Pepsi",
        "cost": 300,
        "salesPrice": 500,
        "formattedPrice": "XAF 500",
        "imagePath": "/images/pepsi.jpg",
        "status": true,
        "taxable": true,
        "taxId": "tax-001",
        "createdDate": "2024-01-15T10:35:00",
        "modifiedDate": "2024-01-15T10:35:00",
        "statusDisplay": "Active",
        "categories": ["Beverages", "Soft Drinks"],
        "categoriesDisplay": "Beverages, Soft Drinks"
      }
    ],
    "totalCount": 2,
    "page": 1,
    "pageSize": 20,
    "totalPages": 1,
    "hasNextPage": false,
    "hasPreviousPage": false,
    "itemType": "",
    "category": ""
  }
}
```

**Desktop Equivalent:** Inventory module > Search bar (searches products and bundles)


---

### 9. Get All Modifiers

**Endpoint:** `GET /api/items/modifiers`

**Description:** Returns all active modifiers that can be applied to products.

**Use Case:** Display available modifiers when waiter selects a product in POS.

**What It Returns:**
- All active modifiers (`status = 1`)
- Sorted by `sortOrder` then `modifierName`
- Includes both price-only and recipe-based modifiers

**Response:**
```json
{
  "success": true,
  "message": "Retrieved 8 modifiers",
  "data": [
    {
      "modifierId": "mod-001",
      "modifierName": "Extra Cheese",
      "priceChange": 500,
      "sortOrder": 1,
      "status": true,
      "createdDate": "2024-01-10T08:00:00",
      "modifiedDate": "2024-01-10T08:00:00",
      "recipeId": null,
      "useRecipePrice": false,
      "recipeName": null,
      "recipeCost": 0,
      "hasRecipe": false,
      "modifierTypeDisplay": "Price Only",
      "effectivePrice": 500,
      "priceChangeDisplay": "+XAF 500",
      "statusDisplay": "Active"
    },
    {
      "modifierId": "mod-002",
      "modifierName": "No Onions",
      "priceChange": 0,
      "sortOrder": 2,
      "status": true,
      "createdDate": "2024-01-10T08:05:00",
      "modifiedDate": "2024-01-10T08:05:00",
      "recipeId": null,
      "useRecipePrice": false,
      "recipeName": null,
      "recipeCost": 0,
      "hasRecipe": false,
      "modifierTypeDisplay": "Price Only",
      "effectivePrice": 0,
      "priceChangeDisplay": "+XAF 0",
      "statusDisplay": "Active"
    },
    {
      "modifierId": "mod-003",
      "modifierName": "Add Bacon",
      "priceChange": 0,
      "sortOrder": 3,
      "status": true,
      "createdDate": "2024-01-10T08:10:00",
      "modifiedDate": "2024-01-10T08:10:00",
      "recipeId": "recipe-bacon",
      "useRecipePrice": true,
      "recipeName": "Bacon Recipe",
      "recipeCost": 800,
      "hasRecipe": true,
      "modifierTypeDisplay": "Recipe-based",
      "effectivePrice": 800,
      "priceChangeDisplay": "+XAF 800",
      "statusDisplay": "Active"
    },
    {
      "modifierId": "mod-004",
      "modifierName": "Large Size",
      "priceChange": 300,
      "sortOrder": 4,
      "status": true,
      "createdDate": "2024-01-10T08:15:00",
      "modifiedDate": "2024-01-10T08:15:00",
      "recipeId": null,
      "useRecipePrice": false,
      "recipeName": null,
      "recipeCost": 0,
      "hasRecipe": false,
      "modifierTypeDisplay": "Price Only",
      "effectivePrice": 300,
      "priceChangeDisplay": "+XAF 300",
      "statusDisplay": "Active"
    }
  ]
}
```

**Field Explanations:**

- `modifierId` - Unique identifier
- `modifierName` - Display name (e.g., "Extra Cheese")
- `priceChange` - Price adjustment for price-only modifiers
- `sortOrder` - Display order (lower numbers first)
- `status` - Active (true) or Inactive (false)
- `recipeId` - Links to recipe if recipe-based modifier
- `useRecipePrice` - If true, use recipe cost instead of priceChange
- `recipeName` - Name of linked recipe (if any)
- `recipeCost` - Cost of linked recipe (if any)
- `hasRecipe` - Computed: true if recipeId is not null
- `modifierTypeDisplay` - Computed: "Recipe-based" or "Price Only"
- `effectivePrice` - Computed: recipeCost if recipe-based, otherwise priceChange
- `priceChangeDisplay` - Computed: Formatted price with + or - sign
- `statusDisplay` - Computed: "Active" or "Inactive"

**How to Use in POS:**
1. Waiter selects a product (e.g., "Hamburger" - XAF 1,000)
2. System calls `/api/items/modifiers`
3. Display modifiers as checkboxes or buttons
4. Waiter selects "Extra Cheese" (+XAF 500) and "Add Bacon" (+XAF 800)
5. Calculate new price: 1,000 + 500 + 800 = XAF 2,300
6. Add to cart with modifiers

**Desktop Equivalent:** Inventory module > Modifiers tab > Modifier list


---

### 10. Get Modifier by ID

**Endpoint:** `GET /api/items/modifiers/{id}`

**Description:** Returns detailed information about a specific modifier.

**Use Case:** View modifier details, check if it's recipe-based.

**Parameters:**
- `id` (path) - Modifier ID

**Response:** Same structure as modifier in "Get All Modifiers" but for a single modifier.

**Desktop Equivalent:** Inventory module > Modifiers tab > Select modifier > View details

---

### 11. Get Product Unit Pricing

**Endpoint:** `GET /api/items/products/{productId}/unit-pricing`

**Description:** Returns all package options (multi-unit pricing) for a product.

**Use Case:** Display package size options when waiter selects a product in POS.

**Parameters:**
- `productId` (path) - Product ID

**What It Returns:**
- All active unit pricing options for the product
- Sorted by `unitsPerPackage` (ascending)
- Includes discount calculations

**Response:**
```json
{
  "success": true,
  "message": "Retrieved 3 unit pricing options",
  "data": [
    {
      "unitPricingId": "up-001",
      "productId": "prod-001",
      "variationId": null,
      "packageName": "Single Bottle",
      "baseUnit": "bottle",
      "unitsPerPackage": 1,
      "packagePrice": 500,
      "formattedPackagePrice": "XAF 500",
      "packageImagePath": "",
      "isActive": true,
      "sku": "COKE-330-SINGLE",
      "createdDate": "2024-01-15T10:30:00",
      "modifiedDate": "2024-01-15T10:30:00",
      "pricePerUnitInPackage": 500,
      "formattedPricePerUnit": "XAF 500",
      "baseUnitPrice": 500,
      "discountPercentage": 0,
      "isDiscounted": false,
      "isPremium": false,
      "discountDisplay": "",
      "packageInfo": "1 bottles per Single Bottle"
    },
    {
      "unitPricingId": "up-002",
      "productId": "prod-001",
      "variationId": null,
      "packageName": "6-Pack",
      "baseUnit": "bottle",
      "unitsPerPackage": 6,
      "packagePrice": 2800,
      "formattedPackagePrice": "XAF 2,800",
      "packageImagePath": "",
      "isActive": true,
      "sku": "COKE-330-6PACK",
      "createdDate": "2024-01-15T10:30:00",
      "modifiedDate": "2024-01-15T10:30:00",
      "pricePerUnitInPackage": 467,
      "formattedPricePerUnit": "XAF 467",
      "baseUnitPrice": 500,
      "discountPercentage": 6.6,
      "isDiscounted": true,
      "isPremium": false,
      "discountDisplay": "Save 6.6%",
      "packageInfo": "6 bottles per 6-Pack"
    },
    {
      "unitPricingId": "up-003",
      "productId": "prod-001",
      "variationId": null,
      "packageName": "Crate (24 bottles)",
      "baseUnit": "bottle",
      "unitsPerPackage": 24,
      "packagePrice": 10000,
      "formattedPackagePrice": "XAF 10,000",
      "packageImagePath": "",
      "isActive": true,
      "sku": "COKE-330-CRATE",
      "createdDate": "2024-01-15T10:30:00",
      "modifiedDate": "2024-01-15T10:30:00",
      "pricePerUnitInPackage": 417,
      "formattedPricePerUnit": "XAF 417",
      "baseUnitPrice": 500,
      "discountPercentage": 16.6,
      "isDiscounted": true,
      "isPremium": false,
      "discountDisplay": "Save 16.6%",
      "packageInfo": "24 bottles per Crate (24 bottles)"
    }
  ]
}
```

**Field Explanations:**

- `unitPricingId` - Unique identifier
- `productId` - Links to product
- `variationId` - Links to variation (null for products)
- `packageName` - Display name (e.g., "6-Pack", "Crate")
- `baseUnit` - Unit of measurement (e.g., "bottle", "can")
- `unitsPerPackage` - How many units in this package
- `packagePrice` - Price for this package
- `formattedPackagePrice` - Formatted price string
- `packageImagePath` - Optional image for package
- `isActive` - Whether this option is available
- `sku` - Unique SKU for this package
- `pricePerUnitInPackage` - Computed: packagePrice / unitsPerPackage
- `formattedPricePerUnit` - Computed: Formatted price per unit
- `baseUnitPrice` - Base product price (for comparison)
- `discountPercentage` - Computed: Discount compared to base price
- `isDiscounted` - Computed: true if discountPercentage > 0
- `isPremium` - Computed: true if discountPercentage < 0 (more expensive)
- `discountDisplay` - Computed: "Save X%" or "+X%" or ""
- `packageInfo` - Computed: "X units per Package Name"

**How to Use in POS:**
1. Waiter selects "Coca-Cola"
2. System calls `/api/items/products/prod-001/unit-pricing`
3. Display package options as buttons or dropdown
4. Waiter selects "Crate (24 bottles)" - XAF 10,000
5. Add to cart with package info
6. Inventory is deducted by 24 bottles

**Desktop Equivalent:** Inventory module > Select product > Multi-Unit Pricing tab


---

### 12. Get Variation Unit Pricing

**Endpoint:** `GET /api/items/variations/{variationId}/unit-pricing`

**Description:** Returns all package options (multi-unit pricing) for a product variation.

**Use Case:** Display package size options when waiter selects a specific variation in POS.

**Parameters:**
- `variationId` (path) - Variation ID

**Response:** Same structure as "Get Product Unit Pricing" but for a variation.

**Example:**
If you have "Coca-Cola (Diet)" as a variation, it might have different package options than regular Coca-Cola.

**Desktop Equivalent:** Inventory module > Select product > Variations tab > Select variation > Multi-Unit Pricing

---

### 13. Get Products by Category

**Endpoint:** `GET /api/items/products/category/{categoryId}`

**Description:** Returns all products in a specific category.

**Use Case:** Filter products by category in POS.

**Parameters:**
- `categoryId` (path) - Category ID

**Response:** List of products (same structure as "Get All Products")

**Desktop Equivalent:** Inventory module > Filter by category

---

### 14. Get Low Stock Products

**Endpoint:** `GET /api/items/products/low-stock`

**Description:** Returns products with inventory below reorder point.

**Use Case:** Alert waiters about low stock items, prevent selling out-of-stock items.

**What It Returns:**
- Products where `inventoryOnHand < reorderPoint`
- Only products with `reorderPoint > 0`
- Sorted by stock level (lowest first)

**Response:** List of products (same structure as "Get All Products")

**Desktop Equivalent:** Inventory > Low Stock Report

---

### 15. Get All Categories

**Endpoint:** `GET /api/items/categories`

**Description:** Returns all active categories with item counts.

**Use Case:** Display category filters in POS, organize products.

**Response:**
```json
{
  "success": true,
  "message": "Retrieved 5 categories",
  "data": [
    {
      "categoryId": "cat-001",
      "name": "Beverages",
      "description": "All drinks",
      "itemCount": 15,
      "isActive": true,
      "createdDate": "2024-01-01T00:00:00"
    },
    {
      "categoryId": "cat-002",
      "name": "Food",
      "description": "All food items",
      "itemCount": 28,
      "isActive": true,
      "createdDate": "2024-01-01T00:00:00"
    }
  ]
}
```

**Desktop Equivalent:** Inventory module > Categories tab

---

### 16. Get Item Statistics

**Endpoint:** `GET /api/items/statistics`

**Description:** Returns statistics about items (counts, inventory value, etc.).

**Use Case:** Dashboard overview, analytics.

**Response:**
```json
{
  "success": true,
  "message": "Item statistics retrieved successfully",
  "data": {
    "totalItems": 45,
    "activeItems": 42,
    "inactiveItems": 3,
    "totalProducts": 32,
    "totalBundles": 8,
    "totalRecipes": 12,
    "productsWithVariations": 5,
    "totalVariations": 23,
    "lowStockItems": 7,
    "ingredientsCount": 18,
    "totalInventoryValue": 2500000,
    "formattedInventoryValue": "XAF 2,500,000",
    "lastItemCreated": "2024-02-08T15:30:00",
    "mostPopularCategory": "Beverages"
  }
}
```

**Desktop Equivalent:** Dashboard > Statistics panel


---

## Other Endpoints

### 🏢 Floor Plans & Tables

Floor plans represent the physical layout of the restaurant with tables and seats.

#### Get All Floor Plans
**Endpoint:** `GET /api/floorplans`

**Description:** Returns all active floor plans.

**Response:**
```json
{
  "success": true,
  "message": "Retrieved 2 floor plans",
  "data": [
    {
      "floorPlanId": "fp-001",
      "floorName": "Main Dining",
      "floorNumber": 1,
      "isActive": true,
      "tableCount": 12,
      "totalSeats": 48,
      "availableSeats": 32,
      "occupiedSeats": 16,
      "createdDate": "2024-01-01T00:00:00"
    }
  ]
}
```

#### Get Floor Plan by ID
**Endpoint:** `GET /api/floorplans/{id}`

**Description:** Returns detailed floor plan with all tables and seats.

#### Create Floor Plan
**Endpoint:** `POST /api/floorplans`

**Description:** Creates a new floor plan with tables and seats.

---

### 🪑 Tables

#### Get All Tables
**Endpoint:** `GET /api/tables`

**Description:** Returns all tables across all floor plans.

#### Get Table by ID
**Endpoint:** `GET /api/tables/{id}`

**Description:** Returns table details with seats.

#### Update Table Status
**Endpoint:** `PUT /api/tables/{id}/status`

**Description:** Updates table status (Available, Occupied, Reserved, Not Available).

#### Update Seat Status
**Endpoint:** `PUT /api/tables/{tableId}/seats/{seatId}/status`

**Description:** Updates individual seat status.

---

### 👥 Customers

#### Get All Customers
**Endpoint:** `GET /api/customers`

**Description:** Returns all customers with pagination.

**Query Parameters:**
- `page` (optional, default: 1)
- `pageSize` (optional, default: 50)

#### Search Customers
**Endpoint:** `GET /api/customers/search`

**Description:** Search customers by name, email, or phone.

**Query Parameters:**
- `searchTerm` (required)
- `page` (optional, default: 1)
- `pageSize` (optional, default: 50)

#### Get Customer by ID
**Endpoint:** `GET /api/customers/{id}`

**Description:** Returns customer details.

---

### 👨‍💼 Staff

#### Staff Login
**Endpoint:** `POST /api/staff/login`

**Description:** Authenticates staff member with daily code.

**Request Body:**
```json
{
  "dailyCode": "1234"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Login successful",
  "data": {
    "id": 1,
    "firstName": "John",
    "lastName": "Doe",
    "email": "john@example.com",
    "phone": "+237123456789",
    "isActive": true,
    "fullName": "John Doe",
    "initials": "JD"
  }
}
```

#### Get All Staff
**Endpoint:** `GET /api/staff`

**Description:** Returns all active staff members.

#### Get Staff by ID
**Endpoint:** `GET /api/staff/{id}`

**Description:** Returns staff member details.


---

### 🛒 Orders

#### Create Waiting Transaction
**Endpoint:** `POST /api/orders/waiting-transactions`

**Description:** Creates a new waiting transaction (order in progress).

**Request Body:**
```json
{
  "cartItems": [
    {
      "productId": "prod-001",
      "productName": "Coca-Cola",
      "quantity": 2,
      "price": 500,
      "discount": 0,
      "taxable": true,
      "taxId": "tax-001",
      "unitType": "Base",
      "unitsPerPackage": 1,
      "isBundle": false,
      "isRecipe": false,
      "modifiers": [
        {
          "modifierId": "mod-001",
          "modifierName": "Extra Ice",
          "priceChange": 0
        }
      ]
    }
  ],
  "customerId": "cust-001",
  "tableId": "table-001",
  "tableNumber": 5,
  "tableName": "Table 5",
  "staffId": 1
}
```

**Response:**
```json
{
  "success": true,
  "message": "Waiting transaction created successfully",
  "data": {
    "transactionId": "wt-001",
    "status": "Pending"
  }
}
```

#### Get All Waiting Transactions
**Endpoint:** `GET /api/orders/waiting-transactions`

**Description:** Returns all pending waiting transactions.

#### Get Waiting Transaction by ID
**Endpoint:** `GET /api/orders/waiting-transactions/{id}`

**Description:** Returns specific waiting transaction details.

#### Update Waiting Transaction
**Endpoint:** `PUT /api/orders/waiting-transactions/{id}`

**Description:** Updates a waiting transaction (add/remove items, change status).

#### Delete Waiting Transaction
**Endpoint:** `DELETE /api/orders/waiting-transactions/{id}`

**Description:** Deletes a waiting transaction.

---

### 🌐 Network

#### Get QR Code
**Endpoint:** `GET /api/network/qr-code`

**Description:** Returns QR code for mobile app connection.

**Response:**
```json
{
  "success": true,
  "message": "QR code generated successfully",
  "data": {
    "qrCodeData": "potta://connect?ip=192.168.1.100&port=5000",
    "ipAddress": "192.168.1.100",
    "port": 5000
  }
}
```

---

### 🔄 Sync

#### Get Database Path
**Endpoint:** `GET /api/sync/database-path`

**Description:** Returns the path to the SQLite database file.

**Response:**
```json
{
  "success": true,
  "message": "Database path retrieved successfully",
  "data": {
    "databasePath": "C:\\ProgramData\\PottaFinance\\pottafinance.db"
  }
}
```

---

## Response Format

All API responses follow a consistent structure:

### Success Response

```json
{
  "success": true,
  "message": "Operation completed successfully",
  "data": {
    // Response data here
  }
}
```

**Fields:**
- `success` (boolean) - Always `true` for successful responses
- `message` (string) - Human-readable success message
- `data` (object/array) - The actual response data

### Error Response

```json
{
  "success": false,
  "message": "Error description",
  "errors": [
    {
      "field": "productId",
      "message": "Product not found"
    }
  ]
}
```

**Fields:**
- `success` (boolean) - Always `false` for error responses
- `message` (string) - Human-readable error message
- `errors` (array, optional) - Detailed validation errors

### Pagination Response

For endpoints that support pagination (search, lists):

```json
{
  "success": true,
  "message": "Retrieved 50 items (showing page 1 of 3)",
  "data": {
    "items": [
      // Array of items
    ],
    "totalCount": 150,
    "page": 1,
    "pageSize": 50,
    "totalPages": 3,
    "hasNextPage": true,
    "hasPreviousPage": false
  }
}
```

**Pagination Fields:**
- `items` - Array of data items
- `totalCount` - Total number of items across all pages
- `page` - Current page number (1-based)
- `pageSize` - Number of items per page
- `totalPages` - Total number of pages
- `hasNextPage` - Whether there's a next page
- `hasPreviousPage` - Whether there's a previous page

---

## Error Handling

### HTTP Status Codes

The API uses standard HTTP status codes:

| Status Code | Meaning | When Used |
|------------|---------|-----------|
| 200 OK | Success | Request completed successfully |
| 201 Created | Created | Resource created successfully |
| 400 Bad Request | Client Error | Invalid request data, validation errors |
| 404 Not Found | Not Found | Resource doesn't exist |
| 500 Internal Server Error | Server Error | Unexpected server error |

### Common Error Scenarios

#### 1. Item Not Found (404)

**Request:**
```
GET /api/items/products/invalid-id
```

**Response:**
```json
{
  "success": false,
  "message": "Product not found"
}
```

#### 2. Validation Error (400)

**Request:**
```
POST /api/orders/waiting-transactions
{
  "cartItems": [],
  "staffId": null
}
```

**Response:**
```json
{
  "success": false,
  "message": "Validation failed",
  "errors": [
    {
      "field": "cartItems",
      "message": "Cart items cannot be empty"
    },
    {
      "field": "staffId",
      "message": "Staff ID is required"
    }
  ]
}
```

#### 3. Database Error (500)

**Response:**
```json
{
  "success": false,
  "message": "An error occurred while processing your request. Please try again."
}
```

**Note:** Detailed error messages are logged server-side but not exposed to clients for security reasons.

### Error Handling Best Practices

1. **Always check `success` field** before processing data
2. **Display `message` to users** for user-friendly error messages
3. **Log errors** for debugging and monitoring
4. **Retry on 500 errors** with exponential backoff
5. **Don't retry on 400/404 errors** - fix the request instead

---

## Authentication & Authorization

### Current Implementation

**PottaAPI v1.0 does NOT require authentication** for most endpoints. This is by design for local network usage.

### Staff Authentication

Only the staff login endpoint requires authentication:

**Endpoint:** `POST /api/staff/login`

**Authentication Method:** Daily Code

Each staff member has a unique 4-digit daily code that changes every day. This code is generated by the desktop application and displayed in the staff profile.

**How It Works:**
1. Desktop app generates daily code for each staff member
2. Staff member views their code in desktop app (Profile > Staff QR Code)
3. Staff member enters code in mobile POS app
4. Mobile app calls `/api/staff/login` with the code
5. API validates code and returns staff details
6. Mobile app stores staff ID for subsequent requests

**Security Note:** Since the API runs on a local network (not internet-facing), authentication is minimal. For production deployment over the internet, implement proper authentication (JWT, OAuth, etc.).

---

## Common Workflows

### Workflow 1: Taking an Order in POS

**Scenario:** Waiter takes an order for Table 5

**Steps:**

1. **Staff Login**
   ```
   POST /api/staff/login
   Body: { "dailyCode": "1234" }
   ```
   Response: Staff details (store staffId)

2. **Get Available Tables**
   ```
   GET /api/tables
   ```
   Response: List of tables (find Table 5)

3. **Browse Products**
   ```
   GET /api/items/products
   ```
   Response: List of products

4. **Search for Item**
   ```
   GET /api/items/search?searchTerm=burger
   ```
   Response: Matching items

5. **Select Product with Variations**
   ```
   GET /api/items/products/prod-002/variations
   ```
   Response: Variations with attributes (Color, Size)

6. **Get Modifiers**
   ```
   GET /api/items/modifiers
   ```
   Response: Available modifiers

7. **Get Package Options**
   ```
   GET /api/items/products/prod-001/unit-pricing
   ```
   Response: Package options (Single, 6-Pack, Crate)

8. **Create Order**
   ```
   POST /api/orders/waiting-transactions
   Body: {
     "cartItems": [
       {
         "productId": "prod-003",
         "productName": "Hamburger",
         "quantity": 2,
         "price": 1000,
         "modifiers": [
           { "modifierId": "mod-001", "modifierName": "Extra Cheese", "priceChange": 500 }
         ]
       },
       {
         "productId": "prod-001",
         "productName": "Coca-Cola",
         "quantity": 2,
         "price": 500,
         "unitType": "Package",
         "unitsPerPackage": 6,
         "modifiers": []
       }
     ],
     "tableId": "table-005",
     "tableNumber": 5,
     "tableName": "Table 5",
     "staffId": 1
   }
   ```
   Response: Transaction created

9. **Update Table Status**
   ```
   PUT /api/tables/table-005/status
   Body: { "status": "Occupied" }
   ```
   Response: Table status updated

---

### Workflow 2: Viewing Recipe Details

**Scenario:** Kitchen staff wants to see recipe for "Margherita Pizza"

**Steps:**

1. **Get All Recipes**
   ```
   GET /api/items/recipes
   ```
   Response: List of recipes

2. **Get Recipe Details**
   ```
   GET /api/items/bundles/recipe-001
   ```
   Response: Recipe with ingredients, quantities, cooking instructions

3. **Display to Kitchen:**
   - Recipe name: "Margherita Pizza"
   - Serving size: 1
   - Preparation time: 15 minutes
   - Ingredients:
     * Pizza Dough: 250g
     * Tomato Sauce: 100g
     * Mozzarella Cheese: 150g
     * Fresh Basil: 10g
   - Instructions: [Step-by-step cooking instructions]

---

### Workflow 3: Checking Low Stock Items

**Scenario:** Manager wants to see items running low

**Steps:**

1. **Get Low Stock Products**
   ```
   GET /api/items/products/low-stock
   ```
   Response: Products below reorder point

2. **Display Alert:**
   - "Coca-Cola: 15 bottles (reorder at 50)"
   - "French Fries: 5 kg (reorder at 20)"
   - "Hamburger Buns: 8 pieces (reorder at 30)"

3. **Manager Takes Action:**
   - Opens desktop app
   - Creates purchase orders
   - Updates inventory

---

### Workflow 4: Applying Modifiers with Multi-Unit Pricing

**Scenario:** Customer orders Coca-Cola crate with extra ice

**Steps:**

1. **Select Product**
   ```
   GET /api/items/products/prod-001
   ```
   Response: Coca-Cola details

2. **Get Package Options**
   ```
   GET /api/items/products/prod-001/unit-pricing
   ```
   Response: Single (XAF 500), 6-Pack (XAF 2,800), Crate (XAF 10,000)

3. **Get Modifiers**
   ```
   GET /api/items/modifiers
   ```
   Response: Extra Ice (+XAF 0), Extra Cheese (+XAF 500), etc.

4. **Calculate Price:**
   - Base: Crate (24 bottles) = XAF 10,000
   - Modifier: Extra Ice = XAF 0
   - Total: XAF 10,000

5. **Add to Cart:**
   ```json
   {
     "productId": "prod-001",
     "productName": "Coca-Cola",
     "quantity": 1,
     "price": 10000,
     "unitType": "Package",
     "unitsPerPackage": 24,
     "modifiers": [
       { "modifierId": "mod-ice", "modifierName": "Extra Ice", "priceChange": 0 }
     ]
   }
   ```

---

## Troubleshooting

### Issue 1: Assembly Components Appearing in Product List

**Problem:** Products used in assemblies are showing up in POS.

**Cause:** Assembly filtering not working correctly.

**Solution:**
- Check that assembly components are properly linked in `BundleItems` table
- Verify `structure = 'Assembly'` in database
- API automatically filters these - if they appear, it's a data issue

**Fix in Desktop App:**
1. Open Inventory module
2. Find the assembly
3. Verify structure is set to "Assembly" (not "Bundle")
4. Save changes

---

### Issue 2: Modifiers Not Showing Up

**Problem:** Modifiers endpoint returns empty list.

**Cause:** All modifiers are inactive or none exist.

**Solution:**
- Check `Modifiers` table in database
- Verify `status = 1` for active modifiers
- Create modifiers in desktop app if none exist

**Fix in Desktop App:**
1. Open Inventory module > Modifiers tab
2. Check if modifiers exist
3. Activate modifiers (toggle status)
4. Save changes

---

### Issue 3: Multi-Unit Pricing Not Available

**Problem:** Unit pricing endpoint returns empty list.

**Cause:** No package options defined for product.

**Solution:**
- Check `ProductUnitPricing` table in database
- Verify `isActive = 1` for package options
- Create package options in desktop app

**Fix in Desktop App:**
1. Open Inventory module
2. Select product
3. Click "Multi-Unit Pricing" tab
4. Add package options
5. Save changes

---

### Issue 4: Variations Not Loading

**Problem:** Product has variations but endpoint returns empty list.

**Cause:** Product not marked as having variations or variations are inactive.

**Solution:**
- Check `hasVariations = 1` in Products table
- Check `status = 1` in ProductVariations table
- Verify attribute values are properly linked

**Fix in Desktop App:**
1. Open Inventory module
2. Select product
3. Click "Variations" tab
4. Verify variations exist and are active
5. Save changes

---

### Issue 5: Search Not Finding Items

**Problem:** Search endpoint returns no results for known items.

**Cause:** Search term doesn't match name, SKU, description, or categories.

**Solution:**
- Try different search terms
- Check spelling
- Search by SKU instead of name
- Verify item is active (`status = 1`)

**Tips:**
- Search is case-insensitive
- Partial matches work (searching "cola" finds "Coca-Cola")
- Search includes categories (searching "beverage" finds all beverages)

---

## API Versioning

**Current Version:** 1.0

**Versioning Strategy:** URL-based versioning (future)

**Current Base URL:** `/api/`

**Future Base URL:** `/api/v1/`, `/api/v2/`, etc.

**Breaking Changes:** Will be introduced in new versions only. v1 will remain stable.

**Deprecation Policy:** Old versions will be supported for at least 6 months after new version release.

---

## Performance Considerations

### Response Times

Expected response times on local network:

| Endpoint Type | Expected Time | Notes |
|--------------|---------------|-------|
| Get by ID | < 50ms | Single database query |
| Get All (no pagination) | < 200ms | Full table scan |
| Search (with pagination) | < 300ms | Complex queries with filtering |
| Create/Update | < 100ms | Single write operation |

### Optimization Tips

1. **Use Pagination:** Always use pagination for large lists
2. **Cache Static Data:** Cache categories, modifiers, floor plans
3. **Minimize Requests:** Batch operations when possible
4. **Use Search:** Use search endpoint instead of loading all items
5. **Filter Early:** Use category filters to reduce data transfer

### Database Performance

The API uses SQLite with these optimizations:

- Indexed columns: `productId`, `bundleId`, `sku`, `status`
- Connection pooling enabled
- Read-only connections for GET requests
- Write-ahead logging (WAL) mode enabled

---

## Data Synchronization

### Desktop App as Source of Truth

**Important:** The desktop application is the **master database**. All data changes must be made in the desktop app.

### How Sync Works

1. **Desktop App:** Admin makes changes (add products, update prices, etc.)
2. **Database:** Changes are saved to SQLite database
3. **API:** API reads from the same database (real-time sync)
4. **Mobile App:** Mobile app fetches latest data from API

### No Caching Required

Since the API reads directly from the desktop app's database, there's no need for caching or manual sync. Changes in the desktop app are immediately available via the API.

### Conflict Resolution

Not applicable - API is read-only for most operations. Only waiting transactions can be created/updated via API, and these are separate from master data.

---

## Security Considerations

### Local Network Only

**PottaAPI is designed for local network use only.** Do not expose it to the internet without proper security measures.

### Security Measures for Internet Deployment

If you need to deploy over the internet, implement:

1. **HTTPS:** Use SSL/TLS certificates
2. **Authentication:** Implement JWT or OAuth 2.0
3. **Authorization:** Role-based access control (RBAC)
4. **Rate Limiting:** Prevent abuse
5. **Input Validation:** Already implemented, but review for security
6. **SQL Injection Protection:** Already using parameterized queries
7. **CORS:** Configure allowed origins
8. **API Keys:** Require API keys for all requests

### Current Security Features

- ✅ Input validation on all endpoints
- ✅ Parameterized SQL queries (no SQL injection)
- ✅ Error message sanitization (no sensitive data in errors)
- ✅ CORS configured for local network
- ❌ No authentication (local network only)
- ❌ No rate limiting (not needed for local network)
- ❌ No HTTPS (not needed for local network)

---

## Testing the API

### Using Postman

1. **Import Collection:** (Create Postman collection from this documentation)
2. **Set Base URL:** `http://localhost:5000/api`
3. **Test Endpoints:** Start with simple GET requests
4. **Verify Responses:** Check `success` field and data structure

### Using cURL

**Example: Get All Products**
```bash
curl -X GET http://localhost:5000/api/items/products
```

**Example: Search Items**
```bash
curl -X GET "http://localhost:5000/api/items/search?searchTerm=cola"
```

**Example: Create Waiting Transaction**
```bash
curl -X POST http://localhost:5000/api/orders/waiting-transactions \
  -H "Content-Type: application/json" \
  -d '{
    "cartItems": [
      {
        "productId": "prod-001",
        "productName": "Coca-Cola",
        "quantity": 2,
        "price": 500,
        "modifiers": []
      }
    ],
    "tableId": "table-001",
    "tableNumber": 1,
    "tableName": "Table 1",
    "staffId": 1
  }'
```

### Using Browser

Simple GET requests can be tested directly in browser:

```
http://localhost:5000/api/items/products
http://localhost:5000/api/items/modifiers
http://localhost:5000/api/tables
```

---

## Support & Contact

### Documentation Updates

This documentation is maintained alongside the API. For the latest version, check:

- `PottaAPI/COMPLETE_API_DOCUMENTATION.md` in the source code
- API version endpoint: `GET /api/version` (if implemented)

### Reporting Issues

If you encounter issues:

1. Check the Troubleshooting section
2. Verify desktop app data is correct
3. Check API logs in `PottaAPI/logs/` folder
4. Contact development team with:
   - Endpoint URL
   - Request body (if applicable)
   - Error message
   - Expected vs actual behavior

---

## Appendix

### Database Schema Reference

**Key Tables:**

- `Products` - Individual products
- `ProductVariations` - Product variations
- `BundleItems` - Bundles, assemblies, and recipes
- `Modifiers` - Product modifiers
- `ProductUnitPricing` - Multi-unit pricing options
- `Categories` - Product categories
- `Tables` - Restaurant tables
- `Seats` - Table seats
- `FloorPlans` - Floor plan layouts
- `Customers` - Customer information
- `Staff` - Staff members
- `WaitingTransactions` - Orders in progress

### Glossary

- **Assembly:** Composite item where components cannot be sold separately
- **Bundle:** Grouped items where components can be sold separately
- **Component:** Product that is part of a bundle or assembly
- **Ingredient:** Product marked as ingredient (used in recipes, not sold directly)
- **Modifier:** Customization that changes product price
- **Multi-Unit Pricing:** Different package sizes for the same product
- **Recipe:** Special bundle with cooking instructions
- **Variation:** Different version of the same product (e.g., colors, sizes)
- **Waiting Transaction:** Order in progress (not yet completed)

---

**End of Documentation**

**Version:** 1.0  
**Last Updated:** February 9, 2026  
**API Base URL:** `http://localhost:5000/api`

