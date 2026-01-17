using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging; // Wajib untuk integrasi
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CostMasterAI.Core.Models;
using CostMasterAI.Core.Services;
using CostMasterAI.Helpers; // Akses AppMessages
using System.Text.RegularExpressions; // Penting untuk membaca tag harga

namespace CostMasterAI.ViewModels
{
    // Helper Class untuk Chart/Grafik
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

        // ==========================================
        // 1. DATA KEUANGAN (INTEGRASI BARU)
        // ==========================================
        [ObservableProperty] private decimal _currentRevenue;      // Pemasukan Bulan Ini (Uang Masuk Real)
        [ObservableProperty] private decimal _currentExpense;      // Pengeluaran Bulan Ini
        [ObservableProperty] private decimal _currentNetProfit;    // Profit Bulan Ini
        [ObservableProperty] private decimal _cashFlowBalance;     // Saldo Total (Semua Waktu)

        // --- FITUR BARU: Opportunity Loss (Selisih Harga) ---
        [ObservableProperty] private decimal _potentialRevenue; // Pendapatan Seharusnya (Harga Normal)
        [ObservableProperty] private decimal _revenueGap;       // Selisih (Diskon/Promo/Bundling)

        public ObservableCollection<Transaction> RecentTransactions { get; } = new();

        // ==========================================
        // 2. DATA PRODUKSI & RESEP
        // ==========================================
        [ObservableProperty] private int _totalRecipesCount;
        [ObservableProperty] private int _totalIngredientsCount;
        [ObservableProperty] private decimal _totalProductionCost;
        [ObservableProperty] private string _globalGrossProfitMargin = "0%";
        [ObservableProperty] private int _efficiencyScore = 100;
        [ObservableProperty] private string _efficiencyStatus = "Excellent";
        [ObservableProperty] private string _starMenuName = "-";

        // Collections untuk Grafik & Tabel
        public ObservableCollection<CostStructureItem> CostStructure { get; } = new();
        public ObservableCollection<TopCostDriver> TopCostDrivers { get; } = new();

        // Product Matrix Collections
        public ObservableCollection<Recipe> StarMenus { get; } = new();
        public ObservableCollection<Recipe> CashCowMenus { get; } = new();
        public ObservableCollection<Recipe> PuzzleMenus { get; } = new();
        public ObservableCollection<Recipe> DogMenus { get; } = new();

        // Counters
        [ObservableProperty] private int _starProductsCount;
        [ObservableProperty] private int _cashCowCount;
        [ObservableProperty] private int _puzzleCount;
        [ObservableProperty] private int _dogCount;

        // --- STATUS ---
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusMessage = "Ready";

        public DashboardViewModel()
        {
            _dbContext = new AppDbContext();

            // --- INTEGRASI SISTEM PESAN (REAL-TIME UPDATE) ---

            // 1. Dengar Perubahan Transaksi (Update Revenue/Profit/Gap)
            WeakReferenceMessenger.Default.Register<TransactionsChangedMessage>(this, (r, m) =>
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(async () => await LoadDashboardData());
            });

            // 2. Dengar Perubahan Resep (Update Total Produk & Inventory Value)
            WeakReferenceMessenger.Default.Register<RecipesChangedMessage>(this, (r, m) =>
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(async () => await LoadDashboardData());
            });

            // 3. Dengar Perubahan Bahan (Update Total Bahan)
            WeakReferenceMessenger.Default.Register<IngredientsChangedMessage>(this, (r, m) =>
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(async () => await LoadDashboardData());
            });
        }

        // Method ini dipanggil dari DashboardPage.xaml.cs saat halaman dibuka
        [RelayCommand]
        public async Task LoadDashboardData()
        {
            if (IsLoading) return;
            IsLoading = true;
            StatusMessage = "Menganalisa Data...";

            try
            {
                // Pastikan DB Siap
                await _dbContext.Database.EnsureCreatedAsync();

                // ---------------------------------------------------------
                // STEP 1: LOAD DATA KEUANGAN & PARSING SELISIH HARGA
                // ---------------------------------------------------------
                var allTransactions = await _dbContext.Transactions.AsNoTracking().ToListAsync();

                // Hitung Saldo Total (Semua Waktu)
                var totalIncome = allTransactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
                var totalExpense = allTransactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);
                CashFlowBalance = totalIncome - totalExpense;

                // Hitung Data Bulan Ini
                var now = DateTime.Now;
                var startMonth = new DateTime(now.Year, now.Month, 1);
                var nextMonth = startMonth.AddMonths(1);

                var monthTrx = allTransactions.Where(t => t.Date >= startMonth && t.Date < nextMonth).ToList();

                CurrentRevenue = monthTrx.Where(t => t.Type == "Income").Sum(t => t.Amount);
                CurrentExpense = monthTrx.Where(t => t.Type == "Expense").Sum(t => t.Amount);
                CurrentNetProfit = CurrentRevenue - CurrentExpense;

                // --- LOGIC DETEKSI SELISIH HARGA (NEW) ---
                // Kita cari tag [Std:ANGKA] di deskripsi transaksi bulan ini
                // Tag ini dibuat otomatis oleh ReportsViewModel saat menjual produk
                decimal calculatedPotentialRevenue = 0;

                foreach (var trx in monthTrx.Where(t => t.Type == "Income"))
                {
                    // Regex mencari pattern [Std:12345]
                    var match = Regex.Match(trx.Description ?? "", @"\[Std:(\d+)\]");
                    if (match.Success && decimal.TryParse(match.Groups[1].Value, out decimal stdPrice))
                    {
                        // Jika ada tag standar, gunakan harga standar untuk potensi revenue
                        calculatedPotentialRevenue += stdPrice;
                    }
                    else
                    {
                        // Jika tidak ada tag (input manual biasa), asumsi harga standar = harga jual
                        calculatedPotentialRevenue += trx.Amount;
                    }
                }

                PotentialRevenue = calculatedPotentialRevenue;
                RevenueGap = PotentialRevenue - CurrentRevenue; // Selisih (Diskon/Promo)

                // Populate 5 Transaksi Terakhir
                RecentTransactions.Clear();
                foreach (var item in allTransactions.OrderByDescending(t => t.Date).Take(5))
                {
                    RecentTransactions.Add(item);
                }

                // ---------------------------------------------------------
                // STEP 2: LOAD DATA PRODUKSI (RECIPES & INGREDIENTS)
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
                    CalculateExecutiveSummary(recipes);
                    CalculateCostStructure(recipes);
                    CalculateProductMatrix(recipes);
                }
                else
                {
                    ResetProductionStats();
                }

                StatusMessage = $"Update: {DateTime.Now:HH:mm}";
            }
            catch (Exception ex)
            {
                StatusMessage = "Error memuat data.";
                System.Diagnostics.Debug.WriteLine($"DASHBOARD ERROR: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // --- LOGIC PERHITUNGAN PRODUKSI ---

        private void CalculateExecutiveSummary(List<Recipe> recipes)
        {
            // Total HPP Global (Estimasi nilai inventory produk jadi)
            TotalProductionCost = recipes.Sum(r => r.TotalBatchCost);

            // Rata-rata Margin Resep
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

            // Efficiency Score (Base on Cooking Loss)
            double avgCookingLoss = recipes.Any() ? recipes.Average(r => r.CookingLossPercent) : 0;
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

            // Top Cost Drivers (Pareto Analysis)
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

            CostStructure.Clear();
            TopCostDrivers.Clear();
            StarMenus.Clear(); CashCowMenus.Clear(); PuzzleMenus.Clear(); DogMenus.Clear();
            StarProductsCount = 0; CashCowCount = 0; PuzzleCount = 0; DogCount = 0;
        }
    }
}