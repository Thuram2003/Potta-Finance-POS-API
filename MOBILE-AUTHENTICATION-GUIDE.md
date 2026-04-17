# Mobile Authentication Guide - Potta POS API

## Overview

This guide explains how mobile developers can implement staff authentication for the Potta POS system. The system supports two authentication methods:

1. **Manual Login** - Staff enters a 4-digit daily code
2. **QR Code Scanning** - Staff scans a QR code containing authentication data

## Important Notes

- Staff management (CRUD operations) is **desktop-only**
- Mobile apps have **read-only** access to staff authentication
- Daily codes expire after **24 hours**
- All codes are **4-digit numeric** values (1000-9999)
- Staff accounts must be **active** to authenticate

--