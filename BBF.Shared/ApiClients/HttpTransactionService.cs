using System.Net.Http.Json;
using BBF.Shared.Contracts.Transactions;

namespace BBF.Shared.ApiClients;

public class HttpTransactionService(HttpClient http) : ITransactionService
{
    public async Task<List<TransactionDto>> GetTransactionsAsync(int groupId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<TransactionDto>>($"/api/groups/{groupId}/transactions", ct) ?? [];

    public async Task<TransactionDto> CreateAsync(int groupId, CreateTransactionRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"/api/groups/{groupId}/transactions", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TransactionDto>(ct)
            ?? throw new InvalidOperationException("Empty response creating transaction.");
    }

    public async Task UpdateAsync(int groupId, int transactionId, UpdateTransactionRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/groups/{groupId}/transactions/{transactionId}", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(int groupId, int transactionId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/api/groups/{groupId}/transactions/{transactionId}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<CsvImportResult> ImportCsvAsync(int groupId, int? accountId, Stream csvContent, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(csvContent);
        form.Add(fileContent, "file", "import.csv");
        if (accountId is int id)
            form.Add(new StringContent(id.ToString()), "accountId");

        var response = await http.PostAsync($"/api/groups/{groupId}/transactions/import-csv", form, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CsvImportResult>(ct)
            ?? throw new InvalidOperationException("Empty response importing CSV.");
    }
}
