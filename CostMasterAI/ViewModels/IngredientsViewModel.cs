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
        public List<string> UnitOptions => UnitHelper.CommonUnits;

        // --- STATE & SELECTION ---
        [ObservableProperty] private Ingredient? _selectedIngredient;
        [ObservableProperty] private bool _isEditing;
        [ObservableProperty] private string _searchText = "";

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

        public IngredientsViewModel()
        {
            _dbContext = new AppDbContext();
            _ = LoadDataAsync();
        }

        public IngredientsViewModel(AppDbContext dbContext)
        {
            _dbContext = dbContext;
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
                PerformSearch();
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

        private void PerformSearch()
        {
            Ingredients.Clear();
            var query = _allIngredients.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                query = query.Where(i => i.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var item in query) Ingredients.Add(item);
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

            try
            {
                // Bersihkan tracker untuk mencegah konflik
                _dbContext.ChangeTracker.Clear();

                if (IsEditing && SelectedIngredient != null)
                {
                    // UPDATE EXISTING
                    var existing = await _dbContext.Ingredients.FindAsync(SelectedIngredient.Id);
                    if (existing != null)
                    {
                        existing.Name = InputName;
                        existing.PricePerPackage = price;
                        existing.QuantityPerPackage = qty;
                        existing.Unit = InputUnit;
                        existing.YieldPercent = yield;

                        _dbContext.Ingredients.Update(existing);
                        await _dbContext.SaveChangesAsync();

                        // INTEGRASI: Kirim sinyal update ke seluruh aplikasi
                        WeakReferenceMessenger.Default.Send(new IngredientsChangedMessage("Updated"));
                    }
                }
                else
                {
                    // CREATE NEW
                    var newIng = new Ingredient
                    {
                        Name = InputName,
                        PricePerPackage = price,
                        QuantityPerPackage = qty,
                        Unit = InputUnit,
                        YieldPercent = yield,
                        Category = "General"
                    };
                    _dbContext.Ingredients.Add(newIng);
                    await _dbContext.SaveChangesAsync();

                    // INTEGRASI: Kirim sinyal update ke seluruh aplikasi
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
                    await _dbContext.SaveChangesAsync();

                    // INTEGRASI: Kirim sinyal update ke seluruh aplikasi
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
            IsEditing = false;
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