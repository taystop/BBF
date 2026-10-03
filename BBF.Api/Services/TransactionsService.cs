using System.Globalization;
using BBF.Api.Auth;
using BBF.Data.Entities;
using BBF.Shared.Contracts.Transactions;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

public class TransactionsService(ITenantDbContextFactory tenantDb)
{
    public async Task<List<TransactionDto>> GetTransactionsAsync(int groupId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        return await db.Transactions
            .Include(t => t.Category)
            .Include(t => t.Account)
            .OrderByDescending(t => t.Date)
            .Select(t => new TransactionDto(
                t.Id, t.Amount, t.Date, t.Description, t.MerchantName, t.Source,
                t.CategoryId, t.Category != null ? t.Category.Name : null, t.Category != null ? t.Category.Color : null,
                t.AccountId, t.Account != null ? t.Account.Name : null, t.RecurringTransactionId))
            .ToListAsync(ct);
    }

    public async Task<TransactionDto> CreateAsync(int groupId, CreateTransactionRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var transaction = new Transaction
        {
            Amount = request.Amount,
            Date = request.Date,
            Description = request.Description,
            MerchantName = request.MerchantName,
            CategoryId = request.CategoryId,
            AccountId = request.AccountId,
            GroupId = groupId,
            Source = "Manual",
            CreatedAt = DateTime.UtcNow,
        };
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync(ct);

        if (transaction.AccountId is int accountId)
        {
            await AccountsService.RecalculateBalanceAsync(accountId, db);
            await db.SaveChangesAsync(ct);
        }

        await db.Entry(transaction).Reference(t => t.Category).LoadAsync(ct);
        await db.Entry(transaction).Reference(t => t.Account).LoadAsync(ct);
        return ToDto(transaction);
    }

    public async Task UpdateAsync(int groupId, int transactionId, UpdateTransactionRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var entity = await db.Transactions.FirstOrDefaultAsync(t => t.Id == transactionId, ct);
        if (entity is null) return;

        var previousAccountId = entity.AccountId;

        entity.Amount = request.Amount;
        entity.Date = request.Date;
        entity.Description = request.Description;
        entity.MerchantName = request.MerchantName;
        entity.CategoryId = request.CategoryId;
        entity.AccountId = request.AccountId;
        await db.SaveChangesAsync(ct);

        if (previousAccountId is int oldAccountId)
            await AccountsService.RecalculateBalanceAsync(oldAccountId, db);
        if (request.AccountId is int newAccountId && newAccountId != previousAccountId)
            await AccountsService.RecalculateBalanceAsync(newAccountId, db);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int groupId, int transactionId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var entity = await db.Transactions.FirstOrDefaultAsync(t => t.Id == transactionId, ct);
        if (entity is null) return;

        var accountId = entity.AccountId;
        db.Transactions.Remove(entity);
        await db.SaveChangesAsync(ct);

        if (accountId is int id)
        {
            await AccountsService.RecalculateBalanceAsync(id, db);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<CsvImportResult> ImportCsvAsync(int groupId, int? accountId, Stream csvContent, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        using var reader = new StreamReader(csvContent);
        var headerLine = await reader.ReadLineAsync(ct);
        if (headerLine is null)
            return new CsvImportResult(0, 0);

        var headers = headerLine.Split(',')
            .Select(h => h.Trim().Trim('"').ToLowerInvariant())
            .ToList();

        var dateIdx = headers.FindIndex(h => h.Contains("date") || h.Contains("posted"));
        var descIdx = headers.FindIndex(h => h.Contains("description") || h.Contains("memo") || h.Contains("payee"));
        var amountIdx = headers.FindIndex(h => h.Contains("amount"));
        var debitIdx = headers.FindIndex(h => h.Contains("debit"));
        var creditIdx = headers.FindIndex(h => h.Contains("credit"));

        if (dateIdx < 0 || (amountIdx < 0 && debitIdx < 0))
            throw new InvalidOperationException(
                "Could not detect Date and Amount columns in CSV. Expected headers like 'Date', 'Description', 'Amount' or 'Debit'/'Credit'.");

        if (descIdx < 0) descIdx = dateIdx + 1;

        var imported = 0;
        var skipped = 0;
        string? line;

        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = ParseCsvLine(line);
            if (cols.Count <= Math.Max(dateIdx, Math.Max(descIdx, amountIdx >= 0 ? amountIdx : debitIdx)))
            {
                skipped++;
                continue;
            }

            if (!DateTime.TryParse(cols[dateIdx], out var date))
            {
                skipped++;
                continue;
            }

            decimal amount;
            if (amountIdx >= 0)
            {
                if (!decimal.TryParse(cols[amountIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out amount))
                {
                    skipped++;
                    continue;
                }
            }
            else
            {
                decimal.TryParse(debitIdx >= 0 && debitIdx < cols.Count ? cols[debitIdx] : "",
                    NumberStyles.Any, CultureInfo.InvariantCulture, out var debit);
                decimal.TryParse(creditIdx >= 0 && creditIdx < cols.Count ? cols[creditIdx] : "",
                    NumberStyles.Any, CultureInfo.InvariantCulture, out var credit);
                amount = debit > 0 ? debit : -credit;
            }

            var desc = descIdx < cols.Count ? cols[descIdx] : "Unknown";

            db.Transactions.Add(new Transaction
            {
                Amount = amount,
                Date = date,
                Description = desc,
                GroupId = groupId,
                AccountId = accountId,
                Source = "CSV",
                CreatedAt = DateTime.UtcNow
            });
            imported++;
        }

        if (imported > 0)
            await db.SaveChangesAsync(ct);

        if (accountId is int id)
        {
            await AccountsService.RecalculateBalanceAsync(id, db);
            await db.SaveChangesAsync(ct);
        }

        return new CsvImportResult(imported, skipped);
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var inQuotes = false;
        var field = new System.Text.StringBuilder();

        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(field.ToString().Trim().Trim('"'));
                field.Clear();
            }
            else
            {
                field.Append(c);
            }
        }
        result.Add(field.ToString().Trim().Trim('"'));
        return result;
    }

    private static TransactionDto ToDto(Transaction t) => new(
        t.Id, t.Amount, t.Date, t.Description, t.MerchantName, t.Source,
        t.CategoryId, t.Category?.Name, t.Category?.Color,
        t.AccountId, t.Account?.Name, t.RecurringTransactionId);
}
