using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PottaAPI.Models
{
    #region Table DTOs

    /// <summary>
    /// DTO for table information
    /// </summary>
    public class TableDTO
    {
        public string TableId { get; set; }
        public string? TableName { get; set; }
        public int TableNumber { get; set; }
        public int Capacity { get; set; }
        public string Status { get; set; } // Available, Occupied, Unpaid, Reserved, Cleaning, OutOfOrder
        public string? CurrentCustomerId { get; set; }
        public string? CurrentTransactionId { get; set; }
        public string? Description { get; set; }
        public string? Size { get; set; }
        public string? Shape { get; set; }
        public DateTime? ReservationDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }

        // Computed properties
        public string DisplayName => !string.IsNullOrEmpty(TableName) ? TableName : $"Table {TableNumber}";
        public bool IsAvailable => Status == "Available";
        public bool IsOccupied => Status == "Occupied";
        public bool IsUnpaid => Status == "Unpaid";
    }

    /// <summary>
    /// DTO for creating a new table
    /// </summary>
    public class CreateTableDTO
    {
        [Required]
        public int TableNumber { get; set; }

        public string? TableName { get; set; }

        [Range(1, 100)]
        public int Capacity { get; set; } = 4;

        public string? Description { get; set; }
        public string? Size { get; set; }
        public string? Shape { get; set; }
    }

    /// <summary>
    /// DTO for updating table information
    /// </summary>
    public class UpdateTableDTO
    {
        public string? TableName { get; set; }
        public int? TableNumber { get; set; }

        [Range(1, 100)]
        public int? Capacity { get; set; }

        public string? Description { get; set; }
        public string? Size { get; set; }
        public string? Shape { get; set; }
        public bool? IsActive { get; set; }
    }

    /// <summary>
    /// DTO for updating table status
    /// </summary>
    public class UpdateTableStatusDTO
    {
        [Required]
        public string Status { get; set; } // Available, Occupied, Unpaid, Reserved, Cleaning, OutOfOrder

        public string? CustomerId { get; set; }
        public string? TransactionId { get; set; }
    }

    /// <summary>
    /// DTO for table reservation
    /// </summary>
    public class ReserveTableDTO
    {
        public string? CustomerId { get; set; }
        public DateTime? ReservationDate { get; set; }
    }

    #endregion

    #region Seat DTOs

    /// <summary>
    /// DTO for seat information
    /// </summary>
    public class SeatDTO
    {
        public string SeatId { get; set; }
        public string TableId { get; set; }
        public int SeatNumber { get; set; }
        public string Status { get; set; } // Available, Occupied, Reserved, Not Available
        public string? CustomerId { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }

        // Computed properties
        public string DisplayName => $"Seat {SeatNumber}";
        public bool IsAvailable => Status == "Available";
        public bool IsOccupied => Status == "Occupied";
        public bool IsReserved => Status == "Reserved";
    }

    /// <summary>
    /// DTO for creating seats for a table
    /// </summary>
    public class CreateSeatsDTO
    {
        [Required]
        public string TableId { get; set; }

        [Required]
        [Range(1, 20)]
        public int NumberOfSeats { get; set; }
    }

    /// <summary>
    /// DTO for updating seat status
    /// </summary>
    public class UpdateSeatStatusDTO
    {
        [Required]
        public string Status { get; set; } // Available, Occupied, Reserved, Not Available

        public string? CustomerId { get; set; }
    }

    /// <summary>
    /// DTO for selecting multiple seats
    /// </summary>
    public class SelectSeatsDTO
    {
        [Required]
        public string TableId { get; set; }

        [Required]
        public List<int> SeatNumbers { get; set; }

        public string? CustomerId { get; set; }
    }

    #endregion

    #region Response DTOs

    /// <summary>
    /// Response DTO for table with seats
    /// </summary>
    public class TableWithSeatsDTO
    {
        public TableDTO Table { get; set; }
        public List<SeatDTO> Seats { get; set; }
        public int OccupiedSeatsCount { get; set; }
        public int AvailableSeatsCount { get; set; }
    }

    /// <summary>
    /// Response DTO for table availability
    /// </summary>
    public class TableAvailabilityDTO
    {
        public string TableId { get; set; }
        public string DisplayName { get; set; }
        public bool IsAvailable { get; set; }
        public int TotalSeats { get; set; }
        public int AvailableSeats { get; set; }
        public int OccupiedSeats { get; set; }
        public string Status { get; set; }
        public bool HasPendingTransactions { get; set; }
    }

    /// <summary>
    /// Response DTO for table summary statistics
    /// </summary>
    public class TableSummaryDTO
    {
        public int TotalTables { get; set; }
        public int AvailableTables { get; set; }
        public int OccupiedTables { get; set; }
        public int ReservedTables { get; set; }
        public int UnpaidTables { get; set; }
        public int NotAvailableTables { get; set; }

        // Computed properties
        public double OccupancyRate => TotalTables > 0 ? (double)OccupiedTables / TotalTables * 100 : 0;
        public int BusyTables => OccupiedTables + ReservedTables + UnpaidTables;
    }

    #endregion
}
