using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CostMasterAI.Core.Models;
using CostMasterAI.Core.Services;
using CostMasterAI.Helpers;
using CostMasterAI.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace CostMasterAI.ViewModels
{
    public partial class AIAssistantViewModel : ObservableObject
    {
        private readonly AppDbContext _dbContext;
        private readonly AIService _aiService;

        // --- STATE UI ---
        [ObservableProperty] private bool _isThinking;
        [ObservableProperty] private string _thinkingMessage = "AI sedang berpikir...";

        // --- HASIL ANALISA (OUTPUT) ---
        [ObservableProperty] private string _profitAnalysisResult = "Klik tombol 'Audit Sekarang' untuk analisa.";
        [ObservableProperty] private string _salesForecastResult = "Klik 'Mulai Prediksi' untuk analisa tren.";
        [ObservableProperty] private string _recipeResult = "Hasil kreasi resep akan muncul di sini."; // NEW PROPERTY
        [ObservableProperty] private string _generatedCaption = "";

        // --- INPUTS ---
        [ObservableProperty] private string _marketingProductName = "";
        [ObservableProperty] private string _selectedPlatform = "Instagram";
        public List<string> Platforms { get; } = new() { "Instagram", "TikTok", "WhatsApp", "Facebook" };

        // --- KOLEKSI DATA ---
        public ObservableCollection<Ingredient> LowStockIngredients { get; } = new();

        public AIAssistantViewModel(AppDbContext dbContext)
        {
            _dbContext = dbContext;
            _aiService = new AIService();
            _ = LoadInitialData();
        }

        private async Task LoadInitialData()
        {
            try
            {
                await _dbContext.Database.EnsureCreatedAsync();

                // Ambil bahan yang stoknya > 0 (Tersedia)
                var ingredients = await _dbContext.Ingredients
                    .AsNoTracking()
                    .Where(i => i.CurrentStock > 0)
                    .OrderByDescending(i => i.CurrentStock)
                    .Take(10)
                    .ToListAsync();

                LowStockIngredients.Clear();
                foreach (var item in ingredients) LowStockIngredients.Add(item);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error Load Data: {ex.Message}"); }
        }

        // 1. PROFIT DOCTOR
        [RelayCommand]
        private async Task RunProfitAudit()
        {
            IsThinking = true;
            ThinkingMessage = "Gemini sedang membedah resep...";
            try
            {
                var recipes = await _dbContext.Recipes
                    .Include(r => r.Items).ThenInclude(i => i.Ingredient)
                    .AsNoTracking().ToListAsync();

                if (!recipes.Any())
                {
                    ProfitAnalysisResult = "Belum ada data resep.";
                    return;
                }

                ProfitAnalysisResult = await _aiService.AnalyzeProfitabilityAsync(recipes);
            }
            catch (Exception ex) { ProfitAnalysisResult = $"Error: {ex.Message}"; }
            finally { IsThinking = false; }
        }

        // 2. SMART INVENTORY CHEF
        [RelayCommand]
        private async Task CreateRecipeFromStock()
        {
            if (!LowStockIngredients.Any()) return;

            IsThinking = true;
            ThinkingMessage = "Chef Gemini sedang meracik menu...";

            try
            {
                var stockList = LowStockIngredients.ToList();
                var newRecipe = await _aiService.GenerateRecipeFromIngredientsAsync(stockList);

                if (newRecipe != null)
                {
                    _dbContext.Recipes.Add(newRecipe);
                    await _dbContext.SaveChangesAsync();
                    WeakReferenceMessenger.Default.Send(new RecipesChangedMessage("Created"));

                    // Tampilkan di properti khusus RecipeResult
                    RecipeResult = $@"✅ BERHASIL DITAMBAHKAN!
Nama: {newRecipe.Name}
Yield: {newRecipe.YieldQty} porsi

Deskripsi:
{newRecipe.Description}

*Detail bahan telah disesuaikan dengan stok.*";
                }
                else
                {
                    RecipeResult = "Gagal membuat resep. Silakan coba lagi.";
                }
            }
            catch (Exception ex) { RecipeResult = $"Error: {ex.Message}"; }
            finally { IsThinking = false; }
        }

        // 3. SALES FORECAST
        [RelayCommand]
        private async Task PredictSales()
        {
            IsThinking = true;
            ThinkingMessage = "Mempelajari pola transaksi...";
            try
            {
                var history = await _dbContext.Transactions.AsNoTracking().ToListAsync();
                if (history.Count < 5)
                {
                    SalesForecastResult = "Data transaksi kurang (min 5).";
                    return;
                }
                SalesForecastResult = await _aiService.ForecastSalesAsync(history);
            }
            catch (Exception ex) { SalesForecastResult = $"Error: {ex.Message}"; }
            finally { IsThinking = false; }
        }

        // 4. MARKETING WIZARD
        [RelayCommand]
        private async Task CreateCaption()
        {
            if (string.IsNullOrWhiteSpace(MarketingProductName)) return;
            IsThinking = true;
            ThinkingMessage = "Menulis copy marketing...";
            try
            {
                GeneratedCaption = await _aiService.GenerateMarketingContent(MarketingProductName, SelectedPlatform);
            }
            catch (Exception ex) { GeneratedCaption = $"Error: {ex.Message}"; }
            finally { IsThinking = false; }
        }
    }
}