using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PottaAPI.Models;

namespace PottaAPI.Services
{
    public class TableService : ITableService
    {
        private readonly string _connectionString;

        public TableService(string connectionString)
        {
            _connectionString = connectionString;
        }

        #region Table Operations

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

        public async Task<TableDTO?> GetTableByNumberAsync(int tableNumber)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT branchId, orgId, tableId, tableName, tableNumber, capacity, status, 
                       currentCustomerId, currentTransactionId, description, size, shape, reservationDate,
                       isActive, createdDate, modifiedDate, createdBy, updatedBy, isSynced, lastSynced
                FROM Tables 
                WHERE tableNumber = @tableNumber AND isActive = 1";
            command.Parameters.AddWithValue("@tableNumber", tableNumber);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapReaderToTableDTO(reader);
            }

            return null;
        }

        public async Task<TableDTO> CreateTableAsync(CreateTableDTO createDto)
        {
            // Check if table number already exists
            var existingTable = await GetTableByNumberAsync(createDto.TableNumber);
            if (existingTable != null)
            {
                throw new InvalidOperationException($"A table with number {createDto.TableNumber} already exists.");
            }

            var tableId = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Tables (
                    tableId, tableName, tableNumber, capacity, status,
                    description, size, shape,
                    isActive, createdDate, modifiedDate, isSynced, lastSynced
                ) VALUES (
                    @tableId, @tableName, @tableNumber, @capacity, @status,
                    @description, @size, @shape,
                    @isActive, @createdDate, @modifiedDate, @isSynced, @lastSynced
                )";

            command.Parameters.AddWithValue("@tableId", tableId);
            command.Parameters.AddWithValue("@tableName", createDto.TableName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@tableNumber", createDto.TableNumber);
            command.Parameters.AddWithValue("@capacity", createDto.Capacity);
            command.Parameters.AddWithValue("@status", "Available");
            command.Parameters.AddWithValue("@description", createDto.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@size", createDto.Size ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@shape", createDto.Shape ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@isActive", true);
            command.Parameters.AddWithValue("@createdDate", now);
            command.Parameters.AddWithValue("@modifiedDate", now);
            command.Parameters.AddWithValue("@isSynced", false);
            command.Parameters.AddWithValue("@lastSynced", now);

            await command.ExecuteNonQueryAsync();

            // Create seats for the table
            await CreateSeatsForTableInternalAsync(tableId, createDto.Capacity);

            return await GetTableByIdAsync(tableId);
        }

        public async Task<TableDTO> UpdateTableAsync(string tableId, UpdateTableDTO updateDto)
        {
            var existingTable = await GetTableByIdAsync(tableId);
            if (existingTable == null)
            {
                throw new KeyNotFoundException($"Table with ID {tableId} not found.");
            }

            // Check if table number is being changed and if it conflicts
            if (updateDto.TableNumber.HasValue && updateDto.TableNumber.Value != existingTable.TableNumber)
            {
                var conflictingTable = await GetTableByNumberAsync(updateDto.TableNumber.Value);
                if (conflictingTable != null && conflictingTable.TableId != tableId)
                {
                    throw new InvalidOperationException($"A table with number {updateDto.TableNumber.Value} already exists.");
                }
            }

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Tables SET
                    tableName = COALESCE(@tableName, tableName),
                    tableNumber = COALESCE(@tableNumber, tableNumber),
                    capacity = COALESCE(@capacity, capacity),
                    description = COALESCE(@description, description),
                    size = COALESCE(@size, size),
                    shape = COALESCE(@shape, shape),
                    isActive = COALESCE(@isActive, isActive),
                    modifiedDate = @modifiedDate,
                    isSynced = 0
                WHERE tableId = @tableId";

            command.Parameters.AddWithValue("@tableId", tableId);
            command.Parameters.AddWithValue("@tableName", updateDto.TableName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@tableNumber", updateDto.TableNumber ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@capacity", updateDto.Capacity ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@description", updateDto.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@size", updateDto.Size ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@shape", updateDto.Shape ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@isActive", updateDto.IsActive ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@modifiedDate", DateTime.UtcNow);

            await command.ExecuteNonQueryAsync();

            // Update seats if capacity changed
            if (updateDto.Capacity.HasValue && updateDto.Capacity.Value != existingTable.Capacity)
            {
                await UpdateSeatsForCapacityChangeAsync(tableId, updateDto.Capacity.Value);
            }

            return await GetTableByIdAsync(tableId);
        }

        public async Task<bool> DeleteTableAsync(string tableId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Tables SET
                    isActive = 0,
                    modifiedDate = @modifiedDate,
                    isSynced = 0
                WHERE tableId = @tableId";

            command.Parameters.AddWithValue("@modifiedDate", DateTime.UtcNow);
            command.Parameters.AddWithValue("@tableId", tableId);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }

        #endregion

        #region Table Status Operations

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

        public async Task<bool> ClearTableAsync(string tableId)
        {
            var statusDto = new UpdateTableStatusDTO
            {
                Status = "Available",
                CustomerId = null,
                TransactionId = null
            };

            return await UpdateTableStatusAsync(tableId, statusDto);
        }

        public async Task<bool> ReserveTableAsync(string tableId, ReserveTableDTO reserveDto)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Tables SET
                    status = 'Reserved',
                    currentCustomerId = @customerId,
                    reservationDate = @reservationDate,
                    modifiedDate = @modifiedDate,
                    isSynced = 0
                WHERE tableId = @tableId";

            command.Parameters.AddWithValue("@customerId", reserveDto.CustomerId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@reservationDate", reserveDto.ReservationDate ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@modifiedDate", DateTime.UtcNow);
            command.Parameters.AddWithValue("@tableId", tableId);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }

        public async Task<bool> SetTableNotAvailableAsync(string tableId)
        {
            var statusDto = new UpdateTableStatusDTO
            {
                Status = "Not Available"
            };

            return await UpdateTableStatusAsync(tableId, statusDto);
        }

        public async Task<bool> SetTableUnpaidAsync(string tableId, string? customerId = null, string? transactionId = null)
        {
            var statusDto = new UpdateTableStatusDTO
            {
                Status = "Unpaid",
                CustomerId = customerId,
                TransactionId = transactionId
            };

            return await UpdateTableStatusAsync(tableId, statusDto);
        }

        #endregion

        #region Table Transaction Operations

        public async Task<bool> HasPendingTransactionsAsync(string tableId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COUNT(*) FROM Transactions
                WHERE status IN ('Pending', 'Delayed')
                AND transactionId IN (
                    SELECT currentTransactionId FROM Tables
                    WHERE tableId = @tableId AND currentTransactionId IS NOT NULL
                )";

            command.Parameters.AddWithValue("@tableId", tableId);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }

        public async Task<bool> UpdateTableStatusBasedOnTransactionsAsync(string tableId)
        {
            var hasPending = await HasPendingTransactionsAsync(tableId);

            if (hasPending)
            {
                // Keep current customer and transaction info
                var table = await GetTableByIdAsync(tableId);
                if (table != null)
                {
                    return await SetTableUnpaidAsync(tableId, table.CurrentCustomerId, table.CurrentTransactionId);
                }
            }
            else
            {
                // Clear table - no pending transactions
                return await ClearTableAsync(tableId);
            }

            return false;
        }

        public async Task UpdateAllTablesStatusBasedOnTransactionsAsync()
        {
            var tables = await GetAllTablesAsync();
            foreach (var table in tables)
            {
                if (table.Status == "Unpaid" || !string.IsNullOrEmpty(table.CurrentTransactionId))
                {
                    await UpdateTableStatusBasedOnTransactionsAsync(table.TableId);
                }
            }
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

        public async Task<SeatDTO?> GetSeatByIdAsync(string seatId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT seatId, tableId, seatNumber, status, customerId, isActive,
                       createdDate, modifiedDate, createdBy, updatedBy, isSynced, lastSynced
                FROM Seats 
                WHERE seatId = @seatId";

            command.Parameters.AddWithValue("@seatId", seatId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapReaderToSeatDTO(reader);
            }

            return null;
        }

        public async Task<List<SeatDTO>> CreateSeatsForTableAsync(CreateSeatsDTO createDto)
        {
            await CreateSeatsForTableInternalAsync(createDto.TableId, createDto.NumberOfSeats);
            return await GetTableSeatsAsync(createDto.TableId);
        }

        private async Task CreateSeatsForTableInternalAsync(string tableId, int numberOfSeats)
        {
            var existingSeats = await GetTableSeatsAsync(tableId);
            var now = DateTime.UtcNow;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Create new seats if needed
            for (int i = existingSeats.Count + 1; i <= numberOfSeats; i++)
            {
                var seatId = Guid.NewGuid().ToString();
                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO Seats (
                        seatId, tableId, seatNumber, status, isActive,
                        createdDate, modifiedDate, isSynced, lastSynced
                    ) VALUES (
                        @seatId, @tableId, @seatNumber, @status, @isActive,
                        @createdDate, @modifiedDate, @isSynced, @lastSynced
                    )";

                command.Parameters.AddWithValue("@seatId", seatId);
                command.Parameters.AddWithValue("@tableId", tableId);
                command.Parameters.AddWithValue("@seatNumber", i);
                command.Parameters.AddWithValue("@status", "Available");
                command.Parameters.AddWithValue("@isActive", true);
                command.Parameters.AddWithValue("@createdDate", now);
                command.Parameters.AddWithValue("@modifiedDate", now);
                command.Parameters.AddWithValue("@isSynced", false);
                command.Parameters.AddWithValue("@lastSynced", now);

                await command.ExecuteNonQueryAsync();
            }
        }

        private async Task UpdateSeatsForCapacityChangeAsync(string tableId, int newCapacity)
        {
            var existingSeats = await GetTableSeatsAsync(tableId);

            if (existingSeats.Count < newCapacity)
            {
                // Add more seats
                await CreateSeatsForTableInternalAsync(tableId, newCapacity);
            }
            else if (existingSeats.Count > newCapacity)
            {
                // Deactivate extra seats
                var seatsToDeactivate = existingSeats.Where(s => s.SeatNumber > newCapacity).ToList();

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                foreach (var seat in seatsToDeactivate)
                {
                    var command = connection.CreateCommand();
                    command.CommandText = @"
                        UPDATE Seats SET
                            isActive = 0,
                            status = 'Not Available',
                            customerId = NULL,
                            modifiedDate = @modifiedDate
                        WHERE seatId = @seatId";

                    command.Parameters.AddWithValue("@modifiedDate", DateTime.UtcNow);
                    command.Parameters.AddWithValue("@seatId", seat.SeatId);

                    await command.ExecuteNonQueryAsync();
                }
            }
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

        public async Task<bool> SelectSeatsAsync(SelectSeatsDTO selectDto)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            foreach (var seatNumber in selectDto.SeatNumbers)
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE Seats SET
                        status = 'Occupied',
                        customerId = @customerId,
                        modifiedDate = @modifiedDate
                    WHERE tableId = @tableId AND seatNumber = @seatNumber";

                command.Parameters.AddWithValue("@tableId", selectDto.TableId);
                command.Parameters.AddWithValue("@seatNumber", seatNumber);
                command.Parameters.AddWithValue("@customerId", selectDto.CustomerId ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@modifiedDate", DateTime.UtcNow);

                await command.ExecuteNonQueryAsync();
            }

            return true;
        }

        public async Task<bool> ClearSeatAsync(string seatId)
        {
            var statusDto = new UpdateSeatStatusDTO
            {
                Status = "Available",
                CustomerId = null
            };

            return await UpdateSeatStatusAsync(seatId, statusDto);
        }

        public async Task<bool> ClearAllSeatsForTableAsync(string tableId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Seats SET
                    status = 'Available',
                    customerId = NULL,
                    modifiedDate = @modifiedDate
                WHERE tableId = @tableId AND isActive = 1";

            command.Parameters.AddWithValue("@tableId", tableId);
            command.Parameters.AddWithValue("@modifiedDate", DateTime.UtcNow);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }

        #endregion

        #region Combined Operations

        public async Task<TableWithSeatsDTO?> GetTableWithSeatsAsync(string tableId)
        {
            var table = await GetTableByIdAsync(tableId);
            if (table == null)
            {
                return null;
            }

            var seats = await GetTableSeatsAsync(tableId);

            return new TableWithSeatsDTO
            {
                Table = table,
                Seats = seats,
                OccupiedSeatsCount = seats.Count(s => s.Status == "Occupied"),
                AvailableSeatsCount = seats.Count(s => s.Status == "Available")
            };
        }

        public async Task<List<TableAvailabilityDTO>> GetTablesAvailabilityAsync()
        {
            var tables = await GetAllTablesAsync();
            var availabilityList = new List<TableAvailabilityDTO>();

            foreach (var table in tables)
            {
                var seats = await GetTableSeatsAsync(table.TableId);
                var hasPending = await HasPendingTransactionsAsync(table.TableId);

                availabilityList.Add(new TableAvailabilityDTO
                {
                    TableId = table.TableId,
                    DisplayName = table.DisplayName,
                    IsAvailable = table.IsAvailable,
                    TotalSeats = seats.Count,
                    AvailableSeats = seats.Count(s => s.Status == "Available"),
                    OccupiedSeats = seats.Count(s => s.Status == "Occupied"),
                    Status = table.Status,
                    HasPendingTransactions = hasPending
                });
            }

            return availabilityList;
        }

        public async Task<TableSummaryDTO> GetTableSummaryAsync()
        {
            var tables = await GetAllTablesAsync();

            return new TableSummaryDTO
            {
                TotalTables = tables.Count,
                AvailableTables = tables.Count(t => t.Status == "Available"),
                OccupiedTables = tables.Count(t => t.Status == "Occupied"),
                ReservedTables = tables.Count(t => t.Status == "Reserved"),
                UnpaidTables = tables.Count(t => t.Status == "Unpaid"),
                NotAvailableTables = tables.Count(t => t.Status == "Not Available")
            };
        }

        #endregion

        #region Initialization

        public async Task InitializeDefaultTablesAsync()
        {
            var existingTables = await GetAllTablesAsync();
            if (existingTables.Count > 0)
            {
                return; // Tables already exist
            }

            // Create default tables (1-3)
            for (int i = 1; i <= 3; i++)
            {
                var createDto = new CreateTableDTO
                {
                    TableNumber = i,
                    TableName = $"Table {i}",
                    Capacity = 4,
                    Description = i <= 10 ? "Section: Main Dining" : "Section: Patio"
                };

                await CreateTableAsync(createDto);
            }
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
