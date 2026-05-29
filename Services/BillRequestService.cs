using Dapper;
using Microsoft.Data.Sqlite;
using PottaAPI.Models;
using PottaAPI.Services.Interfaces;

namespace PottaAPI.Services;

public class BillRequestService : IBillRequestService
{
    private readonly string _connectionString;
    private readonly IOrderService _orderService;
    private readonly IStaffService _staffService;
    private readonly ITableService _tableService;

    public BillRequestService(
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

    // ── Print Bill ──

    public async Task<PrintBillResponse> CreatePrintBillAsync(PrintBillRequest request)
    {
        var transaction = await _orderService.GetWaitingTransactionByIdAsync(request.TransactionId)
            ?? throw new KeyNotFoundException($"Transaction {request.TransactionId} not found");

        var staff = await _staffService.GetStaffByIdAsync(request.StaffId)
            ?? throw new KeyNotFoundException($"Staff {request.StaffId} not found");

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var conflictCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM PayEntireBillRequests WHERE transactionId = @tid AND status = 'Pending'",
            new { tid = request.TransactionId });

        if (conflictCount > 0)
            throw new InvalidOperationException("A payment request is already pending for this transaction.");

        var existing = await connection.QueryFirstOrDefaultAsync<PrintBillRequestDTO>(@"
            SELECT * FROM PrintBillRequests WHERE transactionId = @tid AND status = 'Pending' LIMIT 1",
            new { tid = request.TransactionId });

        if (existing != null)
            return new PrintBillResponse
            {
                RequestId = existing.RequestId,
                TransactionId = existing.TransactionId,
                StaffName = existing.StaffName,
                TableName = existing.TableName,
                RequestedAt = existing.RequestedAt,
                Status = existing.Status,
                Message = "Existing pending print bill request returned"
            };

        var requestId = $"PBR-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
        var staffName = $"{staff.FirstName} {staff.LastName}";

        await connection.ExecuteAsync(@"
            INSERT INTO PrintBillRequests 
            (requestId, transactionId, staffId, staffName, tableId, tableName, requestedAt, status, notes)
            VALUES (@requestId, @transactionId, @staffId, @staffName, @tableId, @tableName, @requestedAt, @status, @notes)",
            new
            {
                requestId,
                transactionId = request.TransactionId,
                staffId = request.StaffId,
                staffName,
                tableId = (object?)transaction.TableId ?? DBNull.Value,
                tableName = (object?)transaction.TableName ?? DBNull.Value,
                requestedAt = DateTime.Now,
                status = "Pending",
                notes = (object?)request.Notes ?? DBNull.Value
            });

        return new PrintBillResponse
        {
            RequestId = requestId,
            TransactionId = request.TransactionId,
            StaffName = staffName,
            TableName = transaction.TableName,
            RequestedAt = DateTime.Now,
            Status = "Pending",
            Message = "Print bill request created successfully"
        };
    }

    public async Task<PrintBillByTableResponse> CreatePrintBillByTableAsync(PrintBillByTableRequest request)
    {
        var staff = await _staffService.GetStaffByIdAsync(request.StaffId)
            ?? throw new KeyNotFoundException($"Staff {request.StaffId} not found");

        var table = await _tableService.GetTableByIdAsync(request.TableId)
            ?? throw new KeyNotFoundException($"Table {request.TableId} not found");

        var allTransactions = await _orderService.GetWaitingTransactionsAsync();
        var tableTransactions = allTransactions.Where(t => t.TableId == request.TableId).ToList();

        if (tableTransactions.Count == 0)
            throw new InvalidOperationException($"No open orders found for table {table.TableName}");

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var createdIds = new List<string>();
        var staffName = $"{staff.FirstName} {staff.LastName}";

        foreach (var txn in tableTransactions)
        {
            var payPending = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM PayEntireBillRequests WHERE transactionId = @tid AND status = 'Pending'",
                new { tid = txn.TransactionId });
            if (payPending > 0) continue;

            var existingId = await connection.QueryFirstOrDefaultAsync<string>(@"
                SELECT requestId FROM PrintBillRequests WHERE transactionId = @tid AND status = 'Pending' LIMIT 1",
                new { tid = txn.TransactionId });

            if (existingId != null)
            {
                createdIds.Add(existingId);
                continue;
            }

            var requestId = $"PBR-{DateTime.Now:yyyyMMddHHmmssff}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
            await connection.ExecuteAsync(@"
                INSERT INTO PrintBillRequests 
                (requestId, transactionId, staffId, staffName, tableId, tableName, requestedAt, status, notes)
                VALUES (@requestId, @transactionId, @staffId, @staffName, @tableId, @tableName, @requestedAt, @status, @notes)",
                new
                {
                    requestId,
                    transactionId = txn.TransactionId,
                    staffId = request.StaffId,
                    staffName,
                    tableId = (object?)txn.TableId ?? DBNull.Value,
                    tableName = (object?)txn.TableName ?? DBNull.Value,
                    requestedAt = DateTime.Now,
                    status = "Pending",
                    notes = (object?)request.Notes ?? DBNull.Value
                });

            createdIds.Add(requestId);
        }

        return new PrintBillByTableResponse
        {
            RequestCount = createdIds.Count,
            TableId = request.TableId,
            TableName = table.TableName,
            RequestIds = createdIds,
            Message = createdIds.Count == 0
                ? "No new print requests created"
                : $"Created {createdIds.Count} print bill request(s)"
        };
    }

    public async Task<List<PrintBillRequestDTO>> GetPendingPrintBillsAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var sql = "SELECT * FROM PrintBillRequests WHERE status = 'Pending' ORDER BY requestedAt ASC";
        return (await connection.QueryAsync<PrintBillRequestDTO>(sql)).ToList();
    }

    public async Task<bool> CompletePrintBillAsync(string requestId, string? completedBy)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var sql = @"
            UPDATE PrintBillRequests
            SET status = 'Completed', completedAt = @completedAt, completedBy = @completedBy
            WHERE requestId = @requestId AND status = 'Pending'";
        return await connection.ExecuteAsync(sql, new { completedAt = DateTime.Now, completedBy, requestId }) > 0;
    }

    public async Task<bool> CancelPrintBillAsync(string requestId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        return await connection.ExecuteAsync(
            "UPDATE PrintBillRequests SET status = 'Cancelled' WHERE requestId = @requestId AND status = 'Pending'",
            new { requestId }) > 0;
    }

    // ── Pay Entire Bill ──

    public async Task<PayEntireBillResponse> CreatePayEntireBillAsync(PayEntireBillRequest request)
    {
        var transaction = await _orderService.GetWaitingTransactionByIdAsync(request.TransactionId)
            ?? throw new KeyNotFoundException($"Transaction {request.TransactionId} not found");

        var staff = await _staffService.GetStaffByIdAsync(request.StaffId)
            ?? throw new KeyNotFoundException($"Staff {request.StaffId} not found");

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Check for duplicate pending request
        var existing = await connection.QueryFirstOrDefaultAsync<PayEntireBillRequestDTO>(@"
            SELECT * FROM PayEntireBillRequests 
            WHERE transactionId = @tid AND status = 'Pending' 
            LIMIT 1",
            new { tid = request.TransactionId });

        if (existing != null)
            return new PayEntireBillResponse
            {
                RequestId = existing.RequestId,
                TransactionId = existing.TransactionId,
                StaffName = existing.StaffName,
                TableName = existing.TableName,
                RequestedAt = existing.RequestedAt,
                Status = existing.Status,
                Message = "Existing pending pay entire bill request returned"
            };

        var requestId = $"PEBR-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
        var staffName = $"{staff.FirstName} {staff.LastName}";

        await connection.ExecuteAsync(@"
            INSERT INTO PayEntireBillRequests 
            (requestId, transactionId, staffId, staffName, tableId, tableName, requestedAt, status, notes)
            VALUES (@requestId, @transactionId, @staffId, @staffName, @tableId, @tableName, @requestedAt, @status, @notes)",
            new
            {
                requestId,
                transactionId = request.TransactionId,
                staffId = request.StaffId,
                staffName,
                tableId = (object?)transaction.TableId ?? DBNull.Value,
                tableName = (object?)transaction.TableName ?? DBNull.Value,
                requestedAt = DateTime.Now,
                status = "Pending",
                notes = (object?)request.Notes ?? DBNull.Value
            });

        return new PayEntireBillResponse
        {
            RequestId = requestId,
            TransactionId = request.TransactionId,
            StaffName = staffName,
            TableName = transaction.TableName,
            RequestedAt = DateTime.Now,
            Status = "Pending",
            Message = "Pay entire bill request created successfully"
        };
    }

    public async Task<List<PayEntireBillRequestDTO>> GetPendingPayBillsAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        return (await connection.QueryAsync<PayEntireBillRequestDTO>(
            "SELECT * FROM PayEntireBillRequests WHERE status = 'Pending' ORDER BY requestedAt ASC")).ToList();
    }

    public async Task<bool> CompletePayBillAsync(string requestId, string? completedBy)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var sql = @"
            UPDATE PayEntireBillRequests
            SET status = 'Completed', completedAt = @completedAt, completedBy = @completedBy
            WHERE requestId = @requestId AND status = 'Pending'";
        return await connection.ExecuteAsync(sql, new { completedAt = DateTime.Now, completedBy, requestId }) > 0;
    }

    public async Task<bool> CancelPayBillAsync(string requestId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        return await connection.ExecuteAsync(
            "UPDATE PayEntireBillRequests SET status = 'Cancelled' WHERE requestId = @requestId AND status = 'Pending'",
            new { requestId }) > 0;
    }
}