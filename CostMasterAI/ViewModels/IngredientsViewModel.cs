using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CostMasterAI.Helpers;
using CostMasterAI.Models;
using CostMasterAI.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.ViewManagement;
using WinRT.Interop;
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
        // Menghitung harga per unit dasar secara otomatis saat user mengetik
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

        public IngredientsViewModel(AppDbContext dbContext)
        {
            _dbContext = dbContext;
            LoadDataAsync();
        }

        public async void LoadDataAsync()
        {
            try
            {
                // Load semua bahan, urutkan A-Z
                var data = await _dbContext.Ingredients
                    .AsNoTracking()
                    .OrderBy(i => i.Name)
                    .ToListAsync();

                _allIngredients = data;
                PerformSearch(); // Populate ObservableCollection
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
                        // Kategori bisa ditambahkan nanti jika perlu

                        _dbContext.Ingredients.Update(existing);
                        await _dbContext.SaveChangesAsync();
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
                }

                ResetInput(); // Bersihkan form
                LoadDataAsync(); // Refresh tabel
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
                // Cek Validasi: Jangan hapus jika sedang dipakai di Resep
                bool isUsed = await _dbContext.RecipeItems.AnyAsync(ri => ri.IngredientId == item.Id);
                if (isUsed)
                {
                    // Idealnya tampilkan dialog, tapi untuk sekarang kita skip delete
                    System.Diagnostics.Debug.WriteLine("Gagal Hapus: Bahan sedang digunakan di resep lain.");
                    return;
                }

                _dbContext.Ingredients.Remove(item);
                await _dbContext.SaveChangesAsync();
                LoadDataAsync();

                // Jika yang dihapus sedang diedit, reset form
                if (SelectedIngredient?.Id == item.Id) ResetInput();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error Delete: {ex.Message}");
            }
        }

        [RelayCommand]
        private void EditIngredient(Ingredient? item)
        {
            if (item == null) return;

            SelectedIngredient = item;

            // Populate Form
            InputName = item.Name;
            InputPrice = item.PricePerPackage.ToString("0.##"); // Format bersih tanpa .00
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
            InputUnit = "Gram"; // Default Unit
            InputYield = "100";
            IsEditing = false;
        }

        [RelayCommand]
        private async Task ImportExcelAsync() // Ganti nama command biar sesuai
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.List;
                picker.SuggestedStartLocation = PickerLocationId.Downloads;

                // FILTER HANYA EXCEL
                picker.FileTypeFilter.Add(".xlsx");
                picker.FileTypeFilter.Add(".xls");

                // WinUI 3 Window Handle logic
                // Pastikan App.MainWindow terekspos public static di App.xaml.cs, atau gunakan cara lain untuk dapat window handle
                var window = App.MainWindow;
                var hWnd = WindowNative.GetWindowHandle(window);
                InitializeWithWindow.Initialize(picker, hWnd);

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    // PANGGIL SEEDER EXCEL
                    var seeder = new DataSeeder(_dbContext);
                    await seeder.SeedFromExcelAsync(file);

                    // Refresh Data
                    LoadDataAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Import Error: {ex.Message}");
            }
        }
    }
}