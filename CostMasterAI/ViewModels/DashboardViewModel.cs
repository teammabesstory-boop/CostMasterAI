using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CostMasterAI.Core.Services;
using CostMasterAI.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CostMasterAI.Core.Models;
using System.Text.RegularExpressions;

// --- LIVECHARTS 2 IMPORTS ---
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace CostMasterAI.ViewModels
{
    // Helper class untuk Pareto List (Top Cost Driver)
    public class TopCostDriver
    {
        public string Name { get; set; } = string.Empty;
        public decimal TotalCostContribution { get; set; }
        public string Percentage { get; set; } = "0%";
    }

    public partial class DashboardViewModel : ObservableObject
    {
        private readonly AppDbContext _dbContext;

        // ==========================================
        // 1. DATA KEUANGAN
        // ==========================================
        [ObservableProperty] private decimal _currentRevenue;
        [ObservableProperty] private decimal _currentExpense;
        [ObservableProperty] private decimal _currentNetProfit;
        [ObservableProperty] private decimal _cashFlowBalance;

        // Opportunity Loss
        [ObservableProperty] private decimal _potentialRevenue;
        [ObservableProperty] private decimal _revenueGap;

        // ==========================================
        // 2. LIVECHARTS PROPERTIES (VISUALISASI)
        // ==========================================
        // Menggantikan ObservableCollection manual dengan ISeries array untuk grafik
        [ObservableProperty] private ISeries[] _costStructureSeries;

        // ==========================================
        // 3. DATA PRODUKSI & METRICS
        // ==========================================
        [ObservableProperty] private int _totalRecipesCount;
        [ObservableProperty] private int _totalIngredientsCount;
        [ObservableProperty] private decimal _totalProductionCost; // Inventory Value
        [ObservableProperty] private string _globalGrossProfitMargin = "0%";
        [ObservableProperty] private int _efficiencyScore = 100;
        [ObservableProperty] private string _efficiencyStatus = "Ready";
        [ObservableProperty] private string _starMenuName = "-";

        // Pareto Items (Masih pakai List karena ditampilkan di Tabel)
        public ObservableCollection<TopCostDriver> TopCostDrivers { get; } = new();

        // BCG Matrix Counters
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

            // Inisialisasi Chart Kosong agar tidak null di XAML
            CostStructureSeries = Array.Empty<ISeries>();

            // --- INTEGRASI REAL-TIME ---
            WeakReferenceMessenger.Default.Register<TransactionsChangedMessage>(this, (r, m) =>
                App.MainWindow.DispatcherQueue.TryEnqueue(async () => await LoadDashboardData()));

            WeakReferenceMessenger.Default.Register<RecipesChangedMessage>(this, (r, m) =>
                App.MainWindow.DispatcherQueue.TryEnqueue(async () => await LoadDashboardData()));

            WeakReferenceMessenger.Default.Register<IngredientsChangedMessage>(this, (r, m) =>
                App.MainWindow.DispatcherQueue.TryEnqueue(async () => await LoadDashboardData()));

            _ = LoadDashboardData();
        }

        [RelayCommand]
        public async Task LoadDashboardData()
        {
            if (IsLoading) return;
            IsLoading = true;
            StatusMessage = "Menganalisa Data...";

            try
            {
                await _dbContext.Database.EnsureCreatedAsync();

                // ---------------------------------------------------------
                // STEP 1: LOAD DATA KEUANGAN
                // ---------------------------------------------------------
                var allTransactions = await _dbContext.Transactions.AsNoTracking().ToListAsync();

                // Saldo Kas Total
                var totalIncome = allTransactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
                var totalExpense = allTransactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);
                CashFlowBalance = totalIncome - totalExpense;

                // Data Bulan Ini
                var now = DateTime.Now;
                var startMonth = new DateTime(now.Year, now.Month, 1);
                var nextMonth = startMonth.AddMonths(1);
                var monthTrx = allTransactions.Where(t => t.Date >= startMonth && t.Date < nextMonth).ToList();

                CurrentRevenue = monthTrx.Where(t => t.Type == "Income").Sum(t => t.Amount);
                CurrentExpense = monthTrx.Where(t => t.Type == "Expense").Sum(t => t.Amount);
                CurrentNetProfit = CurrentRevenue - CurrentExpense;

                // Analisa Opportunity Loss (Selisih Harga Standar vs Aktual)
                decimal calculatedPotentialRevenue = 0;
                foreach (var trx in monthTrx.Where(t => t.Type == "Income"))
                {
                    var match = Regex.Match(trx.Description ?? "", @"\[Std:(\d+)\]");
                    if (match.Success && decimal.TryParse(match.Groups[1].Value, out decimal stdPrice))
                    {
                        calculatedPotentialRevenue += stdPrice;
                    }
                    else
                    {
                        calculatedPotentialRevenue += trx.Amount;
                    }
                }
                PotentialRevenue = calculatedPotentialRevenue;
                RevenueGap = PotentialRevenue - CurrentRevenue;

                // ---------------------------------------------------------
                // STEP 2: LOAD DATA PRODUKSI & ANALISA STRUKTUR BIAYA
                // ---------------------------------------------------------
                var recipes = await _dbContext.Recipes
                    .AsNoTracking()
                    .Include(r => r.Items).ThenInclude(i => i.Ingredient)
                    .Include(r => r.Overheads)
                    .ToListAsync();

                TotalRecipesCount = recipes.Count;
                TotalIngredientsCount = await _dbContext.Ingredients.CountAsync();

                if (recipes.Any())
                {
                    // --- A. COST STRUCTURE CHART (PIE CHART) ---
                    // Menghitung total komposisi biaya dari seluruh resep aktif
                    decimal totalDough = 0, totalTopping = 0, totalPack = 0, totalOverhead = 0;

                    foreach (var r in recipes)
                    {
                        totalDough += r.Items.Where(i => i.UsageCategory == "Main").Sum(i => i.CalculatedCost);
                        totalTopping += r.Items.Where(i => i.UsageCategory == "Support").Sum(i => i.CalculatedCost);
                        totalPack += r.Items.Where(i => i.UsageCategory == "Packaging").Sum(i => i.CalculatedCost);
                        totalOverhead += (r.TotalOverheadCost + r.TotalLaborCost);
                    }

                    // Konfigurasi LiveCharts Series
                    CostStructureSeries = new ISeries[]
                    {
                        new PieSeries<decimal>
                        {
                            Values = new decimal[] { totalDough },
                            Name = "Bahan Utama",
                            InnerRadius = 60,
                            Fill = new SolidColorPaint(SKColors.DodgerBlue),
                            DataLabelsSize = 12,
                            DataLabelsPaint = new SolidColorPaint(SKColors.White)
                        },
                        new PieSeries<decimal>
                        {
                            Values = new decimal[] { totalTopping },
                            Name = "Topping",
                            InnerRadius = 60,
                            Fill = new SolidColorPaint(SKColors.Orange)
                        },
                        new PieSeries<decimal>
                        {
                            Values = new decimal[] { totalPack },
                            Name = "Kemasan",
                            InnerRadius = 60,
                            Fill = new SolidColorPaint(SKColors.MediumPurple)
                        },
                        new PieSeries<decimal>
                        {
                            Values = new decimal[] { totalOverhead },
                            Name = "Overhead",
                            InnerRadius = 60,
                            Fill = new SolidColorPaint(SKColors.Crimson)
                        }
                    };

                    // --- B. PARETO (TOP COST DRIVERS) ---
                    TopCostDrivers.Clear();
                    decimal grandTotalMaterial = totalDough + totalTopping + totalPack;

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
                            Percentage = grandTotalMaterial > 0 ? $"{(driver.TotalCost / grandTotalMaterial * 100):F1}%" : "0%"
                        });
                    }

                    // --- C. EXECUTIVE METRICS ---
                    CalculateExecutiveSummary(recipes);
                    CalculateProductMatrix(recipes);
                }
                else
                {
                    ResetProductionStats();
                }

                StatusMessage = $"Updated: {DateTime.Now:HH:mm}";
            }
            catch (Exception ex)
            {
                StatusMessage = "Error loading data.";
                System.Diagnostics.Debug.WriteLine($"DASHBOARD ERROR: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void CalculateExecutiveSummary(List<Recipe> recipes)
        {
            // Inventory Value (Nilai Stok Bahan di Gudang)
            // Note: Idealnya ambil dari Ingredient.CurrentStock, tapi di sini kita pakai estimasi produksi dulu
            // Jika mau real inventory value:
            // var ingredients = await _dbContext.Ingredients.ToListAsync();
            // TotalProductionCost = ingredients.Sum(i => (decimal)i.CurrentStock * i.PricePerUnit);

            // Untuk sementara pakai Total Batch Cost dari resep sebagai indikator aset produksi
            TotalProductionCost = recipes.Sum(r => r.TotalBatchCost);

            // Gross Profit Margin Global
            var validRecipes = recipes.Where(r => r.ActualSellingPrice > 0).ToList();
            if (validRecipes.Any())
            {
                double avgMargin = validRecipes.Average(r =>
                {
                    if (r.ActualSellingPrice == 0) return 0;
                    return (double)((r.ActualSellingPrice - r.CostPerUnit) / r.ActualSellingPrice * 100);
                });
                GlobalGrossProfitMargin = $"{avgMargin:F1}%";
            }
            else
            {
                GlobalGrossProfitMargin = "0%";
            }

            // Efficiency Score (based on Cooking Loss)
            double avgCookingLoss = recipes.Average(r => r.CookingLossPercent);
            double score = 100 - (avgCookingLoss * 2.0);
            EfficiencyScore = (int)Math.Max(0, Math.Min(100, score));

            if (EfficiencyScore >= 90) EfficiencyStatus = "Excellent ✨";
            else if (EfficiencyScore >= 75) EfficiencyStatus = "Good ✅";
            else if (EfficiencyScore >= 60) EfficiencyStatus = "Warning ⚠️";
            else EfficiencyStatus = "Critical 🚨";
        }

        private void CalculateProductMatrix(List<Recipe> recipes)
        {
            StarProductsCount = 0; CashCowCount = 0; PuzzleCount = 0; DogCount = 0;

            if (!recipes.Any()) return;

            double avgVolume = recipes.Average(r => r.YieldQty);
            double targetMargin = 40.0;
            Recipe? bestPerformer = null;
            decimal maxProfitVal = -1;

            foreach (var r in recipes)
            {
                double margin = 0;
                if (r.ActualSellingPrice > 0)
                    margin = (double)((r.ActualSellingPrice - r.CostPerUnit) / r.ActualSellingPrice * 100);

                bool isHighMargin = margin >= targetMargin;
                bool isHighVolume = r.YieldQty >= avgVolume;

                if (isHighMargin && isHighVolume) StarProductsCount++;
                else if (!isHighMargin && isHighVolume) CashCowCount++;
                else if (isHighMargin && !isHighVolume) PuzzleCount++;
                else DogCount++;

                // Cari Star Menu (Profit Terbesar)
                decimal estimatedProfit = (r.ActualSellingPrice - r.CostPerUnit) * (decimal)r.YieldQty;
                if (estimatedProfit > maxProfitVal)
                {
                    maxProfitVal = estimatedProfit;
                    bestPerformer = r;
                }
            }

            StarMenuName = bestPerformer?.Name ?? "-";
        }

        private void ResetProductionStats()
        {
            TotalProductionCost = 0;
            GlobalGrossProfitMargin = "0%";
            EfficiencyScore = 100;
            EfficiencyStatus = "No Data";
            StarMenuName = "-";
            CostStructureSeries = Array.Empty<ISeries>();
            TopCostDrivers.Clear();
            StarProductsCount = 0; CashCowCount = 0; PuzzleCount = 0; DogCount = 0;
        }
    }
}