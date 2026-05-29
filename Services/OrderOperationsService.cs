using Dapper;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using PottaAPI.Models;
using PottaAPI.Services.Interfaces;

namespace PottaAPI.Services;

public class OrderOperationsService : IOrderOperationsService
{
    private readonly string _connectionString;
    private readonly IOrderService _orderService;
    private readonly IStaffService _staffService;
    private readonly ITableService _tableService;

    public OrderOperationsService(
        IConnectionStringProvider connectionStringProvider,
        IOrderService orderService,
        IStaffService staffService,
        ITableService tableService)
    {
        _connectionString = connectionStringProvider.GetConnectionString();
        _orderService = orderService;
        _staffService = staffService;
        _tableService = tableService;
    }

    public async Task<AddNotesResponse> AddNotesAsync(AddNotesRequest request)
    {
        var transaction = await _orderService.GetWaitingTransactionByIdAsync(request.TransactionId)
            ?? throw new KeyNotFoundException($"Transaction {request.TransactionId} not found");

        string notePrefix = "";
        if (request.AddedByStaffId.HasValue && request.AddedByStaffId.Value > 0)
        {
            var staff = await _staffService.GetStaffByIdAsync(request.AddedByStaffId.Value);
            if (staff != null)
                notePrefix = $"[{staff.FirstName} {staff.LastName} - {DateTime.Now:HH:mm}] ";
        }

        string updatedNotes = string.IsNullOrEmpty(transaction.Notes)
            ? $"{notePrefix}{request.NoteText}"
            : $"{transaction.Notes}\n{notePrefix}{request.NoteText}";

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(@"
            UPDATE WaitingTransactions 
            SET Notes = @notes, ModifiedDate = @modifiedDate
            WHERE TransactionId = @transactionId",
            new { notes = updatedNotes, modifiedDate = DateTime.Now, transactionId = request.TransactionId });

        return new AddNotesResponse
        {
            TransactionId = request.TransactionId,
            NoteText = request.NoteText,
            AddedAt = DateTime.Now,
            Message = "Note added successfully"
        };
    }

    public async Task<TransferServerResponse> TransferServerAsync(TransferServerRequest request)
    {
        var transaction = await _orderService.GetWaitingTransactionByIdAsync(request.TransactionId)
            ?? throw new KeyNotFoundException($"Transaction {request.TransactionId} not found");

        var newStaff = await _staffService.GetStaffByIdAsync(request.NewStaffId)
            ?? throw new KeyNotFoundException($"Staff {request.NewStaffId} not found");

        if (!newStaff.IsActive)
            throw new InvalidOperationException($"Staff {newStaff.FirstName} {newStaff.LastName} is not active");

        StaffDTO? previousStaff = null;
        if (transaction.StaffId.HasValue)
            previousStaff = await _staffService.GetStaffByIdAsync(transaction.StaffId.Value);

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(@"
            UPDATE WaitingTransactions 
            SET StaffId = @newStaffId, ModifiedDate = @modifiedDate
            WHERE TransactionId = @transactionId",
            new { newStaffId = request.NewStaffId, modifiedDate = DateTime.Now, transactionId = request.TransactionId });

        if (transaction.Items?.Count > 0)
        {
            foreach (var item in transaction.Items)
                item.StaffId = request.NewStaffId;

            var json = JsonConvert.SerializeObject(transaction.Items);
            await connection.ExecuteAsync(@"
                UPDATE WaitingTransactions SET CartItems = @cartItems WHERE TransactionId = @transactionId",
                new { cartItems = json, transactionId = request.TransactionId });
        }

        return new TransferServerResponse
        {
            TransactionId = request.TransactionId,
            PreviousStaffId = transaction.StaffId,
            PreviousStaffName = previousStaff != null ? $"{previousStaff.FirstName} {previousStaff.LastName}" : "None",
            NewStaffId = request.NewStaffId,
            NewStaffName = $"{newStaff.FirstName} {newStaff.LastName}",
            TransferredAt = DateTime.Now,
            Message = $"Order transferred to {newStaff.FirstName} {newStaff.LastName}"
        };
    }

    public async Task<ShiftHandoverResponse> ShiftHandoverAsync(ShiftHandoverRequest request)
    {
        var currentStaff = await _staffService.GetStaffByIdAsync(request.CurrentStaffId)
            ?? throw new KeyNotFoundException($"Current staff {request.CurrentStaffId} not found");

        var newStaff = await _staffService.GetStaffByIdAsync(request.NewStaffId)
            ?? throw new KeyNotFoundException($"New staff {request.NewStaffId} not found");

        if (!newStaff.IsActive)
            throw new InvalidOperationException($"New staff {newStaff.FirstName} {newStaff.LastName} is not active");

        var allOrders = await _orderService.GetWaitingTransactionsAsync();
        var staffOrders = allOrders.Where(o => o.StaffId == request.CurrentStaffId).ToList();

        if (staffOrders.Count == 0)
            return new ShiftHandoverResponse
            {
                CurrentStaffId = request.CurrentStaffId,
                CurrentStaffName = $"{currentStaff.FirstName} {currentStaff.LastName}",
                NewStaffId = request.NewStaffId,
                NewStaffName = $"{newStaff.FirstName} {newStaff.LastName}",
                OrdersTransferred = 0,
                Message = "No orders to transfer"
            };

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        foreach (var order in staffOrders)
        {
            await connection.ExecuteAsync(@"
                UPDATE WaitingTransactions SET StaffId = @newStaffId, ModifiedDate = @modifiedDate
                WHERE TransactionId = @transactionId",
                new { newStaffId = request.NewStaffId, modifiedDate = DateTime.Now, transactionId = order.TransactionId });

            if (order.Items?.Count > 0)
            {
                foreach (var item in order.Items) item.StaffId = request.NewStaffId;
                var json = JsonConvert.SerializeObject(order.Items);
                await connection.ExecuteAsync(@"
                    UPDATE WaitingTransactions SET CartItems = @cartItems WHERE TransactionId = @transactionId",
                    new { cartItems = json, transactionId = order.TransactionId });
            }
        }

        return new ShiftHandoverResponse
        {
            CurrentStaffId = request.CurrentStaffId,
            CurrentStaffName = $"{currentStaff.FirstName} {currentStaff.LastName}",
            NewStaffId = request.NewStaffId,
            NewStaffName = $"{newStaff.FirstName} {newStaff.LastName}",
            OrdersTransferred = staffOrders.Count,
            TransferredTransactionIds = staffOrders.Select(o => o.TransactionId).ToList(),
            HandoverAt = DateTime.Now,
            Message = $"Transferred {staffOrders.Count} order(s)"
        };
    }

    public async Task<MoveOrderResponse> MoveOrderAsync(MoveOrderRequest request)
    {
        var transaction = await _orderService.GetWaitingTransactionByIdAsync(request.TransactionId)
            ?? throw new KeyNotFoundException($"Transaction {request.TransactionId} not found");

        var sourceTableId = transaction.TableId;
        var sourceTable = !string.IsNullOrEmpty(sourceTableId)
            ? await _tableService.GetTableByIdAsync(sourceTableId) : null;

        var targetTable = await _tableService.GetTableByIdAsync(request.TargetTableId)
            ?? throw new KeyNotFoundException($"Target table {request.TargetTableId} not found");

        if (targetTable.Status == "Occupied")
            throw new InvalidOperationException($"Target table {targetTable.TableName} is already occupied");

        if (await _tableService.AnySeatsOccupiedAsync(request.TargetTableId))
            throw new InvalidOperationException($"Target table {targetTable.TableName} has occupied seats");

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(@"
            UPDATE WaitingTransactions 
            SET TableId = @targetTableId, TableNumber = @targetTableNumber, 
                TableName = @targetTableName, ModifiedDate = @modifiedDate
            WHERE TransactionId = @transactionId",
            new
            {
                targetTableId = targetTable.TableId,
                targetTableNumber = targetTable.TableNumber,
                targetTableName = targetTable.TableName ?? "",
                modifiedDate = DateTime.Now,
                transactionId = request.TransactionId
            });

        if (sourceTable != null)
            await _tableService.UpdateTableStatusAsync(sourceTable.TableId, new UpdateTableStatusDTO
            { Status = "Available", CustomerId = null, TransactionId = null });

        await _tableService.UpdateTableStatusAsync(targetTable.TableId, new UpdateTableStatusDTO
        { Status = "Occupied", CustomerId = transaction.CustomerId, TransactionId = request.TransactionId });

        return new MoveOrderResponse
        {
            TransactionId = request.TransactionId,
            FromTableId = sourceTableId,
            FromTableName = sourceTable?.TableName ?? "Unknown",
            ToTableId = targetTable.TableId,
            ToTableName = targetTable.TableName ?? "",
            MovedAt = DateTime.Now,
            Message = $"Order moved to {targetTable.TableName}"
        };
    }

    public async Task<RefireToKitchenResponse> RefireToKitchenAsync(RefireToKitchenRequest request)
    {
        var transaction = await _orderService.GetWaitingTransactionByIdAsync(request.TransactionId)
            ?? throw new KeyNotFoundException($"Transaction {request.TransactionId} not found");

        var staff = await _staffService.GetStaffByIdAsync(request.StaffId)
            ?? throw new KeyNotFoundException($"Staff {request.StaffId} not found");

        var cartItems = transaction.Items;
        if (cartItems == null || cartItems.Count == 0)
            throw new InvalidOperationException("Transaction has no items");

        List<WaitingTransactionItemDto> itemsToRefire;
        if (request.ItemIndices == null || request.ItemIndices.Count == 0)
        {
            itemsToRefire = cartItems;
        }
        else
        {
            itemsToRefire = new List<WaitingTransactionItemDto>();
            foreach (var index in request.ItemIndices)
            {
                if (index < 0 || index >= cartItems.Count)
                    throw new ArgumentException($"Invalid item index: {index}");
                itemsToRefire.Add(cartItems[index]);
            }
        }

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(@"
            UPDATE WaitingTransactions 
            SET IsRefired = 1, RefireReason = @refireReason, RefiredAt = @refiredAt,
                RefiredByStaffId = @refiredByStaffId, RefiredByStaffName = @refiredByStaffName,
                ModifiedDate = @modifiedDate
            WHERE TransactionId = @transactionId",
            new
            {
                refireReason = request.Reason,
                refiredAt = DateTime.Now,
                refiredByStaffId = request.StaffId,
                refiredByStaffName = $"{staff.FirstName} {staff.LastName}",
                modifiedDate = DateTime.Now,
                transactionId = request.TransactionId
            });

        return new RefireToKitchenResponse
        {
            TransactionId = request.TransactionId,
            ItemsRefired = itemsToRefire.Count,
            RefiredAt = DateTime.Now,
            Message = $"Order marked as refired. {itemsToRefire.Count} item(s) will be reprinted."
        };
    }

    public async Task<CombineOrdersResponse> CombineOrdersAsync(CombineOrdersRequest request)
    {
        var uniqueIds = request.TransactionIds.Distinct().ToList();
        if (uniqueIds.Count < 2)
            throw new ArgumentException("At least 2 unique transactions required");

        var transactions = new List<WaitingTransactionDto>();
        foreach (var id in uniqueIds)
        {
            var t = await _orderService.GetWaitingTransactionByIdAsync(id)
                ?? throw new KeyNotFoundException($"Transaction {id} not found");
            transactions.Add(t);
        }

        var targetTable = await _tableService.GetTableByIdAsync(request.TargetTableId)
            ?? throw new KeyNotFoundException($"Target table {request.TargetTableId} not found");

        var targetStaff = await _staffService.GetStaffByIdAsync(request.TargetStaffId)
            ?? throw new KeyNotFoundException($"Target staff {request.TargetStaffId} not found");

        if (!targetStaff.IsActive)
            throw new InvalidOperationException($"Target staff {targetStaff.FirstName} {targetStaff.LastName} is not active");

        var allCartItems = transactions
            .Where(t => t.Items != null)
            .SelectMany(t => t.Items!)
            .ToList();

        var mergedItems = MergeCartItems(allCartItems);
        var totalAmount = mergedItems.Sum(i => (i.Price * i.Quantity) + i.TaxAmount);
        var newTransactionId = $"M{DateTime.Now:yyyyMMddHHmmss}";
        var cartItemsJson = JsonConvert.SerializeObject(mergedItems);

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var dbTransaction = connection.BeginTransaction();

        try
        {
            await connection.ExecuteAsync(@"
                INSERT INTO WaitingTransactions 
                (TransactionId, CartItems, CustomerId, TableId, TableNumber, TableName, 
                 Status, StaffId, Notes, CreatedDate, ModifiedDate)
                VALUES 
                (@transactionId, @cartItems, @customerId, @tableId, @tableNumber, @tableName,
                 @status, @staffId, @notes, @createdDate, @modifiedDate)",
                new
                {
                    transactionId = newTransactionId,
                    cartItems = cartItemsJson,
                    customerId = (object?)transactions[0].CustomerId ?? DBNull.Value,
                    tableId = request.TargetTableId,
                    tableNumber = targetTable.TableNumber,
                    tableName = targetTable.TableName ?? "",
                    status = "Pending",
                    staffId = request.TargetStaffId,
                    notes = (object?)request.Notes ?? DBNull.Value,
                    createdDate = DateTime.Now,
                    modifiedDate = DateTime.Now
                }, dbTransaction);

            foreach (var id in uniqueIds)
            {
                await connection.ExecuteAsync(
                    "DELETE FROM PrintBillRequests WHERE transactionId = @id",
                    new { id }, dbTransaction);
                await connection.ExecuteAsync(
                    "DELETE FROM PayEntireBillRequests WHERE transactionId = @id",
                    new { id }, dbTransaction);
                await connection.ExecuteAsync(
                    "DELETE FROM TaxAdjustmentAuditLog WHERE transactionId = @id",
                    new { id }, dbTransaction);
                await connection.ExecuteAsync(
                    "DELETE FROM WaitingTransactions WHERE TransactionId = @id",
                    new { id }, dbTransaction);
            }

            await connection.ExecuteAsync(@"
                UPDATE Tables SET status = 'Occupied', currentTransactionId = @transactionId
                WHERE tableId = @tableId",
                new { transactionId = newTransactionId, tableId = request.TargetTableId }, dbTransaction);

            dbTransaction.Commit();
        }
        catch
        {
            dbTransaction.Rollback();
            throw;
        }

        return new CombineOrdersResponse
        {
            NewTransactionId = newTransactionId,
            CombinedFromIds = uniqueIds,
            TotalItems = mergedItems.Count,
            TotalAmount = totalAmount,
            MergedItemsCount = allCartItems.Count - mergedItems.Count,
            Timestamp = DateTime.Now,
            Message = $"Combined {uniqueIds.Count} orders into 1"
        };
    }

    private List<WaitingTransactionItemDto> MergeCartItems(List<WaitingTransactionItemDto> items)
    {
        var merged = new List<WaitingTransactionItemDto>();
        foreach (var item in items)
        {
            var existing = merged.FirstOrDefault(m => CanMergeItems(m, item));
            if (existing != null)
                existing.Quantity += item.Quantity;
            else
                merged.Add(new WaitingTransactionItemDto
                {
                    ProductId = item.ProductId,
                    Name = item.Name,
                    Quantity = item.Quantity,
                    Price = item.Price,
                    AppliedModifiers = item.AppliedModifiers,
                    TaxId = item.TaxId,
                    Taxable = item.Taxable,
                    TaxAmount = item.TaxAmount,
                    StaffId = item.StaffId
                });
        }
        return merged;
    }

    private bool CanMergeItems(WaitingTransactionItemDto a, WaitingTransactionItemDto b)
    {
        if (a.ProductId != b.ProductId || a.Name != b.Name || a.Price != b.Price ||
            a.TaxId != b.TaxId || a.Taxable != b.Taxable)
            return false;

        return JsonConvert.SerializeObject(a.AppliedModifiers) == JsonConvert.SerializeObject(b.AppliedModifiers);
    }
}