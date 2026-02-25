using System.ComponentModel.DataAnnotations;

namespace PottaAPI.Models
{
    // =============================================
    // ADD NOTES DTOs
    // =============================================
    
    /// <summary>
    /// Request to add notes to an order
    /// </summary>
    public class AddNotesRequest
    {
        /// <summary>
        /// Transaction ID to add notes to
        /// </summary>
        /// <example>M20260219143022</example>
        [Required(ErrorMessage = "Transaction ID is required")]
        public string TransactionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Note text content
        /// </summary>
        /// <example>No onions, extra sauce</example>
        [Required(ErrorMessage = "Note text is required")]
        [MaxLength(500, ErrorMessage = "Note text cannot exceed 500 characters")]
        public string NoteText { get; set; } = string.Empty;
        
        /// <summary>
        /// Staff ID who added the note
        /// </summary>
        /// <example>1</example>
        public int? AddedByStaffId { get; set; }
    }
    
    /// <summary>
    /// Response after adding notes
    /// </summary>
    public class AddNotesResponse
    {
        /// <summary>
        /// Transaction ID
        /// </summary>
        public string TransactionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Note text that was added
        /// </summary>
        public string NoteText { get; set; } = string.Empty;
        
        /// <summary>
        /// When the note was added
        /// </summary>
        public DateTime AddedAt { get; set; }
        
        /// <summary>
        /// Success message
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    // =============================================
    // TRANSFER SERVER DTOs
    // =============================================
    
    /// <summary>
    /// Request to transfer an order to a different server/staff
    /// </summary>
    public class TransferServerRequest
    {
        /// <summary>
        /// Transaction ID to transfer
        /// </summary>
        /// <example>M20260219143022</example>
        [Required(ErrorMessage = "Transaction ID is required")]
        public string TransactionId { get; set; } = string.Empty;
        
        /// <summary>
        /// New staff ID to assign the order to
        /// </summary>
        /// <example>5</example>
        [Required(ErrorMessage = "New staff ID is required")]
        public int NewStaffId { get; set; }
        
        /// <summary>
        /// Optional reason for the transfer
        /// </summary>
        /// <example>Shift change</example>
        [MaxLength(200, ErrorMessage = "Reason cannot exceed 200 characters")]
        public string? Reason { get; set; }
    }
    
    /// <summary>
    /// Response after transferring server
    /// </summary>
    public class TransferServerResponse
    {
        /// <summary>
        /// Transaction ID
        /// </summary>
        public string TransactionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Previous staff ID (if any)
        /// </summary>
        public int? PreviousStaffId { get; set; }
        
        /// <summary>
        /// Previous staff name
        /// </summary>
        public string PreviousStaffName { get; set; } = string.Empty;
        
        /// <summary>
        /// New staff ID
        /// </summary>
        public int NewStaffId { get; set; }
        
        /// <summary>
        /// New staff name
        /// </summary>
        public string NewStaffName { get; set; } = string.Empty;
        
        /// <summary>
        /// When the transfer occurred
        /// </summary>
        public DateTime TransferredAt { get; set; }
        
        /// <summary>
        /// Success message
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    // =============================================
    // SHIFT HANDOVER DTOs
    // =============================================
    
    /// <summary>
    /// Request to transfer all orders from one staff to another (shift handover)
    /// </summary>
    public class ShiftHandoverRequest
    {
        /// <summary>
        /// Current staff ID (outgoing staff)
        /// </summary>
        /// <example>3</example>
        [Required(ErrorMessage = "Current staff ID is required")]
        public int CurrentStaffId { get; set; }
        
        /// <summary>
        /// New staff ID (incoming staff)
        /// </summary>
        /// <example>5</example>
        [Required(ErrorMessage = "New staff ID is required")]
        public int NewStaffId { get; set; }
        
        /// <summary>
        /// Optional reason for the handover
        /// </summary>
        /// <example>End of shift</example>
        [MaxLength(200, ErrorMessage = "Reason cannot exceed 200 characters")]
        public string? Reason { get; set; }
    }
    
    /// <summary>
    /// Response after shift handover
    /// </summary>
    public class ShiftHandoverResponse
    {
        /// <summary>
        /// Current staff ID (outgoing)
        /// </summary>
        public int CurrentStaffId { get; set; }
        
        /// <summary>
        /// Current staff name
        /// </summary>
        public string CurrentStaffName { get; set; } = string.Empty;
        
        /// <summary>
        /// New staff ID (incoming)
        /// </summary>
        public int NewStaffId { get; set; }
        
        /// <summary>
        /// New staff name
        /// </summary>
        public string NewStaffName { get; set; } = string.Empty;
        
        /// <summary>
        /// Number of orders transferred
        /// </summary>
        public int OrdersTransferred { get; set; }
        
        /// <summary>
        /// List of transferred transaction IDs
        /// </summary>
        public List<string> TransferredTransactionIds { get; set; } = new();
        
        /// <summary>
        /// When the handover occurred
        /// </summary>
        public DateTime HandoverAt { get; set; }
        
        /// <summary>
        /// Success message
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    // =============================================
    // MOVE ORDER DTOs
    // =============================================
    
    /// <summary>
    /// Request to move an order from one table to another
    /// </summary>
    public class MoveOrderRequest
    {
        /// <summary>
        /// Transaction ID to move
        /// </summary>
        /// <example>M20260219143022</example>
        [Required(ErrorMessage = "Transaction ID is required")]
        public string TransactionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Target table ID to move the order to
        /// </summary>
        /// <example>TBL001</example>
        [Required(ErrorMessage = "Target table ID is required")]
        public string TargetTableId { get; set; } = string.Empty;
        
        /// <summary>
        /// Optional reason for the move
        /// </summary>
        /// <example>Customer requested different table</example>
        [MaxLength(200, ErrorMessage = "Reason cannot exceed 200 characters")]
        public string? Reason { get; set; }
    }
    
    /// <summary>
    /// Response after moving order
    /// </summary>
    public class MoveOrderResponse
    {
        /// <summary>
        /// Transaction ID
        /// </summary>
        public string TransactionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Source table ID
        /// </summary>
        public string? FromTableId { get; set; }
        
        /// <summary>
        /// Source table name
        /// </summary>
        public string FromTableName { get; set; } = string.Empty;
        
        /// <summary>
        /// Target table ID
        /// </summary>
        public string ToTableId { get; set; } = string.Empty;
        
        /// <summary>
        /// Target table name
        /// </summary>
        public string ToTableName { get; set; } = string.Empty;
        
        /// <summary>
        /// When the move occurred
        /// </summary>
        public DateTime MovedAt { get; set; }
        
        /// <summary>
        /// Success message
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    // =============================================
    // PAY ENTIRE BILL DTOs
    // =============================================
    
    /// <summary>
    /// Request from mobile staff to complete payment on desktop
    /// </summary>
    public class PayEntireBillRequest
    {
        /// <summary>
        /// Transaction ID to pay
        /// </summary>
        /// <example>M20260219143022</example>
        [Required(ErrorMessage = "Transaction ID is required")]
        public string TransactionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Staff ID requesting payment
        /// </summary>
        /// <example>1</example>
        [Required(ErrorMessage = "Staff ID is required")]
        public int StaffId { get; set; }
        
        /// <summary>
        /// Optional notes for the payment request
        /// </summary>
        /// <example>Customer ready to pay</example>
        [MaxLength(200, ErrorMessage = "Notes cannot exceed 200 characters")]
        public string? Notes { get; set; }
    }
    
    /// <summary>
    /// Response after creating pay entire bill request
    /// </summary>
    public class PayEntireBillResponse
    {
        /// <summary>
        /// Unique request ID
        /// </summary>
        public string RequestId { get; set; } = string.Empty;
        
        /// <summary>
        /// Transaction ID
        /// </summary>
        public string TransactionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Staff name who requested
        /// </summary>
        public string StaffName { get; set; } = string.Empty;
        
        /// <summary>
        /// Table name if applicable
        /// </summary>
        public string? TableName { get; set; }
        
        /// <summary>
        /// When the request was created
        /// </summary>
        public DateTime RequestedAt { get; set; }
        
        /// <summary>
        /// Request status
        /// </summary>
        public string Status { get; set; } = string.Empty;
        
        /// <summary>
        /// Success message
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Pay entire bill request data for desktop polling
    /// </summary>
    public class PayEntireBillRequestDTO
    {
        public string RequestId { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public int StaffId { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public string? TableId { get; set; }
        public string? TableName { get; set; }
        public DateTime RequestedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }
    
    /// <summary>
    /// Request to complete a pay entire bill request
    /// </summary>
    public class CompletePayEntireBillRequest
    {
        /// <summary>
        /// Who completed the payment on desktop
        /// </summary>
        /// <example>Desktop User</example>
        public string? CompletedBy { get; set; }
    }
}

    // =============================================
    // PRINT BILL DTOs
    // =============================================
    
    /// <summary>
    /// Request to print bill for a waiting order
    /// </summary>
    public class PrintBillRequest
    {
        /// <summary>
        /// Transaction ID to print bill for
        /// </summary>
        /// <example>M20260219143022</example>
        [Required(ErrorMessage = "Transaction ID is required")]
        public string TransactionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Staff ID who is requesting the print
        /// </summary>
        /// <example>3</example>
        [Required(ErrorMessage = "Staff ID is required")]
        public int StaffId { get; set; }
        
        /// <summary>
        /// Optional notes for the print request
        /// </summary>
        /// <example>Customer ready to pay</example>
        [MaxLength(200, ErrorMessage = "Notes cannot exceed 200 characters")]
        public string? Notes { get; set; }
    }
    
    /// <summary>
    /// Response after creating print bill request
    /// </summary>
    public class PrintBillResponse
    {
        /// <summary>
        /// Unique request ID
        /// </summary>
        public string RequestId { get; set; } = string.Empty;
        
        /// <summary>
        /// Transaction ID
        /// </summary>
        public string TransactionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Staff name who requested
        /// </summary>
        public string StaffName { get; set; } = string.Empty;
        
        /// <summary>
        /// Table name (if any)
        /// </summary>
        public string? TableName { get; set; }
        
        /// <summary>
        /// When the request was created
        /// </summary>
        public DateTime RequestedAt { get; set; }
        
        /// <summary>
        /// Request status
        /// </summary>
        public string Status { get; set; } = "Pending";
        
        /// <summary>
        /// Success message
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Print bill request details for desktop polling
    /// </summary>
    public class PrintBillRequestDTO
    {
        /// <summary>
        /// Unique request ID
        /// </summary>
        public string RequestId { get; set; } = string.Empty;
        
        /// <summary>
        /// Transaction ID
        /// </summary>
        public string TransactionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Staff ID who requested
        /// </summary>
        public int StaffId { get; set; }
        
        /// <summary>
        /// Staff name who requested
        /// </summary>
        public string StaffName { get; set; } = string.Empty;
        
        /// <summary>
        /// Table ID (if any)
        /// </summary>
        public string? TableId { get; set; }
        
        /// <summary>
        /// Table name (if any)
        /// </summary>
        public string? TableName { get; set; }
        
        /// <summary>
        /// When the request was created
        /// </summary>
        public DateTime RequestedAt { get; set; }
        
        /// <summary>
        /// Request status
        /// </summary>
        public string Status { get; set; } = string.Empty;
        
        /// <summary>
        /// Optional notes
        /// </summary>
        public string? Notes { get; set; }
    }
    
    /// <summary>
    /// Request to complete a print bill request
    /// </summary>
    public class CompletePrintBillRequest
    {
        /// <summary>
        /// Who completed the request (desktop user)
        /// </summary>
        /// <example>Desktop User</example>
        public string? CompletedBy { get; set; }
    }

    // =============================================
    // REFIRE TO KITCHEN DTOs
    // =============================================
    
    /// <summary>
    /// Request to mark an order as refired
    /// </summary>
    public class RefireToKitchenRequest
    {
        /// <summary>
        /// Transaction ID to mark as refired
        /// </summary>
        /// <example>M20260221001</example>
        [Required(ErrorMessage = "Transaction ID is required")]
        public string TransactionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Staff ID who is marking the order as refired
        /// </summary>
        /// <example>5</example>
        [Required(ErrorMessage = "Staff ID is required")]
        public int StaffId { get; set; }
        
        /// <summary>
        /// Array of item indices to refire (empty array = refire all items)
        /// </summary>
        /// <example>[0, 2]</example>
        public List<int> ItemIndices { get; set; } = new List<int>();
        
        /// <summary>
        /// Reason for refiring the order
        /// </summary>
        /// <example>Customer complaint - food cold</example>
        [Required(ErrorMessage = "Reason is required")]
        [MaxLength(200, ErrorMessage = "Reason cannot exceed 200 characters")]
        public string Reason { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Response after marking order as refired
    /// </summary>
    public class RefireToKitchenResponse
    {
        /// <summary>
        /// Transaction ID
        /// </summary>
        public string TransactionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Number of items refired
        /// </summary>
        public int ItemsRefired { get; set; }
        
        /// <summary>
        /// When the order was marked as refired
        /// </summary>
        public DateTime RefiredAt { get; set; }
        
        /// <summary>
        /// Success message
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    // =============================================
    // COMBINE ORDERS DTOs
    // =============================================
    
    /// <summary>
    /// Request to combine multiple orders into one
    /// </summary>
    public class CombineOrdersRequest
    {
        /// <summary>
        /// List of transaction IDs to combine (minimum 2)
        /// </summary>
        /// <example>["M20260221001", "M20260221002", "M20260221003"]</example>
        [Required(ErrorMessage = "Transaction IDs are required")]
        [MinLength(2, ErrorMessage = "At least 2 transactions required")]
        public List<string> TransactionIds { get; set; } = new List<string>();
        
        /// <summary>
        /// Target table ID for combined order
        /// </summary>
        /// <example>TBL-001</example>
        [Required(ErrorMessage = "Target table ID is required")]
        public string TargetTableId { get; set; } = string.Empty;
        
        /// <summary>
        /// Target staff ID for combined order
        /// </summary>
        /// <example>5</example>
        [Required(ErrorMessage = "Target staff ID is required")]
        public int TargetStaffId { get; set; }
        
        /// <summary>
        /// Optional notes for combined order
        /// </summary>
        /// <example>Combined for birthday party</example>
        [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        public string? Notes { get; set; }
    }
    
    /// <summary>
    /// Response after combining orders
    /// </summary>
    public class CombineOrdersResponse
    {
        /// <summary>
        /// New transaction ID for combined order
        /// </summary>
        public string NewTransactionId { get; set; } = string.Empty;
        
        /// <summary>
        /// List of original transaction IDs that were combined
        /// </summary>
        public List<string> CombinedFromIds { get; set; } = new List<string>();
        
        /// <summary>
        /// Total number of items in combined order
        /// </summary>
        public int TotalItems { get; set; }
        
        /// <summary>
        /// Total amount of combined order
        /// </summary>
        public decimal TotalAmount { get; set; }
        
        /// <summary>
        /// Number of items that were merged (duplicate items combined)
        /// </summary>
        public int MergedItemsCount { get; set; }
        
        /// <summary>
        /// When the orders were combined
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Success message
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    // =============================================
    // REMOVE TAXES AND FEES DTOs
    // =============================================
    
    /// <summary>
    /// Request to remove taxes and fees from an order
    /// </summary>
    public class RemoveTaxesAndFeesRequest
    {
        /// <summary>
        /// Transaction ID to remove taxes and fees from
        /// </summary>
        /// <example>M20260221001</example>
        [Required(ErrorMessage = "Transaction ID is required")]
        public string TransactionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Staff ID who is removing the taxes and fees
        /// </summary>
        /// <example>5</example>
        [Required(ErrorMessage = "Staff ID is required")]
        public int StaffId { get; set; }
        
        /// <summary>
        /// Reason for removing taxes and fees
        /// </summary>
        /// <example>Tax-exempt organization</example>
        [Required(ErrorMessage = "Reason is required")]
        [MaxLength(200, ErrorMessage = "Reason cannot exceed 200 characters")]
        public string Reason { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Response after removing taxes and fees
    /// </summary>
    public class RemoveTaxesAndFeesResponse
    {
        /// <summary>
        /// Transaction ID
        /// </summary>
        public string TransactionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Original tax amount before removal
        /// </summary>
        public decimal OriginalTaxAmount { get; set; }
        
        /// <summary>
        /// Amount of tax removed
        /// </summary>
        public decimal TaxRemoved { get; set; }
        
        /// <summary>
        /// Number of items affected
        /// </summary>
        public int ItemsAffected { get; set; }
        
        /// <summary>
        /// Staff name who performed the removal
        /// </summary>
        public string RemovedBy { get; set; } = string.Empty;
        
        /// <summary>
        /// Audit log ID for tracking
        /// </summary>
        public string AuditLogId { get; set; } = string.Empty;
        
        /// <summary>
        /// When the removal was performed
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Success message
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    // =============================================
    // PRINT BILL BY TABLE DTOs
    // =============================================

    /// <summary>
    /// Request to print bills for ALL open orders on a specific table at once
    /// </summary>
    public class PrintBillByTableRequest
    {
        /// <summary>
        /// Table ID whose orders should all be printed
        /// </summary>
        /// <example>TBL-004</example>
        [Required(ErrorMessage = "Table ID is required")]
        public string TableId { get; set; } = string.Empty;

        /// <summary>
        /// Staff ID requesting the prints
        /// </summary>
        /// <example>3</example>
        [Required(ErrorMessage = "Staff ID is required")]
        public int StaffId { get; set; }

        /// <summary>
        /// Optional notes for all requests
        /// </summary>
        [MaxLength(200, ErrorMessage = "Notes cannot exceed 200 characters")]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Response after creating print-bill requests for all orders on a table
    /// </summary>
    public class PrintBillByTableResponse
    {
        /// <summary>Number of print bill requests created</summary>
        public int RequestCount { get; set; }

        /// <summary>Table ID</summary>
        public string TableId { get; set; } = string.Empty;

        /// <summary>Table name</summary>
        public string? TableName { get; set; }

        /// <summary>List of created request IDs</summary>
        public List<string> RequestIds { get; set; } = new();

        /// <summary>Success message</summary>
        public string Message { get; set; } = string.Empty;
    }

