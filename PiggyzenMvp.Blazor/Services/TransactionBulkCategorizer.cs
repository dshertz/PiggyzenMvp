using System.Net.Http.Json;
using PiggyzenMvp.Blazor.DTOs;
using PiggyzenMvp.Blazor.DTOs.Transactions;

namespace PiggyzenMvp.Blazor.Services;

public class TransactionBulkCategorizer
{
    private readonly IHttpClientFactory _httpClientFactory;

    public TransactionBulkCategorizer(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<TransactionBulkCategorizationResult> CategorizeAsync(
        IEnumerable<TransactionDto> transactions,
        int targetCategoryId)
    {
        var selected = transactions.ToList();
        var client = _httpClientFactory.CreateClient("ApiClient");
        var result = new TransactionBulkCategorizationResult();

        var manualTargets = selected.Where(t => t.CategoryId == null).ToList();
        var changeTargets = selected.Where(t => t.CategoryId != null).ToList();

        await ProcessManualCategorizeAsync(client, manualTargets, targetCategoryId, result);
        await ProcessChangeCategoryAsync(client, changeTargets, targetCategoryId, result);

        return result;
    }

    private static async Task ProcessManualCategorizeAsync(
        HttpClient client,
        List<TransactionDto> manualTargets,
        int targetCategoryId,
        TransactionBulkCategorizationResult result)
    {
        foreach (var group in manualTargets.GroupBy(t => t.Amount >= 0m))
        {
            var requests = group
                .Select(t => new CategorizeRequest(t.Id, targetCategoryId))
                .ToList();

            if (requests.Count == 0)
            {
                continue;
            }

            var response = await client.PostAsJsonAsync("api/transactions/manual-categorize", requests);
            await EnsureSuccessAsync(response, "kategorisera valda transaktioner");

            var payload = await response.Content.ReadFromJsonAsync<ManualCategorizeResponse>()
                ?? new ManualCategorizeResponse();

            result.Categorized += payload.Categorized;
            result.AutoCategorized += payload.AutoCategorized;
            result.Errors.AddRange(payload.Errors);
        }
    }

    private static async Task ProcessChangeCategoryAsync(
        HttpClient client,
        List<TransactionDto> changeTargets,
        int targetCategoryId,
        TransactionBulkCategorizationResult result)
    {
        foreach (var group in changeTargets.GroupBy(t => t.Amount >= 0m))
        {
            var requests = group
                .Select(t => new ChangeCategoryRequest(t.Id, targetCategoryId))
                .ToList();

            if (requests.Count == 0)
            {
                continue;
            }

            var response = await client.PostAsJsonAsync("api/transactions/change-category", requests);
            await EnsureSuccessAsync(response, "uppdatera kategoriserade transaktioner");

            var payload = await response.Content.ReadFromJsonAsync<ChangeCategoryResponse>()
                ?? new ChangeCategoryResponse();

            result.Updated += payload.Updated;
            result.Errors.AddRange(payload.Errors);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string action)
    {
        if (response.IsSuccessStatusCode)
            return;

        var details = await response.Content.ReadAsStringAsync();
        var message = $"Kunde inte {action}. ({(int)response.StatusCode}) {details}";
        throw new HttpRequestException(message);
    }
}

public class TransactionBulkCategorizationResult
{
    public int Categorized { get; set; }
    public int Updated { get; set; }
    public int AutoCategorized { get; set; }
    public List<ManualCategorizeError> Errors { get; set; } = new();
}
