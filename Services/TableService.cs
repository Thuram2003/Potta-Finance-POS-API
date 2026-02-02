using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PottaAPI.Models;

namespace PottaAPI.Services
{
    /// <summary>
    /// Simplified table service implementation for mobile device operations.
    /// Focuses on read operations and status updates only.
    /// Table creation/editing/deletion is handled by desktop UI.
    /// </summary>
    public class TableService : ITableService
    {
        private readonly string _connectionString;

        public TableService(string connectionString)
        {
            _connectionString = connectionString;
        }

        #region Core Table Operations

        public async Task<List<TableDTO>> GetAllTablesAsync()
        {
            var tables = new List<TableDTO>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT branchId, orgId, tableId, tableName, tableNumber, capacity, status, 
                       currentCustomerId, currentTransactionId, description, size, shape, reservationDate,
                       isActive, createdDate, modifiedDate, createdBy, updatedBy, isSynced, lastSynced
                FROM Tables 
                WHERE isActive = 1 
                ORDER BY tableNumber";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(MapReaderToTableDTO(reader));
            }

            return tables;
        }

        public async Task<List<TableDTO>> GetAvailableTablesAsync()
        {
            var tables = new List<TableDTO>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT branchId, orgId, tableId, tableName, tableNumber, capacity, status, 
                       currentCustomerId, currentTransactionId, description, size, shape, reservationDate,
                       isActive, createdDate, modifiedDate, createdBy, updatedBy, isSynced, lastSynced
                FROM Tables 
                WHERE isActive = 1 AND status = 'Available'
                ORDER BY tableNumber";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(MapReaderToTableDTO(reader));
            }

            return tables;
        }

        public async Task<TableDTO?> GetTableByIdAsync(string tableId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT branchId, orgId, tableId, tableName, tableNumber, capacity, status, 
                       currentCustomerId, currentTransactionId, description, size, shape, reservationDate,
                       isActive, createdDate, modifiedDate, createdBy, updatedBy, isSynced, lastSynced
                FROM Tables 
                WHERE tableId = @tableId";
            command.Parameters.AddWithValue("@tableId", tableId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapReaderToTableDTO(reader);
            }

            return null;
        }

        public async Task<bool> UpdateTableStatusAsync(string tableId, UpdateTableStatusDTO statusDto)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Tables SET
                    status = @status,
                    currentCustomerId = @customerId,
                    currentTransactionId = @transactionId,
                    modifiedDate = @modifiedDate,
                    isSynced = 0
                WHERE tableId = @tableId";

            command.Parameters.AddWithValue("@status", statusDto.Status);
            command.Parameters.AddWithValue("@customerId", statusDto.CustomerId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@transactionId", statusDto.TransactionId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@modifiedDate", DateTime.UtcNow);
            command.Parameters.AddWithValue("@tableId", tableId);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }

        #endregion

        #region Seat Operations

        public async Task<List<SeatDTO>> GetTableSeatsAsync(string tableId)
        {
            var seats = new List<SeatDTO>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT seatId, tableId, seatNumber, status, customerId, isActive,
                       createdDate, modifiedDate, createdBy, updatedBy, isSynced, lastSynced
                FROM Seats 
                WHERE tableId = @tableId AND isActive = 1 
                ORDER BY seatNumber";

            command.Parameters.AddWithValue("@tableId", tableId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                seats.Add(MapReaderToSeatDTO(reader));
            }

            return seats;
        }

        public async Task<bool> UpdateSeatStatusAsync(string seatId, UpdateSeatStatusDTO statusDto)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Seats SET
                    status = @status,
                    customerId = @customerId,
                    modifiedDate = @modifiedDate
                WHERE seatId = @seatId";

            command.Parameters.AddWithValue("@seatId", seatId);
            command.Parameters.AddWithValue("@status", statusDto.Status);
            command.Parameters.AddWithValue("@customerId", statusDto.CustomerId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@modifiedDate", DateTime.UtcNow);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }

        #endregion

        #region Helper Methods

        private TableDTO MapReaderToTableDTO(SqliteDataReader reader)
        {
            return new TableDTO
            {
                TableId = reader["tableId"]?.ToString(),
                TableName = reader["tableName"]?.ToString(),
                TableNumber = Convert.ToInt32(reader["tableNumber"]),
                Capacity = Convert.ToInt32(reader["capacity"]),
                Status = reader["status"]?.ToString() ?? "Available",
                CurrentCustomerId = reader["currentCustomerId"] == DBNull.Value ? null : reader["currentCustomerId"]?.ToString(),
                CurrentTransactionId = reader["currentTransactionId"] == DBNull.Value ? null : reader["currentTransactionId"]?.ToString(),
                Description = reader["description"] == DBNull.Value ? null : reader["description"]?.ToString(),
                Size = reader["size"] == DBNull.Value ? null : reader["size"]?.ToString(),
                Shape = reader["shape"] == DBNull.Value ? null : reader["shape"]?.ToString(),
                ReservationDate = reader["reservationDate"] == DBNull.Value ? null : Convert.ToDateTime(reader["reservationDate"]),
                IsActive = Convert.ToBoolean(reader["isActive"]),
                CreatedDate = Convert.ToDateTime(reader["createdDate"]),
                ModifiedDate = Convert.ToDateTime(reader["modifiedDate"])
            };
        }

        private SeatDTO MapReaderToSeatDTO(SqliteDataReader reader)
        {
            return new SeatDTO
            {
                SeatId = reader["seatId"].ToString(),
                TableId = reader["tableId"].ToString(),
                SeatNumber = Convert.ToInt32(reader["seatNumber"]),
                Status = reader["status"].ToString(),
                CustomerId = reader["customerId"] == DBNull.Value ? null : reader["customerId"].ToString(),
                IsActive = Convert.ToBoolean(reader["isActive"]),
                CreatedDate = Convert.ToDateTime(reader["createdDate"]),
                ModifiedDate = Convert.ToDateTime(reader["modifiedDate"])
            };
        }

        #endregion
    }
}
