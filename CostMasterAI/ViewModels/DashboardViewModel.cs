using CommunityToolkit.Mvvm.ComponentModel;
using CostMasterAI.Services;
using CostMasterAI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CostMasterAI.ViewModels
{
    // Helper Class buat Chart Data
    public class CostStructureItem
    {
        public string Category { get; set; }
        public decimal Amount { get; set; }
        public double Percentage { get; set; }
        public string Color { get; set; } // Hex Color buat UI
    }

    public class TopCostDriver
    {
        public string Name { get; set; }
        public decimal TotalCostContribution { get; set; }
        public string Percentage { get; set; }
    }

    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;

        // --- EXECUTIVE SUMMARY (KPIs) ---
        [ObservableProperty] private decimal _totalProductionCost; // Total HPP Semua Resep
        [ObservableProperty] private string _globalGrossProfitMargin = "0%";
        [ObservableProperty] private int _efficiencyScore = 100; // Skala 1-100
        [ObservableProperty] private string _efficiencyStatus = "Excellent";

        // --- COST STRUCTURE (Deep Dive) ---
        [ObservableProperty] private ObservableCollection<CostStructureItem> _costStructure = new();
        [ObservableProperty] private ObservableCollection<TopCostDriver> _topCostDrivers = new();

        // --- PRODUCT INTELLIGENCE (Matrix) ---
        [ObservableProperty] private int _starProductsCount;
        [ObservableProperty] private int _cashCowCount;
        [ObservableProperty] private int _puzzleCount;
        [ObservableProperty] private int _dogCount;

        // --- STATUS ---
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusMessage = "Ready";

        public DashboardViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task RefreshDashboardAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            StatusMessage = "Processing Enterprise Analytics...";

            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    // 1. Fetch Data Lengkap (AsNoTracking for Speed)
                    var recipes = await db.Recipes
                        .AsNoTracking()
                        .Include(r => r.Items).ThenInclude(i => i.Ingredient)
                        .Include(r => r.Overheads)
                        .ToListAsync();

                    var ingredients = await db.Ingredients.AsNoTracking().ToListAsync();

                    if (recipes.Any())
                    {
                        CalculateExecutiveSummary(recipes);
                        CalculateCostStructure(recipes);
                        CalculateProductMatrix(recipes);
                    }
                    else
                    {
                        ResetDashboard();
                    }
                }
                StatusMessage = $"Last Updated: {DateTime.Now:HH:mm}";
            }
            catch (Exception ex)
            {
                StatusMessage = "Analytic Engine Error.";
                System.Diagnostics.Debug.WriteLine($"❌ DASHBOARD ERROR: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // 1. HITUNG KPI UTAMA
        private void CalculateExecutiveSummary(List<Recipe> recipes)
        {
            // Total HPP (Jika memproduksi 1 batch semua resep)
            TotalProductionCost = recipes.Sum(r => r.TotalCost);

            // Global GPM (Rata-rata tertimbang margin semua produk)
            var avgMargin = recipes.Average(r => r.RealMarginPercent);
            GlobalGrossProfitMargin = $"{avgMargin:F1}%";

            // Efficiency Score Algorithm
            // Faktor 1: Cooking Loss (Semakin kecil semakin bagus)
            // Faktor 2: Rata-rata Yield Bahan Baku (Semakin mendekati 100% semakin efisien)
            double totalCookingLoss = recipes.Average(r => r.CookingLossPercent);

            // Base Score 100 dikurangi penalty loss
            double score = 100 - (totalCookingLoss * 1.5);

            if (score > 100) score = 100;
            if (score < 0) score = 0;

            EfficiencyScore = (int)score;

            if (EfficiencyScore >= 90) EfficiencyStatus = "World Class 🏆";
            else if (EfficiencyScore >= 75) EfficiencyStatus = "Good ✅";
            else if (EfficiencyScore >= 50) EfficiencyStatus = "Warning ⚠️";
            else EfficiencyStatus = "Critical 🚨";
        }

        // 2. HITUNG STRUKTUR BIAYA & DRIVERS
        private void CalculateCostStructure(List<Recipe> recipes)
        {
            CostStructure.Clear();
            TopCostDrivers.Clear();

            decimal totalMaterial = recipes.Sum(r => r.TotalMaterialCost);
            decimal totalLabor = recipes.Sum(r => r.TotalLaborCost);
            decimal totalOverhead = recipes.Sum(r => r.TotalOverheadCost);
            decimal grandTotal = totalMaterial + totalLabor + totalOverhead;

            if (grandTotal == 0) return;

            // Visualisasi Bar Data
            CostStructure.Add(new CostStructureItem { Category = "Bahan Baku", Amount = totalMaterial, Percentage = (double)(totalMaterial / grandTotal) * 100, Color = "#4CAF50" }); // Green
            CostStructure.Add(new CostStructureItem { Category = "Tenaga Kerja", Amount = totalLabor, Percentage = (double)(totalLabor / grandTotal) * 100, Color = "#2196F3" }); // Blue
            CostStructure.Add(new CostStructureItem { Category = "Overhead/Ops", Amount = totalOverhead, Percentage = (double)(totalOverhead / grandTotal) * 100, Color = "#FF9800" }); // Orange

            // Cari Top 5 Bahan Paling Boros (Pareto Analysis)
            // Flatten semua item resep, group by nama bahan
            var materialDrivers = recipes.SelectMany(r => r.Items)
                .GroupBy(i => i.Ingredient?.Name ?? "Unknown")
                .Select(g => new
                {
                    Name = g.Key,
                    TotalCost = g.Sum(x => x.CalculatedCost)
                })
                .OrderByDescending(x => x.TotalCost)
                .Take(5)
                .ToList();

            foreach (var driver in materialDrivers)
            {
                TopCostDrivers.Add(new TopCostDriver
                {
                    Name = driver.Name,
                    TotalCostContribution = driver.TotalCost,
                    Percentage = totalMaterial > 0 ? $"{(driver.TotalCost / totalMaterial * 100):F1}% of Material" : "0%"
                });
            }
        }

        // 3. MATRIX BCG (Product Engineering)
        private void CalculateProductMatrix(List<Recipe> recipes)
        {
            StarProductsCount = 0;
            CashCowCount = 0;
            PuzzleCount = 0;
            DogCount = 0;

            // Thresholds (Batas penentuan kuadran)
            // Idealnya dinamis, tapi kita set statis dulu
            double marginThreshold = 40.0; // Margin di atas 40% dianggap tinggi
            int salesThreshold = 50;       // Penjualan > 50 unit dianggap tinggi (simulasi)

            foreach (var r in recipes)
            {
                bool highMargin = r.RealMarginPercent >= marginThreshold;

                // Gunakan EstMonthlySales user, kalau 0 anggap rendah
                bool highVolume = r.EstMonthlySales >= salesThreshold;

                if (highMargin && highVolume) StarProductsCount++;      // ⭐ Star
                else if (!highMargin && highVolume) CashCowCount++;     // 🐮 Cash Cow
                else if (highMargin && !highVolume) PuzzleCount++;      // 🧩 Puzzle (Potensi)
                else DogCount++;                                        // 🐶 Dog (Rugi/Sepi)
            }
        }

        private void ResetDashboard()
        {
            TotalProductionCost = 0;
            GlobalGrossProfitMargin = "0%";
            EfficiencyScore = 100;
            CostStructure.Clear();
            TopCostDrivers.Clear();
            StarProductsCount = 0;
            CashCowCount = 0;
            PuzzleCount = 0;
            DogCount = 0;
        }
    }
}