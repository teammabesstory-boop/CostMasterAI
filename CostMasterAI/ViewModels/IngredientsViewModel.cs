using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CostMasterAI.Models;
using CostMasterAI.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace CostMasterAI.ViewModels
{
    // --- UPGRADE: IngredientTab jadi ObservableObject ---
    public partial class IngredientTab : ObservableObject
    {
        [ObservableProperty] private string _header;
        [ObservableProperty] private string _icon;
        [ObservableProperty] private ObservableCollection<Ingredient> _ingredients;
        [ObservableProperty] private bool _isClosable;

        // Link ke Database (Null kalau tab "Draft" atau "Semua Bahan")
        public int? RecipeId { get; set; }

        // State Edit Mode
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotEditing))]
        private bool _isEditing;

        // Helper buat UI (Kebalikan dari IsEditing)
        public bool IsNotEditing => !IsEditing;
    }

    public partial class IngredientsViewModel : ObservableObject
    {
        private readonly AppDbContext _dbContext;

        public ObservableCollection<IngredientTab> Tabs { get; } = new();

        // Property Form (Global)
        [ObservableProperty] private string _newName = string.Empty;
        [ObservableProperty] private string _newPrice = string.Empty;
        [ObservableProperty] private string _newQty = string.Empty;
        [ObservableProperty] private string _newUnit = "Gram";
        [ObservableProperty] private string _newYield = "100";
        [ObservableProperty] private int _editingId = 0;
        [ObservableProperty] private string _buttonText = "Simpan Baru";

        public IngredientsViewModel(AppDbContext dbContext)
        {
            _dbContext = dbContext;
            LoadDataAsync();
        }

        public async void LoadDataAsync()
        {
            try
            {
                Tabs.Clear();

                // 1. TAB UTAMA
                var allIngredients = await _dbContext.Ingredients.ToListAsync();
                Tabs.Add(new IngredientTab
                {
                    Header = "📦 Semua Bahan",
                    Icon = "Home",
                    IsClosable = false,
                    Ingredients = new ObservableCollection<Ingredient>(allIngredients)
                });

                // 2. TAB RESEP
                var recipes = await _dbContext.Recipes
                    .Include(r => r.Items)
                    .ThenInclude(i => i.Ingredient)
                    .ToListAsync();

                foreach (var recipe in recipes)
                {
                    var recipeIngredients = recipe.Items
                        .Select(i => i.Ingredient)
                        .Where(i => i != null)
                        .Distinct()
                        .ToList();

                    if (recipeIngredients.Any())
                    {
                        Tabs.Add(new IngredientTab
                        {
                            Header = $"📜 {recipe.Name}",
                            Icon = "Document",
                            IsClosable = true,
                            Ingredients = new ObservableCollection<Ingredient>(recipeIngredients),
                            RecipeId = recipe.Id // Simpan ID Resep
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error Loading Tabs: {ex.Message}");
            }
        }

        // --- COMMANDS HEADER EDITING ---

        [RelayCommand]
        public void StartEditTab(IngredientTab tab)
        {
            if (tab != null) tab.IsEditing = true;
        }

        [RelayCommand]
        public async Task SaveTabHeader(IngredientTab tab)
        {
            if (tab == null) return;

            tab.IsEditing = false; // Keluar mode edit

            // Kalau ini Tab Resep, update nama resep di Database juga
            if (tab.RecipeId.HasValue)
            {
                var recipe = await _dbContext.Recipes.FindAsync(tab.RecipeId.Value);
                if (recipe != null)
                {
                    // Hapus emoji "📜 " dari header kalau ada, biar bersih di DB
                    var cleanName = tab.Header.Replace("📜 ", "").Trim();
                    recipe.Name = cleanName;

                    _dbContext.Recipes.Update(recipe);
                    await _dbContext.SaveChangesAsync();
                }
            }
        }

        // --- COMMANDS LAIN (Tetap Sama) ---

        [RelayCommand]
        public void AddNewTab()
        {
            Tabs.Add(new IngredientTab
            {
                Header = "📝 Draft Baru",
                Icon = "Edit",
                IsClosable = true,
                Ingredients = new ObservableCollection<Ingredient>()
            });
        }

        [RelayCommand]
        public void CloseTab(IngredientTab tabToClose)
        {
            if (Tabs.Contains(tabToClose)) Tabs.Remove(tabToClose);
        }

        [RelayCommand]
        private void PrepareEdit(Ingredient item)
        {
            NewName = item.Name;
            NewPrice = item.PricePerPackage.ToString();
            NewQty = item.QuantityPerPackage.ToString();
            NewUnit = item.Unit;
            NewYield = item.YieldPercent.ToString();
            EditingId = item.Id;
            ButtonText = "Update Data";
        }

        [RelayCommand]
        private void CancelEdit()
        {
            ClearForm();
        }

        [RelayCommand]
        private async Task SaveOrUpdateIngredientAsync()
        {
            if (string.IsNullOrWhiteSpace(NewName) || string.IsNullOrWhiteSpace(NewPrice)) return;
            if (decimal.TryParse(NewPrice, out var price) && double.TryParse(NewQty, out var qty) && double.TryParse(NewYield, out var yield))
            {
                if (yield <= 0) yield = 100;
                try
                {
                    if (EditingId == 0)
                    {
                        var newItem = new Ingredient { Name = NewName, PricePerPackage = price, QuantityPerPackage = qty, Unit = NewUnit, YieldPercent = yield, Category = "General" };
                        _dbContext.Ingredients.Add(newItem);
                        await _dbContext.SaveChangesAsync();
                    }
                    else
                    {
                        var itemToUpdate = await _dbContext.Ingredients.FindAsync(EditingId);
                        if (itemToUpdate != null)
                        {
                            itemToUpdate.Name = NewName; itemToUpdate.PricePerPackage = price; itemToUpdate.QuantityPerPackage = qty; itemToUpdate.Unit = NewUnit; itemToUpdate.YieldPercent = yield;
                            _dbContext.Ingredients.Update(itemToUpdate);
                            await _dbContext.SaveChangesAsync();
                        }
                    }
                    ClearForm();
                    LoadDataAsync();
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"❌ Error Saving: {ex.Message}"); }
            }
        }

        private void ClearForm()
        {
            NewName = ""; NewPrice = ""; NewQty = ""; NewYield = "100"; EditingId = 0; ButtonText = "Simpan Baru";
        }

        [RelayCommand]
        private async Task DeleteIngredientAsync(Ingredient? item)
        {
            if (item == null) return;
            try
            {
                _dbContext.Ingredients.Remove(item);
                await _dbContext.SaveChangesAsync();
                LoadDataAsync();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"❌ Error Deleting: {ex.Message}"); }
        }
    }
}