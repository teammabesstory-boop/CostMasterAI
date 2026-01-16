using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CostMasterAI.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace CostMasterAI.ViewModels
{
    // Helper Class: Item Rencana Produksi (Resep + Jumlah yg mau dimasak)
    public partial class ProductionPlanItem : ObservableObject
    {
        public Recipe Recipe { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SubtotalCost))]
        private int _targetQty;

        public ProductionPlanItem(Recipe recipe)
        {
            Recipe = recipe;
            TargetQty = recipe.YieldQty; // Default isi sesuai resep asli
        }

        public decimal SubtotalCost => Recipe.CostPerUnit * TargetQty;
    }

    public partial class ShoppingListViewModel : ObservableObject
    {
        private readonly AppDbContext _dbContext;

        // Sumber Data
        public ObservableCollection<Recipe> AllRecipes { get; } = new();

        // Rencana Masak User
        public ObservableCollection<ProductionPlanItem> ProductionPlan { get; } = new();

        // Hasil Daftar Belanja
        public ObservableCollection<ShoppingItem> ShoppingList { get; } = new();

        [ObservableProperty] private Recipe? _selectedRecipeToAdd;
        [ObservableProperty] private decimal _totalEstimatedBudget;

        public ShoppingListViewModel(AppDbContext dbContext)
        {
            _dbContext = dbContext;
            LoadRecipes();
        }

        public async void LoadRecipes()
        {
            var recipes = await _dbContext.Recipes
                .Include(r => r.Items).ThenInclude(i => i.Ingredient) // Penting: Include Bahan
                .ToListAsync();

            AllRecipes.Clear();
            foreach (var r in recipes) AllRecipes.Add(r);
        }

        [RelayCommand]
        private void AddToPlan()
        {
            if (SelectedRecipeToAdd == null) return;

            // Cek kalau udah ada di plan, jangan duplikat
            if (ProductionPlan.Any(x => x.Recipe.Id == SelectedRecipeToAdd.Id)) return;

            ProductionPlan.Add(new ProductionPlanItem(SelectedRecipeToAdd));
            SelectedRecipeToAdd = null; // Reset combo box

            // Otomatis generate ulang list
            GenerateShoppingList();
        }

        [RelayCommand]
        private void RemoveFromPlan(ProductionPlanItem item)
        {
            if (ProductionPlan.Contains(item))
            {
                ProductionPlan.Remove(item);
                GenerateShoppingList();
            }
        }

        // --- THE MAGIC: AGGREGATION LOGIC ---
        [RelayCommand]
        public void GenerateShoppingList()
        {
            ShoppingList.Clear();
            TotalEstimatedBudget = 0;

            var aggregation = new Dictionary<int, ShoppingItem>();

            foreach (var plan in ProductionPlan)
            {
                if (plan.TargetQty <= 0) continue;

                double batchRatio = (double)plan.TargetQty / (plan.Recipe.YieldQty > 0 ? plan.Recipe.YieldQty : 1);

                foreach (var item in plan.Recipe.Items)
                {
                    if (item.Ingredient == null) continue;

                    double requiredQty = 0;

                    if (item.IsUnitBased)
                        requiredQty = item.UsageQty * plan.TargetQty;
                    else
                        requiredQty = item.UsageQty * batchRatio;

                    double conversionRate = UnitHelper.GetConversionRate(item.UsageUnit, item.Ingredient.Unit);
                    if (conversionRate <= 0) conversionRate = 1;

                    double finalQty = requiredQty * conversionRate;

                    if (aggregation.ContainsKey(item.IngredientId))
                    {
                        aggregation[item.IngredientId].TotalQuantity += finalQty;
                        aggregation[item.IngredientId].EstimatedCost += (decimal)finalQty * (item.Ingredient.PricePerPackage / (decimal)item.Ingredient.QuantityPerPackage);
                    }
                    else
                    {
                        decimal costPerUnit = item.Ingredient.QuantityPerPackage > 0
                            ? item.Ingredient.PricePerPackage / (decimal)item.Ingredient.QuantityPerPackage
                            : 0;

                        aggregation[item.IngredientId] = new ShoppingItem
                        {
                            IngredientName = item.Ingredient.Name,
                            Unit = item.Ingredient.Unit,
                            TotalQuantity = finalQty,
                            EstimatedCost = (decimal)finalQty * costPerUnit,
                            Category = item.Ingredient.Category
                        };
                    }
                }
            }

            // 4. Render ke UI dengan Rounding
            foreach (var kvp in aggregation)
            {
                // FIX: Bulatkan 2 angka di belakang koma biar rapi di DataGrid
                kvp.Value.TotalQuantity = Math.Round(kvp.Value.TotalQuantity, 2);

                ShoppingList.Add(kvp.Value);
                TotalEstimatedBudget += kvp.Value.EstimatedCost;
            }
        }
    }
}