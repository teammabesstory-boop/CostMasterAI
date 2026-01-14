using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Nodes;
using Windows.Storage; // Perlu ini buat baca Settings

namespace CostMasterAI.Services
{
    public class AIService
    {
        private readonly HttpClient _httpClient;

        // Konstanta dihapus, ganti jadi logic dinamis di bawah
        private const string BaseUrlTemplate = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent";

        public AIService()
        {
            _httpClient = new HttpClient();
        }

        // Helper buat ambil Settingan User
        private (string apiKey, string model) GetSettings()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;

            // Ambil Key (Kalau gak ada, balikin string kosong)
            var key = settings["ApiKey"] as string ?? "";

            // Ambil Model (Default ke flash kalau belum diset)
            var model = settings["AiModel"] as string ?? "gemini-1.5-flash";

            return (key, model);
        }

        public async Task<string> GenerateRecipeDataAsync(string recipeName)
        {
            var (apiKey, model) = GetSettings();

            // Validasi: Kalau user belum set API Key, balikin kosong/error
            if (string.IsNullOrEmpty(apiKey)) return "";

            var prompt = $@"
                Bertindaklah sebagai Chef dan Konsultan Bisnis Kuliner.
                Saya ingin membuat menu: '{recipeName}'.
                
                Tugasmu:
                1. Tentukan bahan-bahan bakunya.
                2. Tentukan estimasi jumlah pemakaian untuk 1 porsi.
                3. Tentukan estimasi HARGA PASARAN (IDR) bahan tersebut per kemasan umum.
                
                OUTPUT WAJIB FORMAT JSON (Tanpa markdown, tanpa teks lain, langsung kurung siku array):
                [
                    {{
                        ""ingredient_name"": ""Nama Bahan"",
                        ""usage_qty"": 100,
                        ""usage_unit"": ""Gram"",
                        ""estimated_price_per_package"": 15000,
                        ""package_qty"": 1000,
                        ""package_unit"": ""Gram""
                    }}
                ]
            ";

            return await SendPromptAsync(prompt, apiKey, model);
        }

        public async Task<string> GenerateMarketingCopyAsync(string recipeName, string ingredients)
        {
            var (apiKey, model) = GetSettings();
            if (string.IsNullOrEmpty(apiKey)) return "[Error: API Key belum diatur di menu Settings]";

            var prompt = $"Buatkan deskripsi makanan yang menggugah selera untuk menu '{recipeName}'. Bahannya: {ingredients}. Gaya bahasa: Gaul, santai, tapi menjual (copywriting). Maksimal 50 kata.";
            return await SendPromptAsync(prompt, apiKey, model);
        }

        // Method SendPrompt sekarang terima parameter Key & Model
        private async Task<string> SendPromptAsync(string prompt, string apiKey, string model)
        {
            try
            {
                var requestBody = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = prompt } } }
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // RAKIT URL DINAMIS
                // Masukin model pilihan user ke URL template
                var finalUrl = string.Format(BaseUrlTemplate, model) + $"?key={apiKey}";

                var response = await _httpClient.PostAsync(finalUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var node = JsonNode.Parse(responseString);
                    var result = node?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

                    if (result != null)
                    {
                        result = result.Replace("```json", "").Replace("```", "").Trim();
                    }
                    return result ?? "";
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"AI Error: {response.StatusCode} - {responseString}");
                    return "";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AI Exception: {ex.Message}");
                return "";
            }
        }
    }
}