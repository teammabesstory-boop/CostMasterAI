using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input; // Wajib untuk RelayCommand
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace CostMasterAI.ViewModels
{
    // Helper Class buat Chart Data
    public class CostStructureItem
    {
        public string Category { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public double Percentage { get; set; }
        public string Color { get; set; } = "#CCCCCC";
    }

    public class TopCostDriver
    {
        public string Name { get; set; } = string.Empty;
        public decimal TotalCostContribution { get; set; }
        public string Percentage { get; set; } = "0%";
    }

    public partial class DashboardViewModel : ObservableObject
    {
        private readonly AppDbContext _dbContext;

        // --- 1. EXECUTIVE SUMMARY (KPIs) ---
        [ObservableProperty] private int _totalRecipesCount;
        [ObservableProperty] private decimal _totalProductionCost;
        [ObservableProperty] private string _globalGrossProfitMargin = "0%";
        [ObservableProperty] private int _efficiencyScore = 100;
        [ObservableProperty] private string _efficiencyStatus = "Excellent";

        // UI Helper Properties
        [ObservableProperty] private string _starMenuName = "-";
        [ObservableProperty] private string _lowStockAlertText = "Stok Aman";

        // --- 2. COST STRUCTURE ---
        public ObservableCollection<CostStructureItem> CostStructure { get; } = new();
        public ObservableCollection<TopCostDriver> TopCostDrivers { get; } = new();

        // --- 3. PRODUCT MATRIX (DataGrid Source) ---
        public ObservableCollection<Recipe> StarMenus { get; } = new();
        public ObservableCollection<Recipe> CashCowMenus { get; } = new();
        public ObservableCollection<Recipe> PuzzleMenus { get; } = new();
        public ObservableCollection<Recipe> DogMenus { get; } = new();

        // Counts
        [ObservableProperty] private int _starProductsCount;
        [ObservableProperty] private int _cashCowCount;
        [ObservableProperty] private int _puzzleCount;
        [ObservableProperty] private int _dogCount;

        // --- STATUS ---
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusMessage = "Ready";

        public DashboardViewModel(AppDbContext dbContext)
        {
            _dbContext = dbContext;
            // Panggil async method tanpa await di constructor (Fire-and-forget aman untuk init)
            _ = RefreshDashboardAsync();
        }

        // --- FIX: GANTI NAMA & TAMBAH RELAY COMMAND ---
        // [RelayCommand] akan men-generate 'RefreshDashboardCommand' untuk XAML
        // Method public ini bisa dipanggil manual oleh Code-Behind
        [RelayCommand]
        public async Task RefreshDashboardAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            StatusMessage = "Menganalisa Data Bisnis...";

            try
            {
                // 1. Fetch Data Lengkap
                var recipes = await _dbContext.Recipes
                    .AsNoTracking()
                    .Include(r => r.Items).ThenInclude(i => i.Ingredient)
                    .Include(r => r.Overheads)
                    .ToListAsync();

                TotalRecipesCount = recipes.Count;

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

                StatusMessage = $"Data Terupdate: {DateTime.Now:HH:mm}";
            }
            catch (Exception ex)
            {
                StatusMessage = "Gagal memuat data dashboard.";
                System.Diagnostics.Debug.WriteLine($"DASHBOARD ERROR: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // --- LOGIC PERHITUNGAN ---

        private void CalculateExecutiveSummary(List<Recipe> recipes)
        {
            // Total HPP Global
            TotalProductionCost = recipes.Sum(r => r.TotalBatchCost);

            // Rata-rata Margin
            var validRecipes = recipes.Where(r => r.DineInPrice > 0).ToList();
            if (validRecipes.Any())
            {
                double avgMargin = validRecipes.Average(r => r.RealMarginPercent);
                GlobalGrossProfitMargin = $"{avgMargin:F1}%";
            }
            else
            {
                GlobalGrossProfitMargin = "0%";
            }

            // Efficiency Score
            double avgCookingLoss = recipes.Average(r => r.CookingLossPercent);
            double score = 100 - (avgCookingLoss * 2.0);
            score = Math.Max(0, Math.Min(100, score));
            EfficiencyScore = (int)score;

            if (EfficiencyScore >= 90) EfficiencyStatus = "World Class 🏆";
            else if (EfficiencyScore >= 75) EfficiencyStatus = "Good ✅";
            else if (EfficiencyScore >= 60) EfficiencyStatus = "Warning ⚠️";
            else EfficiencyStatus = "Critical 🚨";
        }

        private void CalculateCostStructure(List<Recipe> recipes)
        {
            CostStructure.Clear();
            TopCostDrivers.Clear();

            decimal totalMaterial = recipes.Sum(r => r.TotalMaterialCost);
            decimal totalLabor = recipes.Sum(r => r.TotalLaborCost);
            decimal totalOverhead = recipes.Sum(r => r.TotalOverheadCost);
            decimal grandTotal = totalMaterial + totalLabor + totalOverhead;

            if (grandTotal == 0) return;

            // Chart Data
            CostStructure.Add(new CostStructureItem { Category = "Bahan Baku", Amount = totalMaterial, Percentage = (double)(totalMaterial / grandTotal) * 100, Color = "#10B981" });
            CostStructure.Add(new CostStructureItem { Category = "Tenaga Kerja", Amount = totalLabor, Percentage = (double)(totalLabor / grandTotal) * 100, Color = "#3B82F6" });
            CostStructure.Add(new CostStructureItem { Category = "Overhead", Amount = totalOverhead, Percentage = (double)(totalOverhead / grandTotal) * 100, Color = "#F59E0B" });

            // Top Cost Drivers (Pareto)
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
                    Percentage = totalMaterial > 0 ? $"{(driver.TotalCost / totalMaterial * 100):F1}%" : "0%"
                });
            }
        }

        private void CalculateProductMatrix(List<Recipe> recipes)
        {
            StarMenus.Clear(); CashCowMenus.Clear(); PuzzleMenus.Clear(); DogMenus.Clear();
            StarProductsCount = 0; CashCowCount = 0; PuzzleCount = 0; DogCount = 0;

            double avgSales = recipes.Average(r => r.EstMonthlySales);
            if (avgSales < 10) avgSales = 10;

            double targetMargin = 40.0;

            Recipe? bestPerformer = null;
            decimal maxProfitVal = -1;

            foreach (var r in recipes)
            {
                bool isHighMargin = r.RealMarginPercent >= targetMargin;
                bool isHighVolume = r.EstMonthlySales >= avgSales;

                if (isHighMargin && isHighVolume)
                {
                    StarMenus.Add(r);
                    StarProductsCount++;
                }
                else if (!isHighMargin && isHighVolume)
                {
                    CashCowMenus.Add(r);
                    CashCowCount++;
                }
                else if (isHighMargin && !isHighVolume)
                {
                    PuzzleMenus.Add(r);
                    PuzzleCount++;
                }
                else
                {
                    DogMenus.Add(r);
                    DogCount++;
                }

                // Cari Top Performer
                decimal totalProfit = (r.DineInPrice - r.CostPerUnit) * r.EstMonthlySales;
                if (totalProfit > maxProfitVal)
                {
                    maxProfitVal = totalProfit;
                    bestPerformer = r;
                }
            }

            StarMenuName = bestPerformer?.Name ?? "-";
        }

        private void ResetDashboard()
        {
            TotalProductionCost = 0;
            GlobalGrossProfitMargin = "0%";
            EfficiencyScore = 100;
            EfficiencyStatus = "No Data";
            StarMenuName = "-";

            CostStructure.Clear();
            TopCostDrivers.Clear();
            StarMenus.Clear(); CashCowMenus.Clear(); PuzzleMenus.Clear(); DogMenus.Clear();
            StarProductsCount = 0; CashCowCount = 0; PuzzleCount = 0; DogCount = 0;
        }
    }
}