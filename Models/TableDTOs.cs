using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PottaAPI.Models
{
    #region Table DTOs

    /// <summary>
    /// DTO for table information
    /// Mobile use case: Display table details in floor plan
    /// </summary>
    public class TableDTO
    {
        public string TableId { get; set; }
        public string? TableName { get; set; }
        public int TableNumber { get; set; }
        public int Capacity { get; set; }
        public string Status { get; set; } // Available, Occupied, Reserved, Not Available
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
        public bool IsReserved => Status == "Reserved";
        public bool IsNotAvailable => Status == "Not Available";
    }

    /// <summary>
    /// DTO for updating table status (unified endpoint)
    /// Mobile use case: Update table status to Available, Occupied, Reserved, Not Available
    /// Replaces: ClearTable, ReserveTable, SetNotAvailable
    /// </summary>
    public class UpdateTableStatusDTO
    {
        [Required]
        public string Status { get; set; } // Available, Occupied, Reserved, Not Available

        public string? CustomerId { get; set; }
        public string? TransactionId { get; set; }
    }

    #endregion

    #region Seat DTOs

    /// <summary>
    /// DTO for seat information
    /// Mobile use case: Display seat layout and availability
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
    /// DTO for updating seat status (unified endpoint)
    /// Mobile use case: Mark seat as Occupied, Available, Reserved, etc.
    /// Replaces: SelectSeats, ClearSeat, ClearAllSeats
    /// </summary>
    public class UpdateSeatStatusDTO
    {
        [Required]
        public string Status { get; set; } // Available, Occupied, Reserved, Not Available

        public string? CustomerId { get; set; }
    }

    #endregion
}
