using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Linq;
using System.Collections.Generic; // Wajib untuk List<>
using CostMasterAI.Core.Models;   // Wajib untuk Recipe & Ingredient
using Windows.Storage;

namespace CostMasterAI.Services
{
    public class AIService
    {
        private readonly HttpClient _httpClient;

        // Template URL Gemini API
        private const string BaseUrlTemplate = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent";

        public AIService()
        {
            _httpClient = new HttpClient();
        }

        // --- HELPER: AMBIL SETTINGS ---
        private (string apiKey, string model) GetSettings()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            var key = settings["ApiKey"] as string ?? "";
            var model = settings["AiModel"] as string ?? "gemini-1.5-flash"; // Default Model
            return (key, model);
        }

        // ==========================================
        // KATEGORI 1: BASIC AUTOMATION
        // ==========================================

        public async Task<string> GenerateRecipeDataAsync(string recipeName)
        {
            var (apiKey, model) = GetSettings();
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
            if (string.IsNullOrEmpty(apiKey)) return "[Error: API Key belum diatur]";

            var prompt = $"Buatkan deskripsi makanan yang menggugah selera untuk menu '{recipeName}'. Bahannya: {ingredients}. Gaya bahasa: Gaul, santai, tapi menjual (copywriting). Maksimal 50 kata.";
            return await SendPromptAsync(prompt, apiKey, model);
        }

        // ==========================================
        // KATEGORI 2: PROFIT DOCTOR & COST ENGINEERING
        // ==========================================

        public async Task<string> AnalyzeProfitabilityAsync(List<Recipe> recipes)
        {
            var (apiKey, model) = GetSettings();
            if (string.IsNullOrEmpty(apiKey)) return "[Error: API Key belum diatur]";

            // Kita kirim ringkasan data ke AI agar token tidak jebol
            var sb = new StringBuilder();
            sb.AppendLine("Bertindaklah sebagai Konsultan Keuangan F&B. Berikut adalah data margin resep saya:");

            foreach (var r in recipes.Take(20)) // Batasi 20 resep teratas
            {
                sb.AppendLine($"- {r.Name}: HPP Rp{r.CostPerUnit:N0}, Harga Jual Rp{r.ActualSellingPrice:N0}, Margin {(r.ActualSellingPrice > 0 ? ((r.ActualSellingPrice - r.CostPerUnit) / r.ActualSellingPrice * 100) : 0):N1}%");
            }

            sb.AppendLine("\nInstruksi:");
            sb.AppendLine("1. Identifikasi menu yang marginnya SEHAT (>40%).");
            sb.AppendLine("2. Identifikasi menu yang marginnya KRITIS (<30%).");
            sb.AppendLine("3. Berikan saran strategi harga atau efisiensi spesifik untuk menu yang kritis.");
            sb.AppendLine("Gunakan format markdown yang rapi dengan emoji.");

            return await SendPromptAsync(sb.ToString(), apiKey, model);
        }

        // Generate Resep dari Stok Bahan (Smart Chef)
        public async Task<Recipe> GenerateRecipeFromIngredientsAsync(List<Ingredient> availableIngredients)
        {
            var (apiKey, model) = GetSettings();
            if (string.IsNullOrEmpty(apiKey)) return null;

            var sb = new StringBuilder();
            sb.AppendLine("Saya memiliki sisa stok bahan berikut:");
            foreach (var ing in availableIngredients)
            {
                sb.AppendLine($"- {ing.Name}");
            }
            sb.AppendLine("\nInstruksi: Ciptakan SATU resep inovatif yang bisa dibuat dominan menggunakan bahan-bahan di atas. Anda boleh menambahkan bumbu dasar umum (Garam, Gula, Air).");
            sb.AppendLine("OUTPUT WAJIB JSON dengan struktur berikut (Tanpa markdown):");
            sb.AppendLine(@"{ ""name"": ""Nama Resep Keren"", ""description"": ""Deskripsi singkat"", ""yield_qty"": 10, ""items"": [ { ""ingredient_name"": ""Nama Bahan"", ""qty"": 100, ""unit"": ""Gram"" } ] }");

            string jsonResponse = await SendPromptAsync(sb.ToString(), apiKey, model);

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var aiData = JsonSerializer.Deserialize<AiRecipeDataRaw>(jsonResponse, options); // Butuh class helper internal

                if (aiData != null)
                {
                    var newRecipe = new Recipe
                    {
                        Name = aiData.Name,
                        Description = aiData.Description,
                        YieldQty = aiData.Yield_Qty,
                        LastUpdated = DateTime.Now
                    };

                    // Mapping Items (Perlu dicocokkan kembali dengan ID bahan asli di ViewModel nanti)
                    // Di sini kita return Recipe object polosan dulu
                    return newRecipe;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"JSON Parse Error: {ex.Message}");
            }

            return null;
        }

        // ==========================================
        // KATEGORI 3: SALES FORECASTING
        // ==========================================

        public async Task<string> ForecastSalesAsync(List<Transaction> history)
        {
            var (apiKey, model) = GetSettings();
            if (string.IsNullOrEmpty(apiKey)) return "[Error: API Key belum diatur]";

            var sb = new StringBuilder();
            sb.AppendLine("Berikut adalah riwayat penjualan harian saya selama 1 minggu terakhir:");

            var dailySales = history
                .Where(t => t.Type == "Income")
                .GroupBy(t => t.Date.Date)
                .OrderBy(g => g.Key)
                .Select(g => $"{g.Key:dd/MM}: Rp {g.Sum(t => t.Amount):N0}")
                .ToList();

            if (!dailySales.Any()) return "Belum ada data transaksi yang cukup untuk prediksi.";

            foreach (var s in dailySales) sb.AppendLine(s);

            sb.AppendLine("\nInstruksi:");
            sb.AppendLine("1. Analisa tren penjualan (Naik/Turun/Stabil).");
            sb.AppendLine("2. Prediksi total omset untuk 3 hari ke depan.");
            sb.AppendLine("3. Berikan saran stok (belanja lebih banyak atau kurangi).");
            sb.AppendLine("Jawab dengan gaya Data Analyst yang santai.");

            return await SendPromptAsync(sb.ToString(), apiKey, model);
        }

        // ==========================================
        // KATEGORI 4: CREATIVE STUDIO (MARKETING)
        // ==========================================

        public async Task<string> GenerateMarketingContent(string productName, string platform)
        {
            var (apiKey, model) = GetSettings();
            if (string.IsNullOrEmpty(apiKey)) return "[Error: API Key belum diatur]";

            string prompt = platform switch
            {
                "Instagram" => $"Buatkan caption Instagram yang aesthetic dan menggoda untuk menu '{productName}'. Pakai emoji, hashtag relevan, dan gaya bahasa anak Jaksel/Foodies.",
                "TikTok" => $"Buatkan naskah video TikTok pendek (15 detik) untuk mempromosikan '{productName}'. Tuliskan Hook, Isi, dan CTA yang viral. Gaya bahasa seru dan cepat.",
                "WhatsApp" => $"Buatkan pesan broadcast WhatsApp untuk pelanggan setia. Tawarkan promo terbatas untuk menu '{productName}'. Bahasa sopan tapi mendesak (FOMO).",
                _ => $"Buatkan deskripsi promosi untuk {productName}."
            };

            return await SendPromptAsync(prompt, apiKey, model);
        }

        public async Task<string> AuditRecipeCostAsync(Recipe recipe)
        {
            var (apiKey, model) = GetSettings();
            if (string.IsNullOrEmpty(apiKey)) return "[Error: API Key belum diatur]";

            var sb = new StringBuilder();
            sb.AppendLine($"Analisa Food Cost untuk resep bisnis kuliner ini:");
            sb.AppendLine($"Nama Menu: {recipe.Name}");
            sb.AppendLine($"Total HPP (Cost): Rp {recipe.CostPerUnit:N0}");
            sb.AppendLine($"Harga Jual (Dine In): Rp {recipe.ActualSellingPrice:N0}");

            // Hitung manual food cost % untuk dikirim ke AI
            decimal fcPercent = 0;
            if (recipe.ActualSellingPrice > 0)
                fcPercent = (recipe.CostPerUnit / recipe.ActualSellingPrice) * 100;

            sb.AppendLine($"Food Cost Percentage: {fcPercent:N1}%");
            sb.AppendLine("Instruksi: Apakah persentase food cost ini wajar? Berikan kritik dan saran jika terlalu tinggi (>40%) atau terlalu rendah (mencurigakan).");

            return await SendPromptAsync(sb.ToString(), apiKey, model);
        }

        public async Task<string> GetSmartSubstitutionsAsync(Recipe recipe)
        {
            var (apiKey, model) = GetSettings();
            if (string.IsNullOrEmpty(apiKey)) return "[Error: API Key belum diatur]";

            var sb = new StringBuilder();
            sb.AppendLine($"Saran substitusi bahan untuk menekan HPP resep '{recipe.Name}':");
            if (recipe.Items != null)
            {
                foreach (var item in recipe.Items)
                {
                    var ingName = item.Ingredient?.Name ?? "Unknown";
                    sb.AppendLine($"- {ingName}: Rp {item.CalculatedCost:N0}");
                }
            }
            sb.AppendLine("Instruksi: Identifikasi bahan termahal dan sarankan alternatif lebih murah tanpa merusak rasa.");

            return await SendPromptAsync(sb.ToString(), apiKey, model);
        }

        public async Task<string> GetWasteReductionIdeasAsync(Recipe recipe)
        {
            var (apiKey, model) = GetSettings();
            if (string.IsNullOrEmpty(apiKey)) return "[Error: API Key belum diatur]";

            var sb = new StringBuilder();
            sb.AppendLine($"Ide olahan limbah/sisa dari resep '{recipe.Name}':");
            if (recipe.Items != null)
            {
                foreach (var item in recipe.Items)
                {
                    sb.AppendLine($"- {item.Ingredient?.Name}");
                }
            }
            sb.AppendLine("Instruksi: Identifikasi potensi limbah (kulit, sisa potongan) dan ide menu baru untuk menjualnya.");

            return await SendPromptAsync(sb.ToString(), apiKey, model);
        }

        public async Task<string> GenerateSocialMediaCaptionAsync(Recipe recipe, string platform, string tone)
        {
            var (apiKey, model) = GetSettings();
            if (string.IsNullOrEmpty(apiKey)) return "[Error: API Key belum diatur]";

            var sb = new StringBuilder();
            sb.AppendLine($"Buatkan Caption {platform} untuk menu '{recipe.Name}' dengan tone {tone}.");
            sb.AppendLine($"Harga: Rp {recipe.ActualSellingPrice:N0}.");
            return await SendPromptAsync(sb.ToString(), apiKey, model);
        }

        public async Task<string> GenerateHypnoticDescriptionAsync(Recipe recipe)
        {
            var (apiKey, model) = GetSettings();
            if (string.IsNullOrEmpty(apiKey)) return "[Error: API Key belum diatur]";
            return await SendPromptAsync($"Tulis deskripsi menu '{recipe.Name}' dengan teknik NLP Hypnotic Copywriting.", apiKey, model);
        }

        public async Task<string> GenerateImagePromptAsync(Recipe recipe)
        {
            var (apiKey, model) = GetSettings();
            if (string.IsNullOrEmpty(apiKey)) return "[Error: API Key belum diatur]";
            return await SendPromptAsync($"Buatkan Image Prompt (Bahasa Inggris) untuk AI Art Generator (Midjourney) agar menghasilkan foto makanan '{recipe.Name}' yang fotorealistik.", apiKey, model);
        }

        // ==========================================
        // CORE: HTTP CLIENT KE GEMINI API
        // ==========================================
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

                var finalUrl = string.Format(BaseUrlTemplate, model) + $"?key={apiKey}";

                var response = await _httpClient.PostAsync(finalUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var node = JsonNode.Parse(responseString);
                    var result = node?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

                    if (result != null)
                    {
                        // Bersihkan markdown
                        result = result.Replace("```json", "").Replace("```", "").Trim();
                    }
                    return result ?? "";
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"AI Error: {response.StatusCode} - {responseString}");
                    return $"[Error AI: {response.StatusCode}. Cek API Key/Quota]";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AI Exception: {ex.Message}");
                return $"[Exception: {ex.Message}]";
            }
        }

        // Helper Class untuk JSON Parsing Internal (Smart Chef)
        private class AiRecipeDataRaw
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public int Yield_Qty { get; set; }
            // Items diabaikan dulu karena mapping ke Ingredient ID butuh logic di ViewModel
        }
    }
}