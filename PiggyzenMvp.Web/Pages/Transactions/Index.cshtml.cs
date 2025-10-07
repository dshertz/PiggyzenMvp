using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PiggyzenMvp.Web.DTOs;
using static PiggyzenMvp.Web.Pages.Transactions.ImportModel;

namespace PiggyzenMvp.Web.Pages.Transactions
{
    public class IndexModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        public IndexModel(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        public List<TransactionDto> Transactions { get; set; } = new();

        public async Task OnGetAsync()
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_config["ApiBaseUrl"]!);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json")
            );

            var response = await client.GetAsync("api/transactions");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<
                    ApiResponse<List<TransactionDto>>
                >();

                if (result is null || !result.Success)
                {
                    // hantera fel
                    Transactions = new();
                }
                else
                {
                    Transactions = result.Data ?? new();
                }
            }
        }
    }
}
