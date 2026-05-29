using Dapper;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using PottaAPI.Services.Interfaces;

namespace PottaAPI.Services;

public class OrderTaxService : IOrderTaxService
{
    private readonly string _connectionString;
    private readonly IOrderService _orderService;
    private readonly IStaffService _staffService;

    public OrderTaxService(
        IConnectionStringProvider connectionStringProvider,
        IOrderService orderService,
        IStaffService staffService)
    {
        _connectionString = connectionStringProvider.GetConnectionString();
        _orderService = orderService;
        _staffService = staffService;
    }

    public async Task<RemoveTaxesAndFeesResponse> RemoveTaxesAndFeesAsync(RemoveTaxesAndFeesRequest request)
    {
        var transaction = await _orderService.GetWaitingTransactionByIdAsync(request.TransactionId)
            ?? throw new KeyNotFoundException($"Transaction {request.TransactionId} not found");

        var staff = await _staffService.GetStaffByIdAsync(request.StaffId)
            ?? throw new KeyNotFoundException($"Staff {request.StaffId} not found");

        var cartItems = transaction.Items;
        if (cartItems == null || cartItems.Count == 0)
            throw new InvalidOperationException("Transaction has no items");

        var originalTaxAmount = cartItems.Sum(i => i.TaxAmount);
        var itemsAffected = 0;

        foreach (var item in cartItems)
        {
            if (item.TaxAmount > 0)
            {
                item.TaxAmount = 0;
                item.Taxable = false;
                itemsAffected++;
            }
        }

        var updatedJson = JsonConvert.SerializeObject(cartItems);
        var auditLogId = $"AUDIT-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
        var staffName = $"{staff.FirstName} {staff.LastName}";

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var dbTransaction = connection.BeginTransaction();

        try
        {
            await connection.ExecuteAsync(@"
                UPDATE WaitingTransactions 
                SET CartItems = @cartItems, ModifiedDate = @modifiedDate
                WHERE TransactionId = @transactionId",
                new { cartItems = updatedJson, modifiedDate = DateTime.Now, transactionId = request.TransactionId },
                dbTransaction);

            await connection.ExecuteAsync(@"
                INSERT INTO TaxAdjustmentAuditLog 
                (auditId, transactionId, staffId, staffName, action, applyTo, 
                 originalTaxAmount, newTaxAmount, reason, timestamp)
                VALUES 
                (@auditId, @transactionId, @staffId, @staffName, @action, @applyTo,
                 @originalTaxAmount, @newTaxAmount, @reason, @timestamp)",
                new
                {
                    auditId = auditLogId,
                    transactionId = request.TransactionId,
                    staffId = request.StaffId,
                    staffName,
                    action = "Remove",
                    applyTo = "Order",
                    originalTaxAmount,
                    newTaxAmount = 0,
                    reason = request.Reason,
                    timestamp = DateTime.Now
                }, dbTransaction);

            dbTransaction.Commit();
        }
        catch
        {
            dbTransaction.Rollback();
            throw;
        }

        return new RemoveTaxesAndFeesResponse
        {
            TransactionId = request.TransactionId,
            OriginalTaxAmount = originalTaxAmount,
            TaxRemoved = originalTaxAmount,
            ItemsAffected = itemsAffected,
            RemovedBy = staffName,
            AuditLogId = auditLogId,
            Timestamp = DateTime.Now,
            Message = $"Taxes and fees removed successfully. {itemsAffected} item(s) affected."
        };
    }
}