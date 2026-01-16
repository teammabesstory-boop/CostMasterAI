using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;

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
        [ObservableProperty] private int _selectedTypeIndex = 0;

        // --- DATA ---
        public ObservableCollection<Transaction> Transactions { get; } = new();
        public ObservableCollection<ChartDataPoint> SalesChartData { get; } = new();

        public ReportsViewModel()
        {
            _dbContext = new AppDbContext();
            // Load data dari database saat aplikasi dibuka
            LoadDataFromDb();
        }

        private void LoadDataFromDb()
        {
            // Pastikan database dan tabel ada
            _dbContext.Database.EnsureCreated();

            Transactions.Clear();
            // Ambil dari DB, urutkan dari yang terbaru
            var dbData = _dbContext.Transactions.OrderByDescending(t => t.Date).ToList();

            foreach (var item in dbData)
            {
                Transactions.Add(item);
            }

            RecalculateTotals();
        }

        [RelayCommand]
        private void AddTransaction()
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
            _dbContext.SaveChanges();

            // 3. Update UI (Insert di index 0 agar muncul di paling atas)
            Transactions.Insert(0, newTrx);

            // Reset Form
            InputNote = string.Empty;
            InputAmountText = string.Empty;
            InputDate = DateTimeOffset.Now;

            RecalculateTotals();
        }

        [RelayCommand]
        private void DeleteTransaction(Transaction trx)
        {
            if (trx == null) return;

            // Hapus dari Database
            _dbContext.Transactions.Remove(trx);
            _dbContext.SaveChanges();

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
        private void LoadDummyData()
        {
            // Method kosong untuk mencegah error binding di XAML jika tombol Filter ditekan
            // (Nanti bisa diisi logic filter tanggal database)
            LoadDataFromDb();
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