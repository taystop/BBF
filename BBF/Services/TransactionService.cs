using BBF.Data;
using BBF.Data.Entities;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

namespace BBF.Services;

public class TransactionService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public TransactionService(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task CreateAsync(Transaction transaction)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();

        if (transaction.AccountId is int accountId)
        {
            await AccountService.RecalculateBalanceAsync(accountId, db);
            await db.SaveChangesAsync();
        }
    }

    public async Task UpdateAsync(int transactionId, decimal amount, DateTime date, string description,
        string? merchantName, int? categoryId, int? accountId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.Transactions.FindAsync(transactionId);
        if (entity is null) return;

        var previousAccountId = entity.AccountId;

        entity.Amount = amount;
        entity.Date = date;
        entity.Description = description;
        entity.MerchantName = merchantName;
        entity.CategoryId = categoryId;
        entity.AccountId = accountId;
        await db.SaveChangesAsync();

        if (previousAccountId is int oldAccountId)
            await AccountService.RecalculateBalanceAsync(oldAccountId, db);
        if (accountId is int newAccountId && newAccountId != previousAccountId)
            await AccountService.RecalculateBalanceAsync(newAccountId, db);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int transactionId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.Transactions.FindAsync(transactionId);
        if (entity is null) return;

        var accountId = entity.AccountId;
        db.Transactions.Remove(entity);
        await db.SaveChangesAsync();

        if (accountId is int id)
        {
            await AccountService.RecalculateBalanceAsync(id, db);
            await db.SaveChangesAsync();
        }
    }

    public async Task<(int Imported, int Skipped)> ImportCsvBatchAsync(IBrowserFile file, int? groupId, int? accountId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        using var reader = new StreamReader(file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024));
        var headerLine = await reader.ReadLineAsync();
        if (headerLine is null)
            return (0, 0);

        // Detect column layout from header
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

        if (descIdx < 0) descIdx = dateIdx + 1; // fallback: assume description is next column

        var imported = 0;
        var skipped = 0;
        string? line;

        while ((line = await reader.ReadLineAsync()) is not null)
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
                if (!decimal.TryParse(cols[amountIdx], System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out amount))
                {
                    skipped++;
                    continue;
                }
            }
            else
            {
                // Debit/Credit columns
                decimal.TryParse(debitIdx >= 0 && debitIdx < cols.Count ? cols[debitIdx] : "",
                    System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var debit);
                decimal.TryParse(creditIdx >= 0 && creditIdx < cols.Count ? cols[creditIdx] : "",
                    System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var credit);
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
            await db.SaveChangesAsync();

        if (accountId is int id)
        {
            await AccountService.RecalculateBalanceAsync(id, db);
            await db.SaveChangesAsync();
        }

        return (imported, skipped);
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var inQuotes = false;
        var field = new System.Text.StringBuilder();

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
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
}
