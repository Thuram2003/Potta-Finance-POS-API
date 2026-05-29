using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PottaAPI.Models;
using Dapper;
using PottaAPI.Services.Interfaces;

namespace PottaAPI.Services
{
    // Table service for mobile operations (read-only, status updates)
    public class TableService : ITableService
    {
        private readonly string _connectionString;

        public TableService(IConnectionStringProvider connectionStringProvider)
        {
            _connectionString = connectionStringProvider.GetConnectionString();
        }

        #region Core Table Operations

        public async Task<List<TableDTO>> GetAllTablesAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            
            var sql = @"
                SELECT branchId, orgId, tableId, tableName, tableNumber, capacity, status, 
                       currentCustomerId, currentTransactionId, description, size, shape, reservationDate,
                       isActive, createdDate, modifiedDate, createdBy, updatedBy, isSynced, lastSynced
                FROM Tables 
                WHERE isActive = 1 
                ORDER BY tableNumber";

            var tables = await connection.QueryAsync<TableDTO>(sql);
            return tables.ToList();
        }

        public async Task<List<TableDTO>> GetAvailableTablesAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            
            var sql = @"
                SELECT branchId, orgId, tableId, tableName, tableNumber, capacity, status, 
                       currentCustomerId, currentTransactionId, description, size, shape, reservationDate,
                       isActive, createdDate, modifiedDate, createdBy, updatedBy, isSynced, lastSynced
                FROM Tables 
                WHERE isActive = 1 AND status = 'Available'
                ORDER BY tableNumber";

            var tables = await connection.QueryAsync<TableDTO>(sql);
            return tables.ToList();
        }

        public async Task<TableDTO?> GetTableByIdAsync(string tableId)
        {
            using var connection = new SqliteConnection(_connectionString);
            
            var sql = @"
                SELECT branchId, orgId, tableId, tableName, tableNumber, capacity, status, 
                       currentCustomerId, currentTransactionId, description, size, shape, reservationDate,
                       isActive, createdDate, modifiedDate, createdBy, updatedBy, isSynced, lastSynced
                FROM Tables 
                WHERE tableId = @tableId";

            return await connection.QueryFirstOrDefaultAsync<TableDTO>(sql, new { tableId });
        }

        public async Task<bool> UpdateTableStatusAsync(string tableId, UpdateTableStatusDTO statusDto)
        {
            using var connection = new SqliteConnection(_connectionString);
            
            var sql = @"
                UPDATE Tables SET
                    status = @status,
                    currentCustomerId = @customerId,
                    currentTransactionId = @transactionId,
                    modifiedDate = @modifiedDate,
                    isSynced = 0
                WHERE tableId = @tableId";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                status = statusDto.Status,
                customerId = statusDto.CustomerId,
                transactionId = statusDto.TransactionId,
                modifiedDate = DateTime.UtcNow,
                tableId
            });

            return rowsAffected > 0;
        }

        #endregion

        #region Seat Operations

        public async Task<List<SeatDTO>> GetTableSeatsAsync(string tableId)
        {
            using var connection = new SqliteConnection(_connectionString);
            
            var sql = @"
                SELECT seatId, tableId, seatNumber, status, customerId, isActive,
                       createdDate, modifiedDate, createdBy, updatedBy, isSynced, lastSynced
                FROM Seats 
                WHERE tableId = @tableId AND isActive = 1 
                ORDER BY seatNumber";

            var seats = await connection.QueryAsync<SeatDTO>(sql, new { tableId });
            return seats.ToList();
        }

        public async Task<bool> UpdateSeatStatusAsync(string seatId, UpdateSeatStatusDTO statusDto)
        {
            using var connection = new SqliteConnection(_connectionString);
            
            var sql = @"
                UPDATE Seats SET
                    status = @status,
                    customerId = @customerId,
                    modifiedDate = @modifiedDate
                WHERE seatId = @seatId";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                seatId,
                status = statusDto.Status,
                customerId = statusDto.CustomerId,
                modifiedDate = DateTime.UtcNow
            });

            return rowsAffected > 0;
        }

        public async Task<bool> AreAllSeatsOccupiedAsync(string tableId)
        {
            using var connection = new SqliteConnection(_connectionString);
            
            var sql = @"
                SELECT COUNT(*) as TotalSeats,
                       SUM(CASE WHEN status = 'Occupied' THEN 1 ELSE 0 END) as OccupiedSeats
                FROM Seats
                WHERE tableId = @tableId AND isActive = 1";

            var result = await connection.QueryFirstOrDefaultAsync<(int TotalSeats, int OccupiedSeats)>(sql, new { tableId });

            // All seats are occupied if total > 0 and total == occupied
            return result.TotalSeats > 0 && result.TotalSeats == result.OccupiedSeats;
        }

        public async Task<bool> AnySeatsOccupiedAsync(string tableId)
        {
            using var connection = new SqliteConnection(_connectionString);
            
            var sql = @"
                SELECT COUNT(*)
                FROM Seats
                WHERE tableId = @tableId AND isActive = 1 AND status = 'Occupied'";

            var count = await connection.ExecuteScalarAsync<int>(sql, new { tableId });
            return count > 0;
        }

        #endregion
    }
}
