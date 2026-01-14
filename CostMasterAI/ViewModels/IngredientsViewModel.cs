using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CostMasterAI.Models;
using CostMasterAI.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace CostMasterAI.ViewModels
{
    public partial class IngredientsViewModel : ObservableObject
    {
        private readonly AppDbContext _dbContext;

        public ObservableCollection<Ingredient> Ingredients { get; } = new();

        // Property Form
        [ObservableProperty] private string _newName = string.Empty;
        [ObservableProperty] private string _newPrice = string.Empty;
        [ObservableProperty] private string _newQty = string.Empty;
        [ObservableProperty] private string _newUnit = "Gram";
        [ObservableProperty] private string _newYield = "100";

        // Logic Edit: Kita simpan ID yang lagi diedit. Kalau 0 berarti mode Add.
        [ObservableProperty] private int _editingId = 0;

        // Property buat label tombol (Simpan vs Update)
        [ObservableProperty] private string _buttonText = "Simpan Baru";

        public IngredientsViewModel(AppDbContext dbContext)
        {
            _dbContext = dbContext;
            LoadDataAsync();
        }

        public async void LoadDataAsync()
        {
            var list = await _dbContext.Ingredients.ToListAsync();
            Ingredients.Clear();
            foreach (var item in list) Ingredients.Add(item);
        }

        // COMMAND: Pas user klik tombol Edit di tabel
        [RelayCommand]
        private void PrepareEdit(Ingredient item)
        {
            // Isi form dengan data yang mau diedit
            NewName = item.Name;
            NewPrice = item.PricePerPackage.ToString();
            NewQty = item.QuantityPerPackage.ToString();
            NewUnit = item.Unit;
            NewYield = item.YieldPercent.ToString();

            // Set ID biar tau ini lagi ngedit
            EditingId = item.Id;
            ButtonText = "Update Data";
        }

        // COMMAND: Pas user klik tombol Batal
        [RelayCommand]
        private void CancelEdit()
        {
            ClearForm();
        }

        [RelayCommand]
        private async Task SaveOrUpdateIngredientAsync()
        {
            if (string.IsNullOrWhiteSpace(NewName) || string.IsNullOrWhiteSpace(NewPrice)) return;

            if (decimal.TryParse(NewPrice, out var price) &&
                double.TryParse(NewQty, out var qty) &&
                double.TryParse(NewYield, out var yield))
            {
                if (yield <= 0) yield = 100;

                if (EditingId == 0)
                {
                    // --- MODE ADD BARU ---
                    var newItem = new Ingredient
                    {
                        Name = NewName,
                        PricePerPackage = price,
                        QuantityPerPackage = qty,
                        Unit = NewUnit,
                        YieldPercent = yield,
                        Category = "General"
                    };
                    _dbContext.Ingredients.Add(newItem);
                    await _dbContext.SaveChangesAsync();
                    Ingredients.Add(newItem);
                }
                else
                {
                    // --- MODE UPDATE ---
                    var itemToUpdate = await _dbContext.Ingredients.FindAsync(EditingId);
                    if (itemToUpdate != null)
                    {
                        itemToUpdate.Name = NewName;
                        itemToUpdate.PricePerPackage = price;
                        itemToUpdate.QuantityPerPackage = qty;
                        itemToUpdate.Unit = NewUnit;
                        itemToUpdate.YieldPercent = yield;

                        _dbContext.Ingredients.Update(itemToUpdate);
                        await _dbContext.SaveChangesAsync();

                        // Refresh List manual biar UI update
                        var index = Ingredients.IndexOf(itemToUpdate); // Cari index lama (object lama mungkin beda ref)
                        // Reload total biar aman
                        LoadDataAsync();
                    }
                }

                ClearForm();
            }
        }

        private void ClearForm()
        {
            NewName = "";
            NewPrice = "";
            NewQty = "";
            NewYield = "100";
            EditingId = 0;
            ButtonText = "Simpan Baru";
        }

        [RelayCommand]
        private async Task DeleteIngredientAsync(Ingredient? item)
        {
            if (item == null) return;
            _dbContext.Ingredients.Remove(item);
            await _dbContext.SaveChangesAsync();
            Ingredients.Remove(item);
        }
    }
}