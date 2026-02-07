using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging; // Wajib untuk komunikasi antar halaman
using CostMasterAI.Helpers;
using CostMasterAI.Core.Services;
using CostMasterAI.Core.Models;
using CostMasterAI.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace CostMasterAI.ViewModels
{
    public partial class IngredientsViewModel : ObservableObject
    {
        private readonly AppDbContext _dbContext;

        // --- DATA COLLECTIONS ---
        private List<Ingredient> _allIngredients = new(); // Cache untuk pencarian cepat
        public ObservableCollection<Ingredient> Ingredients { get; } = new();
        public ObservableCollection<Ingredient> LowStockIngredients { get; } = new();
        public ObservableCollection<StockTransaction> RecentTransactions { get; } = new();
        public ObservableCollection<string> CategoryOptions { get; } = new();
        public ObservableCollection<string> UnitFilterOptions { get; } = new();
        public List<string> UnitOptions => UnitHelper.CommonUnits;
        public List<string> StockStatusOptions { get; } = new()
        {
            "All",
            "Healthy",
            "Low Stock",
            "Out of Stock"
        };
        public List<string> SortOptions { get; } = new()
        {
            "Name (A-Z)",
            "Name (Z-A)",
            "Cost/Unit (High)",
            "Cost/Unit (Low)",
            "Stock (High)",
            "Stock (Low)"
        };

        // --- STATE & SELECTION ---
        [ObservableProperty] private Ingredient? _selectedIngredient;
        [ObservableProperty] private bool _isEditing;
        [ObservableProperty] private string _searchText = "";
        [ObservableProperty] private string _selectedCategory = "All";
        [ObservableProperty] private string _selectedStockStatus = "All";
        [ObservableProperty] private string _selectedSort = "Name (A-Z)";
        [ObservableProperty] private string _selectedUnitFilter = "All";

        // --- FORM INPUTS ---
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CalculatedCostPerUnit))]
        private string _inputName = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CalculatedCostPerUnit))]
        private string _inputPrice = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CalculatedCostPerUnit))]
        private string _inputQty = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CalculatedCostPerUnit))]
        private string _inputUnit = "Gram";

        [ObservableProperty]
        private string _inputYield = "100";

        // --- NEW: INVENTORY INPUTS ---
        [ObservableProperty] private string _inputStock = "0"; // Stok Fisik
        [ObservableProperty] private string _inputMinStock = "0"; // Alert Limit
        [ObservableProperty] private string _inputAdjustment = "0";
        [ObservableProperty] private string _inputAdjustmentNote = "";

        // --- DASHBOARD INSIGHTS ---
        [ObservableProperty] private string _totalIngredientsLabel = "0";
        [ObservableProperty] private string _inventoryValueLabel = "Rp 0";
        [ObservableProperty] private string _averageCostLabel = "Rp 0";
        [ObservableProperty] private string _lowStockLabel = "0";
        [ObservableProperty] private string _outOfStockLabel = "0";
        [ObservableProperty] private string _stockHealthLabel = "Healthy";
        [ObservableProperty] private string _valueAtRiskLabel = "Rp 0";
        [ObservableProperty] private string _lastSyncLabel = "-";
        [ObservableProperty] private string _selectedIngredientCostLabel = "-";
        [ObservableProperty] private string _selectedIngredientStockValueLabel = "-";
        [ObservableProperty] private string _selectedIngredientStatusLabel = "-";
        [ObservableProperty] private string _selectedIngredientUsageLabel = "-";

        // --- COMPUTED PROPERTY (Real-time Feedback) ---
        public string CalculatedCostPerUnit
        {
            get
            {
                if (decimal.TryParse(InputPrice, out var p) && double.TryParse(InputQty, out var q) && q > 0)
                {
                    decimal cost = p / (decimal)q;
                    return $"Rp {cost:N2} / {InputUnit}";
                }
                return "Rp 0";
            }
        }

        // --- CONSTRUCTOR INJECTION ---
        public IngredientsViewModel(AppDbContext dbContext)
        {
            _dbContext = dbContext;

            // Dengarkan update stok dari halaman lain (Shopping List / Reports)
            WeakReferenceMessenger.Default.Register<IngredientsChangedMessage>(this, (r, m) =>
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

                // Bersihkan tracker sebelum load data fresh
                _dbContext.ChangeTracker.Clear();

                // Load semua bahan, urutkan A-Z
                var data = await _dbContext.Ingredients
                    .AsNoTracking()
                    .OrderBy(i => i.Name)
                    .ToListAsync();

                _allIngredients = data;
                UpdateFilterOptions();
                PerformSearch();
                await UpdateInsightsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error LoadData: {ex.Message}");
            }
        }

        // --- SEARCH LOGIC ---
        partial void OnSearchTextChanged(string value)
        {
            PerformSearch();
        }

        partial void OnSelectedCategoryChanged(string value)
        {
            PerformSearch();
        }

        partial void OnSelectedStockStatusChanged(string value)
        {
            PerformSearch();
        }

        partial void OnSelectedSortChanged(string value)
        {
            PerformSearch();
        }

        partial void OnSelectedUnitFilterChanged(string value)
        {
            PerformSearch();
        }

        partial void OnSelectedIngredientChanged(Ingredient? value)
        {
            UpdateSelectedIngredientInsights();
        }

        private void PerformSearch()
        {
            Ingredients.Clear();
            var query = _allIngredients.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                query = query.Where(i => i.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(SelectedCategory) && SelectedCategory != "All")
            {
                query = query.Where(i => i.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(SelectedUnitFilter) && SelectedUnitFilter != "All")
            {
                query = query.Where(i => i.Unit.Equals(SelectedUnitFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(SelectedStockStatus) && SelectedStockStatus != "All")
            {
                query = query.Where(i => GetStockStatus(i) == SelectedStockStatus);
            }

            query = SelectedSort switch
            {
                "Name (Z-A)" => query.OrderByDescending(i => i.Name),
                "Cost/Unit (High)" => query.OrderByDescending(i => i.RealCostPerUnit),
                "Cost/Unit (Low)" => query.OrderBy(i => i.RealCostPerUnit),
                "Stock (High)" => query.OrderByDescending(i => i.CurrentStock),
                "Stock (Low)" => query.OrderBy(i => i.CurrentStock),
                _ => query.OrderBy(i => i.Name)
            };

            foreach (var item in query) Ingredients.Add(item);
            UpdateSelectedIngredientInsights();
        }

        private void UpdateFilterOptions()
        {
            CategoryOptions.Clear();
            CategoryOptions.Add("All");
            foreach (var category in _allIngredients.Select(i => i.Category).Distinct().OrderBy(c => c))
            {
                CategoryOptions.Add(category);
            }

            UnitFilterOptions.Clear();
            UnitFilterOptions.Add("All");
            foreach (var unit in _allIngredients.Select(i => i.Unit).Distinct().OrderBy(u => u))
            {
                UnitFilterOptions.Add(unit);
            }
        }

        private async Task UpdateInsightsAsync()
        {
            TotalIngredientsLabel = _allIngredients.Count.ToString("N0");

            var totalValue = _allIngredients.Sum(i => i.RealCostPerUnit * (decimal)i.CurrentStock);
            InventoryValueLabel = $"Rp {totalValue:N0}";

            var avgCost = _allIngredients.Any() ? _allIngredients.Average(i => i.RealCostPerUnit) : 0m;
            AverageCostLabel = $"Rp {avgCost:N2}";

            var lowStock = _allIngredients.Count(i => i.MinimumStock > 0 && i.CurrentStock <= i.MinimumStock && i.CurrentStock > 0);
            var outOfStock = _allIngredients.Count(i => i.CurrentStock <= 0);
            LowStockLabel = lowStock.ToString("N0");
            OutOfStockLabel = outOfStock.ToString("N0");

            var totalCount = _allIngredients.Count;
            var riskScore = totalCount == 0 ? 0 : (double)(lowStock + outOfStock) / totalCount;
            StockHealthLabel = riskScore switch
            {
                > 0.4 => "Critical",
                > 0.2 => "Watchlist",
                _ => "Healthy"
            };

            var valueAtRisk = _allIngredients
                .Where(i => i.MinimumStock > 0 && i.CurrentStock < i.MinimumStock)
                .Sum(i => (decimal)Math.Max(0, i.MinimumStock - i.CurrentStock) * i.RealCostPerUnit);
            ValueAtRiskLabel = $"Rp {valueAtRisk:N0}";

            var recent = await _dbContext.StockTransactions
                .AsNoTracking()
                .OrderByDescending(t => t.Date)
                .Take(8)
                .ToListAsync();

            RecentTransactions.Clear();
            foreach (var tx in recent)
            {
                RecentTransactions.Add(tx);
            }

            LowStockIngredients.Clear();
            foreach (var item in _allIngredients
                .Where(i => i.MinimumStock > 0 && i.CurrentStock <= i.MinimumStock)
                .OrderBy(i => i.CurrentStock))
            {
                LowStockIngredients.Add(item);
            }

            LastSyncLabel = DateTime.Now.ToString("dd MMM yyyy HH:mm");
            UpdateSelectedIngredientInsights();
        }

        private string GetStockStatus(Ingredient ingredient)
        {
            if (ingredient.CurrentStock <= 0) return "Out of Stock";
            if (ingredient.MinimumStock > 0 && ingredient.CurrentStock <= ingredient.MinimumStock) return "Low Stock";
            return "Healthy";
        }

        private void UpdateSelectedIngredientInsights()
        {
            if (SelectedIngredient == null)
            {
                SelectedIngredientCostLabel = "-";
                SelectedIngredientStockValueLabel = "-";
                SelectedIngredientStatusLabel = "-";
                SelectedIngredientUsageLabel = "-";
                return;
            }

            SelectedIngredientCostLabel = $"Rp {SelectedIngredient.RealCostPerUnit:N2} / {SelectedIngredient.Unit}";
            var stockValue = SelectedIngredient.RealCostPerUnit * (decimal)SelectedIngredient.CurrentStock;
            SelectedIngredientStockValueLabel = $"Rp {stockValue:N0}";
            SelectedIngredientStatusLabel = GetStockStatus(SelectedIngredient);
            SelectedIngredientUsageLabel = SelectedIngredient.MinimumStock > 0
                ? $"Min {SelectedIngredient.MinimumStock:N0} {SelectedIngredient.Unit}"
                : "Min belum diatur";
        }

        // --- CRUD COMMANDS ---

        [RelayCommand]
        private async Task SaveIngredientAsync()
        {
            // Validasi Input
            if (string.IsNullOrWhiteSpace(InputName)) return;
            if (!decimal.TryParse(InputPrice, out var price)) return;
            if (!double.TryParse(InputQty, out var qty) || qty <= 0) return;
            if (!double.TryParse(InputYield, out var yield)) yield = 100;

            // Parse Stock Inputs
            double.TryParse(InputStock, out var stock);
            double.TryParse(InputMinStock, out var minStock);

            try
            {
                _dbContext.ChangeTracker.Clear();

                if (IsEditing && SelectedIngredient != null)
                {
                    // --- UPDATE EXISTING (STOCK OPNAME LOGIC) ---
                    var existing = await _dbContext.Ingredients.FindAsync(SelectedIngredient.Id);
                    if (existing != null)
                    {
                        // 1. Cek perubahan Stok (Stock Opname)
                        if (Math.Abs(existing.CurrentStock - stock) > 0.001)
                        {
                            double diff = stock - existing.CurrentStock;
                            // Catat Transaksi Adjustment
                            _dbContext.StockTransactions.Add(new StockTransaction
                            {
                                IngredientId = existing.Id,
                                Date = DateTime.Now,
                                Type = "Adjustment",
                                Quantity = Math.Abs(diff),
                                Unit = InputUnit,
                                Description = diff > 0 ? "Stock Opname (Tambah)" : "Stock Opname (Susut)"
                            });
                        }

                        // 2. Update Data Master
                        existing.Name = InputName;
                        existing.PricePerPackage = price;
                        existing.QuantityPerPackage = qty;
                        existing.Unit = InputUnit;
                        existing.YieldPercent = yield;
                        existing.CurrentStock = stock; // Update Stok Baru
                        existing.MinimumStock = minStock;

                        _dbContext.Ingredients.Update(existing);
                        await _dbContext.SaveChangesAsync();

                        WeakReferenceMessenger.Default.Send(new IngredientsChangedMessage("Updated"));
                    }
                }
                else
                {
                    // --- CREATE NEW ---
                    var newIng = new Ingredient
                    {
                        Name = InputName,
                        PricePerPackage = price,
                        QuantityPerPackage = qty,
                        Unit = InputUnit,
                        YieldPercent = yield,
                        Category = "General",
                        CurrentStock = stock,
                        MinimumStock = minStock
                    };
                    _dbContext.Ingredients.Add(newIng);
                    await _dbContext.SaveChangesAsync(); // Save dulu untuk dapat ID

                    // Jika ada stok awal, catat sebagai Inisialisasi
                    if (stock > 0)
                    {
                        _dbContext.StockTransactions.Add(new StockTransaction
                        {
                            IngredientId = newIng.Id,
                            Date = DateTime.Now,
                            Type = "In",
                            Quantity = stock,
                            Unit = InputUnit,
                            Description = "Initial Stock"
                        });
                        await _dbContext.SaveChangesAsync();
                    }

                    WeakReferenceMessenger.Default.Send(new IngredientsChangedMessage("Created"));
                }

                ResetInput(); // Bersihkan form
                await LoadDataAsync(); // Refresh tabel
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error Save: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task DeleteIngredientAsync(Ingredient? item)
        {
            if (item == null) return;

            try
            {
                _dbContext.ChangeTracker.Clear();

                // Cek Validasi: Jangan hapus jika sedang dipakai di Resep
                bool isUsed = await _dbContext.RecipeItems.AnyAsync(ri => ri.IngredientId == item.Id);
                if (isUsed)
                {
                    System.Diagnostics.Debug.WriteLine("Gagal Hapus: Bahan sedang digunakan di resep lain.");
                    return;
                }

                // Hapus dari Database
                var entry = await _dbContext.Ingredients.FindAsync(item.Id);
                if (entry != null)
                {
                    _dbContext.Ingredients.Remove(entry);

                    // Hapus history stok juga agar bersih
                    var history = _dbContext.StockTransactions.Where(x => x.IngredientId == item.Id);
                    _dbContext.StockTransactions.RemoveRange(history);

                    await _dbContext.SaveChangesAsync();

                    WeakReferenceMessenger.Default.Send(new IngredientsChangedMessage("Deleted"));
                }

                // Update UI Collections langsung
                var itemInList = _allIngredients.FirstOrDefault(i => i.Id == item.Id);
                if (itemInList != null) _allIngredients.Remove(itemInList);

                if (Ingredients.Contains(item)) Ingredients.Remove(item);

                if (SelectedIngredient?.Id == item.Id) ResetInput();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error Delete: {ex.Message}");
            }
        }

        [RelayCommand]
        private void PrepareEdit(Ingredient? item)
        {
            if (item == null) return;

            SelectedIngredient = item;

            // Populate Form
            InputName = item.Name;
            InputPrice = item.PricePerPackage.ToString("0.##");
            InputQty = item.QuantityPerPackage.ToString("0.##");
            InputUnit = item.Unit;
            InputYield = item.YieldPercent.ToString("0.##");

            // Populate Stock
            InputStock = item.CurrentStock.ToString("0.##");
            InputMinStock = item.MinimumStock.ToString("0.##");

            IsEditing = true;
        }

        [RelayCommand]
        private void ResetInput()
        {
            SelectedIngredient = null;
            InputName = "";
            InputPrice = "";
            InputQty = "";
            InputUnit = "Gram";
            InputYield = "100";
            InputStock = "0";
            InputMinStock = "0";
            InputAdjustment = "0";
            InputAdjustmentNote = "";
            IsEditing = false;
        }

        [RelayCommand]
        private async Task ApplyStockAdjustmentAsync(string direction)
        {
            if (SelectedIngredient == null) return;
            if (!double.TryParse(InputAdjustment, out var qty) || qty <= 0) return;

            var delta = direction == "IN" ? qty : -qty;

            try
            {
                var existing = await _dbContext.Ingredients.FindAsync(SelectedIngredient.Id);
                if (existing == null) return;

                existing.CurrentStock = Math.Max(0, existing.CurrentStock + delta);
                _dbContext.Ingredients.Update(existing);

                _dbContext.StockTransactions.Add(new StockTransaction
                {
                    IngredientId = existing.Id,
                    Date = DateTime.Now,
                    Type = direction == "IN" ? "In" : "Out",
                    Quantity = qty,
                    Unit = existing.Unit,
                    Description = string.IsNullOrWhiteSpace(InputAdjustmentNote)
                        ? "Manual Adjustment"
                        : InputAdjustmentNote
                });

                await _dbContext.SaveChangesAsync();
                await LoadDataAsync();
                InputAdjustment = "0";
                InputAdjustmentNote = "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Adjustment Error: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task RefreshInsightsAsync()
        {
            await LoadDataAsync();
        }

        [RelayCommand]
        private void ClearFilters()
        {
            SearchText = "";
            SelectedCategory = "All";
            SelectedStockStatus = "All";
            SelectedSort = "Name (A-Z)";
            SelectedUnitFilter = "All";
        }

        [RelayCommand]
        private async Task ImportExcelAsync()
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.List;
                picker.SuggestedStartLocation = PickerLocationId.Downloads;
                picker.FileTypeFilter.Add(".xlsx");
                picker.FileTypeFilter.Add(".xls");

                if (App.MainWindow != null)
                {
                    var hWnd = WindowNative.GetWindowHandle(App.MainWindow);
                    InitializeWithWindow.Initialize(picker, hWnd);
                }

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    var seeder = new DataSeeder(_dbContext);
                    await seeder.SeedFromExcelAsync(file);

                    await LoadDataAsync();

                    // INTEGRASI: Kabari sistem bahwa data banyak berubah
                    WeakReferenceMessenger.Default.Send(new IngredientsChangedMessage("Imported"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Import Error: {ex.Message}");
            }
        }
    }
}
