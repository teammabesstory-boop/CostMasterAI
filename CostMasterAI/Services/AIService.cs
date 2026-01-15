using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Linq; // Penting buat LINQ
using Windows.Storage;
using CostMasterAI.Models;

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

            // Ambil Key (Kalau gak ada, balikin string kosong)
            var key = settings["ApiKey"] as string ?? "";

            // Ambil Model (Default ke flash kalau belum diset)
            var model = settings["AiModel"] as string ?? "gemini-1.5-flash";

            return (key, model);
        }

        // ==========================================
        // KATEGORI 1: BASIC AUTOMATION
        // ==========================================

        // Generate daftar bahan & harga pasar dari nama resep
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

        // Generate deskripsi singkat (Fitur lama)
        public async Task<string> GenerateMarketingCopyAsync(string recipeName, string ingredients)
        {
            var (apiKey, model) = GetSettings();
            if (string.IsNullOrEmpty(apiKey)) return "[Error: API Key belum diatur di menu Settings]";

            var prompt = $"Buatkan deskripsi makanan yang menggugah selera untuk menu '{recipeName}'. Bahannya: {ingredients}. Gaya bahasa: Gaul, santai, tapi menjual (copywriting). Maksimal 50 kata.";
            return await SendPromptAsync(prompt, apiKey, model);
        }

        // ==========================================
        // KATEGORI 2: COST ENGINEERING (KONSULTAN)
        // ==========================================

        // Audit kewajaran HPP
        public async Task<string> AuditRecipeCostAsync(Recipe recipe)
        {
            var (apiKey, model) = GetSettings();
            if (string.IsNullOrEmpty(apiKey)) return "[Error: API Key belum diatur]";

            var sb = new StringBuilder();
            sb.AppendLine($"Analisa Food Cost untuk resep bisnis kuliner ini:");
            sb.AppendLine($"Nama Menu: {recipe.Name}");
            sb.AppendLine($"Total HPP (Cost): Rp {recipe.CostPerUnit:N0}");
            sb.AppendLine($"Harga Jual (Dine In): Rp {recipe.DineInPrice:N0}");
            sb.AppendLine($"Food Cost Percentage: {recipe.FoodCostPercentage}%");
            sb.AppendLine("Instruksi: Apakah persentase food cost ini wajar untuk kategori makanan tersebut? (Standar industri rata-rata 30-35%). Jika terlalu tinggi (>40%), berikan kritik tajam dan saran spesifik cara menurunkannya. Jawab dengan bahasa Indonesia yang profesional.");

            return await SendPromptAsync(sb.ToString(), apiKey, model);
        }

        // Saran substitusi bahan murah
        public async Task<string> GetSmartSubstitutionsAsync(Recipe recipe)
        {
            var (apiKey, model) = GetSettings();
            if (string.IsNullOrEmpty(apiKey)) return "[Error: API Key belum diatur]";

            var sb = new StringBuilder();
            sb.AppendLine($"Saya butuh saran substitusi bahan untuk menekan HPP resep ini:");
            sb.AppendLine($"Menu: {recipe.Name}");
            sb.AppendLine("Daftar Bahan & Biaya:");
            if (recipe.Items != null)
            {
                foreach (var item in recipe.Items)
                {
                    var ingName = item.Ingredient?.Name ?? "Unknown";
                    sb.AppendLine($"- {ingName}: Rp {item.CalculatedCost:N0} (Qty: {item.UsageQty} {item.UsageUnit})");
                }
            }
            sb.AppendLine("Instruksi: Identifikasi 3 bahan termahal (Pareto). Sarankan alternatif bahan pengganti yang lebih murah TAPI kualitas rasa tidak jatuh drastis. Berikan estimasi penghematan per porsi jika diganti.");

            return await SendPromptAsync(sb.ToString(), apiKey, model);
        }

        // Ide olahan limbah sisa
        public async Task<string> GetWasteReductionIdeasAsync(Recipe recipe)
        {
            var (apiKey, model) = GetSettings();
            if (string.IsNullOrEmpty(apiKey)) return "[Error: API Key belum diatur]";

            var sb = new StringBuilder();
            sb.AppendLine($"Analisa potensi limbah/sisa bahan dari resep ini:");
            sb.AppendLine($"Menu: {recipe.Name}");
            sb.AppendLine("Bahan yang dipakai:");
            if (recipe.Items != null)
            {
                foreach (var item in recipe.Items)
                {
                    var ingName = item.Ingredient?.Name ?? "Unknown";
                    sb.AppendLine($"- {ingName}");
                }
            }
            sb.AppendLine("Instruksi: Identifikasi bahan sisa (by-product) yang biasanya terbuang dari resep ini (contoh: Putih telur sisa dari resep yang cuma pakai kuningnya, atau kulit buah, tulang, dll). Sarankan 2 menu baru spesifik yang bisa dibuat dari limbah tersebut untuk dijual kembali (Upcycling).");

            return await SendPromptAsync(sb.ToString(), apiKey, model);
        }

        // ==========================================
        // KATEGORI 3: CREATIVE STUDIO (MARKETING)
        // ==========================================

        // Social Media Manager (Caption Generator)
        public async Task<string> GenerateSocialMediaCaptionAsync(Recipe recipe, string platform, string tone)
        {
            var (apiKey, model) = GetSettings();
            if (string.IsNullOrEmpty(apiKey)) return "[Error: API Key belum diatur]";

            var sb = new StringBuilder();
            sb.AppendLine($"Bertindaklah sebagai Social Media Specialist handal.");
            sb.AppendLine($"Buatkan Caption untuk postingan di {platform} tentang menu '{recipe.Name}'.");
            sb.AppendLine($"Target Audiens: Pecinta kuliner.");
            sb.AppendLine($"Tone/Gaya Bahasa: {tone}.");
            sb.AppendLine($"Harga: Rp {recipe.DineInPrice:N0}.");
            sb.AppendLine("Instruksi:");
            sb.AppendLine("1. Buat Headline yang 'Hook' (menarik perhatian).");
            sb.AppendLine("2. Deskripsikan rasa dengan emoji yang relevan.");
            sb.AppendLine("3. Sertakan Call to Action (CTA) yang jelas.");
            sb.AppendLine("4. Berikan 10 hashtag relevan dan trending.");

            return await SendPromptAsync(sb.ToString(), apiKey, model);
        }

        // Hypnotic Copywriting (Deskripsi Menu)
        public async Task<string> GenerateHypnoticDescriptionAsync(Recipe recipe)
        {
            var (apiKey, model) = GetSettings();
            if (string.IsNullOrEmpty(apiKey)) return "[Error: API Key belum diatur]";

            var sb = new StringBuilder();
            sb.AppendLine($"Bertindaklah sebagai Copywriter Kuliner profesional dengan teknik NLP (Neuro-Linguistic Programming).");
            sb.AppendLine($"Tulis deskripsi menu untuk '{recipe.Name}' yang menghipnotis dan bikin lapar.");
            sb.AppendLine("Instruksi:");
            sb.AppendLine("- Gunakan kata-kata sensorik (Sensory Words) yang menyentuh indra perasa, penciuman, dan penglihatan.");
            sb.AppendLine("- Jangan hanya bilang 'enak', tapi jelaskan teksturnya (misal: lumer, renyah, creamy, smokey).");
            sb.AppendLine("- Buat seolah-olah pembaca sudah merasakannya di mulut mereka.");
            sb.AppendLine("- Panjang: Sekitar 2-3 paragraf pendek.");

            return await SendPromptAsync(sb.ToString(), apiKey, model);
        }

        // AI Art Director (Image Prompt Generator)
        public async Task<string> GenerateImagePromptAsync(Recipe recipe)
        {
            var (apiKey, model) = GetSettings();
            if (string.IsNullOrEmpty(apiKey)) return "[Error: API Key belum diatur]";

            var sb = new StringBuilder();
            sb.AppendLine($"Bertindaklah sebagai AI Art Director. Saya ingin men-generate gambar makanan '{recipe.Name}' menggunakan AI (seperti Midjourney, Bing Image Creator, atau DALL-E).");
            sb.AppendLine("Tugasmu: Buatkan PROMPT (Teks Perintah) yang sangat detail dalam Bahasa Inggris untuk menghasilkan gambar yang fotorealistik dan menggugah selera.");
            sb.AppendLine("Sertakan detail tentang:");
            sb.AppendLine("- Angle kamera (misal: 45-degree angle, macro shot, flat lay).");
            sb.AppendLine("- Pencahayaan (misal: natural soft morning light, cinematic rim lighting, moody).");
            sb.AppendLine("- Plating dan Garnish (penataan makanan).");
            sb.AppendLine("- Background (misal: rustic wooden table, marble top, blurred restaurant background).");
            sb.AppendLine("- Kualitas keywords (misal: 8k, highly detailed, photorealistic, food photography, masterpiece).");
            sb.AppendLine("Output HANYA prompt-nya saja dalam bahasa Inggris.");

            return await SendPromptAsync(sb.ToString(), apiKey, model);
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

                // RAKIT URL DINAMIS
                var finalUrl = string.Format(BaseUrlTemplate, model) + $"?key={apiKey}";

                var response = await _httpClient.PostAsync(finalUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var node = JsonNode.Parse(responseString);
                    var result = node?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

                    if (result != null)
                    {
                        // Bersihkan format Markdown JSON biar bersih
                        result = result.Replace("```json", "").Replace("```", "").Trim();
                    }
                    return result ?? "";
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"AI Error: {response.StatusCode} - {responseString}");
                    return $"[Error from AI: {response.StatusCode}. Cek API Key/Koneksi/Model]";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AI Exception: {ex.Message}");
                return $"[Exception: {ex.Message}]";
            }
        }
    }
}