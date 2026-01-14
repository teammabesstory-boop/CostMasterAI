using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CostMasterAI.Models;
using CostMasterAI.Services;
using CostMasterAI.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;
using System.Text.Json;
using System;

namespace CostMasterAI.ViewModels
{
    public partial class RecipesViewModel : ObservableObject
    {
        private readonly AppDbContext _dbContext;
        private readonly AIService _aiService;

        public ObservableCollection<Recipe> Recipes { get; } = new();
        public ObservableCollection<Ingredient> AvailableIngredients { get; } = new();
        public List<string> UnitOptions => UnitHelper.CommonUnits;

        [ObservableProperty] private Recipe? _selectedRecipe;
        [ObservableProperty] private Ingredient? _selectedIngredientToAdd;
        [ObservableProperty] private string _usageQtyInput = "0";
        [ObservableProperty] private string _selectedUsageUnit = "Gram";
        [ObservableProperty] private string _newRecipeName = "";
        [ObservableProperty] private bool _isAiLoading;

        // Input Properties
        [ObservableProperty] private string _newOverheadName = "";
        [ObservableProperty] private string _newOverheadCost = "";
        [ObservableProperty] private string _usageCycles = "1";

        public RecipesViewModel(AppDbContext dbContext, AIService aiService)
        {
            _dbContext = dbContext;
            _aiService = aiService;
            LoadDataAsync();
        }

        public async void LoadDataAsync()
        {
            try
            {
                var ingredients = await _dbContext.Ingredients.ToListAsync();
                AvailableIngredients.Clear();
                foreach (var i in ingredients) AvailableIngredients.Add(i);
                await ReloadRecipesList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error LoadData: {ex.Message}");
            }
        }

        private async Task ReloadRecipesList()
        {
            var recipes = await _dbContext.Recipes
                .Include(r => r.Items).ThenInclude(i => i.Ingredient)
                .Include(r => r.Overheads)
                .ToListAsync();

            Recipes.Clear();
            foreach (var r in recipes) Recipes.Add(r);
        }

        // --- COMMAND: UPDATE DATA RESEP (Labor/Loss) ---
        // Dipanggil saat user ganti angka Loss atau Labor di UI
        [RelayCommand]
        private async Task UpdateRecipeDetailsAsync()
        {
            if (SelectedRecipe == null) return;

            _dbContext.Recipes.Update(SelectedRecipe);
            await _dbContext.SaveChangesAsync();

            // Trigger update perhitungan total
            OnPropertyChanged(nameof(SelectedRecipe));
            await ReloadSelectedRecipe();
        }

        [RelayCommand]
        private async Task RecalculateYieldFromMassAsync()
        {
            if (SelectedRecipe == null || SelectedRecipe.TargetPortionSize <= 0) return;

            // 1. Hitung Berat Mentah Total
            double totalRawMass = 0;
            foreach (var item in SelectedRecipe.Items)
            {
                if (item.IsUnitBased) continue; // Jangan hitung packaging/topping
                double rate = UnitHelper.GetConversionRate(item.UsageUnit, "Gram");
                if (rate <= 0) rate = UnitHelper.GetConversionRate(item.UsageUnit, "ML");
                if (rate > 0) totalRawMass += item.UsageQty * rate;
            }

            if (totalRawMass > 0)
            {
                // 2. Hitung Penyusutan (Cooking Loss)
                // Kalau Loss 10%, berarti sisa 90% (Faktor 0.9)
                double lossFactor = 1 - (SelectedRecipe.CookingLossPercent / 100.0);
                double netMass = totalRawMass * lossFactor;

                // 3. Bagi dengan Target per Porsi
                int newYield = (int)(netMass / SelectedRecipe.TargetPortionSize);

                if (newYield < 1) newYield = 1;

                SelectedRecipe.YieldQty = newYield;
                _dbContext.Recipes.Update(SelectedRecipe);
                await _dbContext.SaveChangesAsync();

                await ReloadSelectedRecipe();
            }
        }

        // --- COMMANDS LAIN (STANDAR) ---
        [RelayCommand]
        private async Task ToggleItemUnitBasedAsync(RecipeItem item)
        {
            if (item == null) return;
            _dbContext.RecipeItems.Update(item);
            await _dbContext.SaveChangesAsync();
            await ReloadSelectedRecipe();
        }

        [RelayCommand]
        private async Task AddItemToRecipeAsync()
        {
            if (SelectedRecipe == null || SelectedIngredientToAdd == null) return;
            if (double.TryParse(UsageQtyInput, out var qty) && qty > 0)
            {
                var newItem = new RecipeItem
                {
                    RecipeId = SelectedRecipe.Id,
                    IngredientId = SelectedIngredientToAdd.Id,
                    UsageQty = qty,
                    UsageUnit = SelectedUsageUnit,
                    IsUnitBased = false
                };
                _dbContext.RecipeItems.Add(newItem);
                await _dbContext.SaveChangesAsync();
                await ReloadSelectedRecipe();
                UsageQtyInput = "0";
            }
        }

        [RelayCommand]
        private async Task AddOverheadAsync()
        {
            if (SelectedRecipe == null || string.IsNullOrWhiteSpace(NewOverheadName)) return;
            if (decimal.TryParse(NewOverheadCost, out var cost) && cost > 0 && double.TryParse(UsageCycles, out var cycles) && cycles > 0)
            {
                decimal finalCost = cost / (decimal)cycles;
                var overhead = new RecipeOverhead
                {
                    RecipeId = SelectedRecipe.Id,
                    Name = NewOverheadName + (cycles > 1 ? $" (1/{cycles} siklus)" : ""),
                    Cost = finalCost
                };
                _dbContext.RecipeOverheads.Add(overhead);
                await _dbContext.SaveChangesAsync();
                await ReloadSelectedRecipe();
                NewOverheadName = ""; NewOverheadCost = ""; UsageCycles = "1";
            }
        }

        [RelayCommand]
        private async Task CreateRecipeAsync()
        {
            if (string.IsNullOrWhiteSpace(NewRecipeName)) return;
            var newRecipe = new Recipe { Name = NewRecipeName, YieldQty = 1 };
            _dbContext.Recipes.Add(newRecipe);
            await _dbContext.SaveChangesAsync();
            Recipes.Add(newRecipe);
            SelectedRecipe = newRecipe;
            NewRecipeName = "";
        }

        [RelayCommand]
        private async Task RemoveItemFromRecipeAsync(RecipeItem? item)
        {
            if (item == null) return;
            _dbContext.RecipeItems.Remove(item);
            await _dbContext.SaveChangesAsync();
            await ReloadSelectedRecipe();
        }

        [RelayCommand]
        private async Task RemoveOverheadAsync(RecipeOverhead? item)
        {
            if (item == null) return;
            _dbContext.RecipeOverheads.Remove(item);
            await _dbContext.SaveChangesAsync();
            await ReloadSelectedRecipe();
        }

        [RelayCommand]
        private async Task GenerateDescriptionAsync()
        {
            if (SelectedRecipe == null) return;
            IsAiLoading = true;
            var sb = new StringBuilder();
            foreach (var item in SelectedRecipe.Items) sb.Append($"{item.Ingredient.Name} ({item.UsageQty} {item.UsageUnit}), ");
            var result = await _aiService.GenerateMarketingCopyAsync(SelectedRecipe.Name, sb.ToString().TrimEnd(',', ' '));
            SelectedRecipe.Description = result;
            _dbContext.Recipes.Update(SelectedRecipe);
            await _dbContext.SaveChangesAsync();
            OnPropertyChanged(nameof(SelectedRecipe));
            IsAiLoading = false;
        }

        [RelayCommand]
        private async Task AutoGenerateIngredientsAsync()
        {
            if (SelectedRecipe == null) return;
            IsAiLoading = true;
            try
            {
                string jsonResult = await _aiService.GenerateRecipeDataAsync(SelectedRecipe.Name);
                if (!string.IsNullOrEmpty(jsonResult))
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var aiItems = JsonSerializer.Deserialize<List<AiRecipeData>>(jsonResult, options);
                    if (aiItems != null)
                    {
                        foreach (var item in aiItems)
                        {
                            var existingIngredient = await _dbContext.Ingredients.FirstOrDefaultAsync(i => i.Name.ToLower() == item.IngredientName.ToLower());
                            Ingredient ingredientToUse;
                            if (existingIngredient != null) ingredientToUse = existingIngredient;
                            else
                            {
                                var newIng = new Ingredient { Name = item.IngredientName, PricePerPackage = item.EstimatedPrice, QuantityPerPackage = item.PackageQty, Unit = item.PackageUnit, YieldPercent = 100 };
                                _dbContext.Ingredients.Add(newIng);
                                await _dbContext.SaveChangesAsync();
                                AvailableIngredients.Add(newIng);
                                ingredientToUse = newIng;
                            }
                            var recipeItem = new RecipeItem { RecipeId = SelectedRecipe.Id, IngredientId = ingredientToUse.Id, UsageQty = item.UsageQty, UsageUnit = item.UsageUnit };
                            _dbContext.RecipeItems.Add(recipeItem);
                        }
                        await _dbContext.SaveChangesAsync();
                        await ReloadSelectedRecipe();
                    }
                }
            }
            catch { }
            finally { IsAiLoading = false; }
        }

        partial void OnSelectedIngredientToAddChanged(Ingredient? value)
        {
            if (value != null) SelectedUsageUnit = value.Unit;
        }

        private async Task ReloadSelectedRecipe()
        {
            if (SelectedRecipe == null) return;
            var id = SelectedRecipe.Id;
            _dbContext.ChangeTracker.Clear(); // PENTING: Clear tracker
            var updatedRecipe = await _dbContext.Recipes
                .Include(r => r.Items).ThenInclude(i => i.Ingredient)
                .Include(r => r.Overheads)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (updatedRecipe != null)
            {
                _dbContext.Attach(updatedRecipe);
                var index = -1;
                for (int i = 0; i < Recipes.Count; i++) { if (Recipes[i].Id == id) { index = i; break; } }
                if (index != -1) { Recipes[index] = updatedRecipe; SelectedRecipe = updatedRecipe; }
            }
        }
    }
}