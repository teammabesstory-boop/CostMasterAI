using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using CostMasterAI.Core.Services;
using CostMasterAI.Core.Models;
using CostMasterAI.Helpers;

namespace CostMasterAI.ViewModels
{
    public partial class ReportsViewModel : ObservableObject
    {
        private readonly AppDbContext _dbContext;

        // --- RINGKASAN ---
        [ObservableProperty] private decimal _totalRevenue;
        [ObservableProperty] private decimal _totalExpense;
        [ObservableProperty] private decimal _netProfit;
        [ObservableProperty] private double _profitMarginPercent;

        // --- INPUT FORM (MANUAL) ---
        [ObservableProperty] private string _inputNote;
        [ObservableProperty] private string _inputAmountText;
        [ObservableProperty] private DateTimeOffset _inputDate = DateTimeOffset.Now;
        [ObservableProperty] private int _selectedTypeIndex = 0;

        // --- SALES INPUT (PRODUK) ---
        [ObservableProperty] private Recipe? _selectedProductToSell;
        [ObservableProperty] private string _salesQty = "1";
        [ObservableProperty] private string _salesActualPrice = "0"; // Harga Deal (Uang Masuk)
        [ObservableProperty] private string _salesStandardTotal = "0"; // Harga Teori (HPP x Margin atau Harga Jual Std)
        [ObservableProperty] private string _salesVariance = "0"; // Selisih

        // --- DATA ---
        public ObservableCollection<Transaction> Transactions { get; } = new();
        public ObservableCollection<ChartDataPoint> SalesChartData { get; } = new();
        public ObservableCollection<Ingredient> IngredientsList { get; } = new();

        // List Produk untuk Dijual
        public ObservableCollection<Recipe> ProductList { get; } = new();

        // --- CONSTRUCTOR INJECTION ---
        public ReportsViewModel(AppDbContext dbContext)
        {
            _dbContext = dbContext;

            // Integrasi: Dengarkan jika ada transaksi baru dari halaman lain (misal: Shopping List)
            WeakReferenceMessenger.Default.Register<TransactionsChangedMessage>(this, (r, m) =>
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(async () => await LoadDataAsync());
            });

            _ = LoadDataAsync();
        }

        public async Task LoadDataAsync()
        {
            try
            {
                await _dbContext.Database.EnsureCreatedAsync();

                // Load Transaksi
                Transactions.Clear();
                var dbData = await _dbContext.Transactions.OrderByDescending(t => t.Date).ToListAsync();
                foreach (var item in dbData) Transactions.Add(item);

                // Load Bahan (Picker)
                IngredientsList.Clear();
                var ingData = await _dbContext.Ingredients.AsNoTracking().OrderBy(i => i.Name).ToListAsync();
                foreach (var item in ingData) IngredientsList.Add(item);

                // Load Produk (Picker Penjualan)
                ProductList.Clear();
                var prodData = await _dbContext.Recipes.AsNoTracking().OrderBy(r => r.Name).ToListAsync();
                foreach (var item in prodData) ProductList.Add(item);

                RecalculateTotals();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error LoadData: {ex.Message}");
            }
        }

        // --- LOGIC SALES PRODUCT ---

        partial void OnSelectedProductToSellChanged(Recipe? value)
        {
            // Reset harga saat ganti produk
            if (value != null)
            {
                SalesActualPrice = "0";
                CalculateSalesLogic();
            }
        }

        partial void OnSalesQtyChanged(string value) => CalculateSalesLogic();

        partial void OnSalesActualPriceChanged(string value)
        {
            // Hitung variance setiap harga aktual diketik manual
            if (decimal.TryParse(value, out decimal actual) && decimal.TryParse(SalesStandardTotal, out decimal std))
            {
                SalesVariance = (std - actual).ToString("N0");
            }
        }

        private void CalculateSalesLogic()
        {
            if (SelectedProductToSell != null && int.TryParse(SalesQty, out int qty) && qty > 0)
            {
                // Harga Standar (Teori) = Harga Jual di Resep x Jumlah
                decimal stdTotal = SelectedProductToSell.ActualSellingPrice * qty;
                SalesStandardTotal = stdTotal.ToString("F0");

                // Jika Harga Aktual masih 0 atau kosong, isi otomatis dengan Harga Standar
                // Jika user sudah mengetik harga deal (misal 25000), jangan ditimpa
                if (!decimal.TryParse(SalesActualPrice, out decimal currentActual) || currentActual == 0)
                {
                    SalesActualPrice = stdTotal.ToString("F0");
                }
                else
                {
                    // Trigger ulang perhitungan variance
                    OnSalesActualPriceChanged(SalesActualPrice);
                }
            }
        }

        // --- COMMANDS ---

        [RelayCommand]
        private void PickIngredient(Ingredient item)
        {
            if (item == null) return;
            InputNote = $"Belanja {item.Name}";
            InputAmountText = item.PricePerPackage.ToString("F0");
            SelectedTypeIndex = 1;
        }

        // --- CORE LOGIC UPDATE: REAL INVENTORY DEDUCTION ---
        [RelayCommand]
        private async Task SellProductAsync()
        {
            // Validasi Ketat
            if (SelectedProductToSell == null) return;
            if (!int.TryParse(SalesQty, out int qty) || qty <= 0) return;

            // Coba parsing harga, handle culture indonesia/inggris
            if (!decimal.TryParse(SalesActualPrice, out decimal actualPrice)) return;

            if (!decimal.TryParse(SalesStandardTotal, out decimal stdPrice)) stdPrice = actualPrice;

            // Generate Deskripsi dengan Metadata Tagging [Std:XXX]
            string varianceTag = "";
            if (stdPrice != actualPrice)
            {
                varianceTag = $" [Std:{stdPrice:N0}]";
            }

            try
            {
                // 1. Simpan Transaksi Keuangan (Pemasukan)
                var newTrx = new Transaction
                {
                    Date = DateTime.Now,
                    Description = $"Jual {qty}x {SelectedProductToSell.Name}{varianceTag}",
                    Amount = actualPrice,
                    Type = "Income", // Penjualan selalu Income
                    PaymentMethod = "Cash"
                };
                _dbContext.Transactions.Add(newTrx);

                // 2. RECIPE EXPLOSION (POTONG STOK)
                // Kita perlu load resep lengkap dari DB untuk memastikan kita punya item bahan terbaru
                // dan objek Ingredients yang ter-attach ke context agar update stok tersimpan.
                var recipeWithItems = await _dbContext.Recipes
                    .Include(r => r.Items)
                    .ThenInclude(ri => ri.Ingredient)
                    .FirstOrDefaultAsync(r => r.Id == SelectedProductToSell.Id);

                if (recipeWithItems != null)
                {
                    foreach (var item in recipeWithItems.Items)
                    {
                        if (item.Ingredient != null)
                        {
                            // Hitung pemakaian bahan
                            double usagePerUnit = 0;

                            if (item.IsPerPiece)
                            {
                                // Jika bahan dihitung per pcs (misal: Glaze, Topping per donat)
                                usagePerUnit = item.UsageQty;
                            }
                            else
                            {
                                // Jika bahan adalah adonan (Batch), bagi dengan yield resep
                                // Misal: 1 Batch (Yield 10 pcs) butuh 1000g tepung -> 1 pcs butuh 100g.
                                double yield = recipeWithItems.YieldQty > 0 ? recipeWithItems.YieldQty : 1;
                                usagePerUnit = item.UsageQty / yield;
                            }

                            double totalUsage = usagePerUnit * qty;

                            // Kurangi Stok Fisik
                            item.Ingredient.CurrentStock -= totalUsage;
                            _dbContext.Ingredients.Update(item.Ingredient);

                            // Catat Log Stok Keluar (Stock Transaction)
                            var stockLog = new StockTransaction
                            {
                                IngredientId = item.Ingredient.Id,
                                Date = DateTime.Now,
                                Type = "Out", // Barang Keluar
                                Quantity = totalUsage,
                                Unit = item.UsageUnit,
                                Description = $"Terjual: {qty}x {recipeWithItems.Name}",
                                ReferenceId = "" // Bisa diisi ID Transaksi jika mau relasi kuat
                            };
                            _dbContext.StockTransactions.Add(stockLog);
                        }
                    }
                }

                // Simpan semua perubahan (Keuangan + Stok) ke database
                await _dbContext.SaveChangesAsync();

                // 3. Update UI
                Transactions.Insert(0, newTrx);
                RecalculateTotals();

                // Reset Form
                SelectedProductToSell = null;
                SalesQty = "1";
                SalesActualPrice = "0";
                SalesStandardTotal = "0";
                SalesVariance = "0";

                // 4. Kabari Sistem Lain
                WeakReferenceMessenger.Default.Send(new TransactionsChangedMessage("NewTransaction"));
                WeakReferenceMessenger.Default.Send(new IngredientsChangedMessage("StockDeducted")); // Kabari halaman Bahan Baku
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error Jual: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task AddTransaction()
        {
            if (string.IsNullOrWhiteSpace(InputNote) || string.IsNullOrWhiteSpace(InputAmountText)) return;
            if (!decimal.TryParse(InputAmountText, out decimal amount)) return;

            var type = SelectedTypeIndex == 0 ? "Income" : "Expense";

            var newTrx = new Transaction
            {
                Date = InputDate.Date + DateTime.Now.TimeOfDay,
                Description = InputNote,
                Amount = amount,
                Type = type,
                PaymentMethod = "Manual"
            };

            _dbContext.Transactions.Add(newTrx);
            await _dbContext.SaveChangesAsync();
            Transactions.Insert(0, newTrx);

            InputNote = string.Empty;
            InputAmountText = string.Empty;
            InputDate = DateTimeOffset.Now;

            RecalculateTotals();
            WeakReferenceMessenger.Default.Send(new TransactionsChangedMessage("NewTransaction"));
        }

        [RelayCommand]
        private async Task DeleteTransaction(Transaction trx)
        {
            if (trx == null) return;
            _dbContext.Transactions.Remove(trx);
            await _dbContext.SaveChangesAsync();
            Transactions.Remove(trx);
            RecalculateTotals();
            WeakReferenceMessenger.Default.Send(new TransactionsChangedMessage("DeletedTransaction"));
        }

        private void RecalculateTotals()
        {
            TotalRevenue = Transactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
            TotalExpense = Transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);
            NetProfit = TotalRevenue - TotalExpense;

            if (TotalRevenue > 0)
                ProfitMarginPercent = (double)(NetProfit / TotalRevenue * 100);
            else
                ProfitMarginPercent = 0;

            UpdateChart();
        }

        private void UpdateChart()
        {
            SalesChartData.Clear();
            var endDate = DateTime.Now.Date;
            var startDate = endDate.AddDays(-6);

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var dailySum = Transactions
                    .Where(t => t.Type == "Income" && t.Date.Date == date)
                    .Sum(t => t.Amount);

                SalesChartData.Add(new ChartDataPoint
                {
                    Label = date.ToString("ddd"),
                    Amount = dailySum,
                    Value = (double)dailySum
                });
            }

            if (SalesChartData.Any())
            {
                double maxVal = SalesChartData.Max(x => x.Value);
                if (maxVal > 0)
                {
                    foreach (var item in SalesChartData)
                    {
                        item.Value = (item.Value / maxVal) * 150;
                        if (item.Value < 5 && item.Amount > 0) item.Value = 5;
                    }
                }
            }
        }

        [RelayCommand]
        private async Task LoadDummyData() => await LoadDataAsync();

        [RelayCommand]
        private void ExportReport() { }
    }

    public class ChartDataPoint
    {
        public string Label { get; set; }
        public double Value { get; set; }
        public decimal Amount { get; set; }
    }
}