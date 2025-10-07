using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PiggyzenMvp.Web.DTOs;

namespace PiggyzenMvp.Web.Pages.Transactions;

public class ImportModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public ImportModel(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    [BindProperty]
    public string RawText { get; set; } = "";

    public List<TransactionImportDto> ParsedTransactions { get; set; } = new();

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(RawText))
        {
            ViewData["NotificationMessage"] = "No text provided.";
            ViewData["NotificationType"] = "danger"; // success, info, warning, danger
            return Page();
        }

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_config["ApiBaseUrl"]!);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json")
        );

        var content = new StringContent(RawText, Encoding.UTF8, "text/plain");
        var response = await client.PostAsync("api/transactions/import", content);

        var result = await response.Content.ReadFromJsonAsync<
            ApiResponse<List<TransactionImportDto>>
        >();

        if (result is null || !result.Success)
        {
            ViewData["NotificationMessage"] = result?.Errors?.FirstOrDefault() ?? "Import failed.";
            ViewData["NotificationType"] = "danger";
            return Page();
        }

        ParsedTransactions = result.Data ?? new();
        ViewData["NotificationMessage"] = "Import successful.";
        ViewData["NotificationType"] = "success";

        return Page(); // Visa meddelande direkt
    }

    /* public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(RawText))
        {
            return Page();
        }

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_config["ApiBaseUrl"]!);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json")
        );

        var content = new StringContent(RawText, Encoding.UTF8, "text/plain");
        var response = await client.PostAsync("api/transactions/import", content);

        var result = await response.Content.ReadFromJsonAsync<
            ApiResponse<List<TransactionImportDto>>
        >();

        if (result is null || !result.Success)
        {
            return Page();
        }

        ParsedTransactions = result.Data ?? new();
        return Page();
    } */

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }
    }
}
