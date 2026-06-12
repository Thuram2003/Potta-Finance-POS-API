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

            var tables = (await connection.QueryAsync<TableDTO>(sql)).ToList();

            // Fetch seat summaries for all tables in one query
            if (tables.Count > 0)
            {
                var seatSummarySql = @"
                    SELECT 
                        tableId,
                        COUNT(*) as TotalSeats,
                        SUM(CASE WHEN status = 'Occupied' THEN 1 ELSE 0 END) as OccupiedSeats,
                        SUM(CASE WHEN status = 'Available' THEN 1 ELSE 0 END) as AvailableSeats,
                        SUM(CASE WHEN status = 'Reserved' THEN 1 ELSE 0 END) as ReservedSeats
                    FROM Seats
                    WHERE isActive = 1
                    GROUP BY tableId";

                var summaries = (await connection.QueryAsync(seatSummarySql))
                    .ToDictionary(
                        r => (string)r.tableId,
                        r => new TableSeatSummary
                        {
                            TotalSeats = (int)r.TotalSeats,
                            OccupiedSeats = (int)(r.OccupiedSeats ?? 0),
                            AvailableSeats = (int)(r.AvailableSeats ?? 0),
                            ReservedSeats = (int)(r.ReservedSeats ?? 0)
                        });

                foreach (var table in tables)
                {
                    if (summaries.TryGetValue(table.TableId, out var summary))
                        table.SeatSummary = summary;
                }
            }

            return tables;
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

            var table = await connection.QueryFirstOrDefaultAsync<TableDTO>(sql, new { tableId });

            if (table != null)
            {
                var seatSummarySql = @"
                    SELECT 
                        COUNT(*) as TotalSeats,
                        SUM(CASE WHEN status = 'Occupied' THEN 1 ELSE 0 END) as OccupiedSeats,
                        SUM(CASE WHEN status = 'Available' THEN 1 ELSE 0 END) as AvailableSeats,
                        SUM(CASE WHEN status = 'Reserved' THEN 1 ELSE 0 END) as ReservedSeats
                    FROM Seats
                    WHERE tableId = @tableId AND isActive = 1";

                var summary = await connection.QueryFirstOrDefaultAsync(seatSummarySql, new { tableId });
                if (summary != null)
                {
                    table.SeatSummary = new TableSeatSummary
                    {
                        TotalSeats = (int)summary.TotalSeats,
                        OccupiedSeats = (int)(summary.OccupiedSeats ?? 0),
                        AvailableSeats = (int)(summary.AvailableSeats ?? 0),
                        ReservedSeats = (int)(summary.ReservedSeats ?? 0)
                    };
                }
            }

            return table;
        }

        public async Task<bool> UpdateTableStatusAsync(string tableId, UpdateTableStatusDTO statusDto)
        {
            using var connection = new SqliteConnection(_connectionString);

            // When setting Occupied, verify seat state first:
            // - If ANY seats exist, only mark Occupied when ALL are occupied
            // - If no seats exist (non-seat table), allow direct status update
            if (statusDto.Status == "Occupied")
            {
                var seatCheckSql = @"
                    SELECT 
                        COUNT(*) as totalSeats,
                        SUM(CASE WHEN status = 'Occupied' THEN 1 ELSE 0 END) as occupiedSeats
                    FROM Seats
                    WHERE tableId = @tableId AND isActive = 1";

                var result = await connection.QueryFirstOrDefaultAsync<(int TotalSeats, int OccupiedSeats)>(
                    seatCheckSql, new { tableId });

                if (result.TotalSeats > 0 && result.TotalSeats != result.OccupiedSeats)
                {
                    // Some seats still free — table is partially occupied, keep as Available
                    // so new customers can still be seated at remaining seats
                    statusDto = new UpdateTableStatusDTO
                    {
                        Status = "Available",
                        CustomerId = statusDto.CustomerId,
                        TransactionId = statusDto.TransactionId
                    };
                }
            }

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
