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

        // --- HASIL ANALISA ---
        [ObservableProperty] private string _profitAnalysisResult = "Klik tombol 'Audit Profit' untuk memulai analisa margin resep Anda.";
        [ObservableProperty] private string _salesForecastResult = "Dapatkan prediksi penjualan berdasarkan data historis.";
        [ObservableProperty] private string _generatedCaption = "";

        // --- INPUTS ---
        [ObservableProperty] private string _marketingProductName = "";
        [ObservableProperty] private string _selectedPlatform = "Instagram";
        public List<string> Platforms { get; } = new() { "Instagram", "TikTok", "WhatsApp" };

        // --- KOLEKSI DATA ---
        public ObservableCollection<Ingredient> LowStockIngredients { get; } = new();

        public AIAssistantViewModel()
        {
            _dbContext = new AppDbContext();
            _aiService = new AIService();
            _ = LoadInitialData();
        }

        private async Task LoadInitialData()
        {
            await _dbContext.Database.EnsureCreatedAsync();

            // Load bahan dummy untuk simulasi stok menipis (Logic random)
            var ingredients = await _dbContext.Ingredients.AsNoTracking().Take(5).ToListAsync();
            LowStockIngredients.Clear();
            foreach (var item in ingredients) LowStockIngredients.Add(item);
        }

        // 1. FITUR PROFIT DOCTOR
        [RelayCommand]
        private async Task RunProfitAudit()
        {
            IsThinking = true;
            ThinkingMessage = "Menganalisa struktur biaya resep...";
            try
            {
                var recipes = await _dbContext.Recipes.Include(r => r.Items).ThenInclude(i => i.Ingredient).ToListAsync();
                ProfitAnalysisResult = await _aiService.AnalyzeProfitabilityAsync(recipes);
            }
            finally { IsThinking = false; }
        }

        // 2. FITUR SMART CHEF (CREATE RECIPE)
        [RelayCommand]
        private async Task CreateRecipeFromStock()
        {
            IsThinking = true;
            ThinkingMessage = "Meracik resep dari sisa bahan...";
            try
            {
                var stock = LowStockIngredients.ToList();
                var newRecipe = await _aiService.GenerateRecipeFromIngredientsAsync(stock);

                // Simpan ke DB
                _dbContext.Recipes.Add(newRecipe);
                await _dbContext.SaveChangesAsync();

                // Kabari halaman Resep
                WeakReferenceMessenger.Default.Send(new RecipesChangedMessage("Created"));

                ProfitAnalysisResult = $"✅ Sukses! Resep '{newRecipe.Name}' telah ditambahkan ke Database Resep.";
            }
            finally { IsThinking = false; }
        }

        // 3. FITUR SALES FORECAST
        [RelayCommand]
        private async Task PredictSales()
        {
            IsThinking = true;
            ThinkingMessage = "Mempelajari pola transaksi...";
            try
            {
                var history = await _dbContext.Transactions.AsNoTracking().ToListAsync();
                SalesForecastResult = await _aiService.ForecastSalesAsync(history);
            }
            finally { IsThinking = false; }
        }

        // 4. FITUR MARKETING WIZARD
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
            finally { IsThinking = false; }
        }
    }
}