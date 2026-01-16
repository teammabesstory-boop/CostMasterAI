using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using CostMasterAI.Core.Services; // Pastikan namespace ini benar (Core)
using CostMasterAI.Core.Models;   // Pastikan namespace ini benar (Core)

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

        // --- INPUT FORM ---
        [ObservableProperty] private string _inputNote;
        [ObservableProperty] private string _inputAmountText;
        [ObservableProperty] private DateTimeOffset _inputDate = DateTimeOffset.Now;
        [ObservableProperty] private int _selectedTypeIndex = 0; // 0 = Income, 1 = Expense

        // --- DATA ---
        public ObservableCollection<Transaction> Transactions { get; } = new();
        public ObservableCollection<ChartDataPoint> SalesChartData { get; } = new();

        // --- FITUR BARU: PICK FROM INGREDIENTS ---
        public ObservableCollection<Ingredient> IngredientsList { get; } = new();

        public ReportsViewModel()
        {
            _dbContext = new AppDbContext();
            // Load data async (fire and forget di constructor aman untuk viewmodel top-level)
            _ = LoadDataAsync();
        }

        public async Task LoadDataAsync()
        {
            await _dbContext.Database.EnsureCreatedAsync();

            // 1. Load Transaksi
            Transactions.Clear();
            var dbData = await _dbContext.Transactions.OrderByDescending(t => t.Date).ToListAsync();
            foreach (var item in dbData)
            {
                Transactions.Add(item);
            }

            // 2. Load Bahan Baku untuk Picker
            IngredientsList.Clear();
            var ingData = await _dbContext.Ingredients.AsNoTracking().OrderBy(i => i.Name).ToListAsync();
            foreach (var item in ingData)
            {
                IngredientsList.Add(item);
            }

            RecalculateTotals();
        }

        // Command ini dipanggil saat item di list bahan baku diklik
        [RelayCommand]
        private void PickIngredient(Ingredient item)
        {
            if (item == null) return;

            // Otomatis isi Form
            InputNote = $"Belanja {item.Name}"; // Contoh: "Belanja Tepung Terigu"
            InputAmountText = item.PricePerPackage.ToString("F0"); // Harga per kemasan
            SelectedTypeIndex = 1; // Otomatis set ke "Expense" / Pengeluaran
        }

        [RelayCommand]
        private async Task AddTransaction()
        {
            if (string.IsNullOrWhiteSpace(InputNote) || string.IsNullOrWhiteSpace(InputAmountText)) return;
            if (!decimal.TryParse(InputAmountText, out decimal amount)) return;

            var type = SelectedTypeIndex == 0 ? "Income" : "Expense";

            // 1. Buat Object Baru
            var newTrx = new Transaction
            {
                Date = InputDate.Date + DateTime.Now.TimeOfDay,
                Description = InputNote,
                Amount = amount,
                Type = type,
                PaymentMethod = "Manual"
            };

            // 2. Simpan ke Database
            _dbContext.Transactions.Add(newTrx);
            await _dbContext.SaveChangesAsync();

            // 3. Update UI (Insert di index 0 agar muncul di paling atas)
            Transactions.Insert(0, newTrx);

            // Reset Form
            InputNote = string.Empty;
            InputAmountText = string.Empty;
            InputDate = DateTimeOffset.Now;
            // SelectedTypeIndex tidak direset agar user bisa input banyak expense sekaligus

            RecalculateTotals();
        }

        [RelayCommand]
        private async Task DeleteTransaction(Transaction trx)
        {
            if (trx == null) return;

            // Hapus dari Database
            _dbContext.Transactions.Remove(trx);
            await _dbContext.SaveChangesAsync();

            // Hapus dari UI
            Transactions.Remove(trx);
            RecalculateTotals();
        }

        private void RecalculateTotals()
        {
            // Hitung total langsung dari koleksi yang ada di memori
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

            // Logic Chart: Ambil data 7 hari terakhir
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var dailySum = Transactions
                    .Where(t => t.Type == "Income" && t.Date.Date == date)
                    .Sum(t => t.Amount);

                SalesChartData.Add(new ChartDataPoint
                {
                    Label = date.ToString("ddd"), // Format hari (Sen, Sel...)
                    Amount = dailySum,
                    Value = (double)dailySum
                });
            }

            // Normalisasi Tinggi Bar Visual agar proporsional
            if (SalesChartData.Any())
            {
                double maxVal = SalesChartData.Max(x => x.Value);
                if (maxVal > 0)
                {
                    foreach (var item in SalesChartData)
                    {
                        // Scaling visual ke max height 150 pixel
                        item.Value = (item.Value / maxVal) * 150;

                        // Minimum height visual agar bar tetap terlihat sedikit meski nilainya kecil
                        if (item.Value < 5 && item.Amount > 0) item.Value = 5;
                    }
                }
            }
        }

        [RelayCommand]
        private async Task LoadDummyData()
        {
            // Tombol refresh memanggil ulang load data
            await LoadDataAsync();
        }

        [RelayCommand]
        private void ExportReport()
        {
            // Placeholder Export
        }
    }

    // Helper Class untuk Chart
    public class ChartDataPoint
    {
        public string Label { get; set; }
        public double Value { get; set; }
        public decimal Amount { get; set; }
    }
}