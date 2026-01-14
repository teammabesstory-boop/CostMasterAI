using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CostMasterAI.Models;
using CostMasterAI.Services;
using CostMasterAI.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;
using System.Text.Json;

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

        // --- INPUT BARU: OVERHEAD ---
        [ObservableProperty] private string _newOverheadName = "";
        [ObservableProperty] private string _newOverheadCost = "";

        public RecipesViewModel(AppDbContext dbContext, AIService aiService)
        {
            _dbContext = dbContext;
            _aiService = aiService;
            LoadDataAsync();
        }

        public async void LoadDataAsync()
        {
            var ingredients = await _dbContext.Ingredients.ToListAsync();
            AvailableIngredients.Clear();
            foreach (var i in ingredients) AvailableIngredients.Add(i);

            // LOAD LENGKAP: Items + Ingredient DAN Overheads
            var recipes = await _dbContext.Recipes
                .Include(r => r.Items).ThenInclude(i => i.Ingredient)
                .Include(r => r.Overheads) // <-- Load Overhead
                .ToListAsync();

            Recipes.Clear();
            foreach (var r in recipes) Recipes.Add(r);
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

        // --- LOGIC TAMBAH BAHAN ---
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
                    UsageUnit = SelectedUsageUnit
                };
                _dbContext.RecipeItems.Add(newItem);
                await _dbContext.SaveChangesAsync();
                await ReloadSelectedRecipe();
                UsageQtyInput = "0";
            }
        }

        [RelayCommand]
        private async Task RemoveItemFromRecipeAsync(RecipeItem? item)
        {
            if (item == null) return;
            _dbContext.RecipeItems.Remove(item);
            await _dbContext.SaveChangesAsync();
            await ReloadSelectedRecipe();
        }

        // --- LOGIC TAMBAH OVERHEAD (OPERASIONAL) ---
        [RelayCommand]
        private async Task AddOverheadAsync()
        {
            if (SelectedRecipe == null || string.IsNullOrWhiteSpace(NewOverheadName)) return;

            if (decimal.TryParse(NewOverheadCost, out var cost) && cost > 0)
            {
                var overhead = new RecipeOverhead
                {
                    RecipeId = SelectedRecipe.Id,
                    Name = NewOverheadName,
                    Cost = cost
                };
                _dbContext.RecipeOverheads.Add(overhead);
                await _dbContext.SaveChangesAsync();

                await ReloadSelectedRecipe();

                // Reset Form
                NewOverheadName = "";
                NewOverheadCost = "";
            }
        }

        [RelayCommand]
        private async Task RemoveOverheadAsync(RecipeOverhead? item)
        {
            if (item == null) return;
            _dbContext.RecipeOverheads.Remove(item);
            await _dbContext.SaveChangesAsync();
            await ReloadSelectedRecipe();
        }

        // --- AI STUFF ---
        [RelayCommand]
        private async Task AutoGenerateIngredientsAsync()
        {
            if (SelectedRecipe == null) return;
            IsAiLoading = true;
            string jsonResult = await _aiService.GenerateRecipeDataAsync(SelectedRecipe.Name);

            if (!string.IsNullOrEmpty(jsonResult))
            {
                try
                {
                    var aiItems = JsonSerializer.Deserialize<List<AiRecipeData>>(jsonResult);
                    if (aiItems != null)
                    {
                        foreach (var item in aiItems)
                        {
                            var existingIngredient = await _dbContext.Ingredients
                                .FirstOrDefaultAsync(i => i.Name.ToLower() == item.IngredientName.ToLower());

                            Ingredient ingredientToUse;
                            if (existingIngredient != null) ingredientToUse = existingIngredient;
                            else
                            {
                                var newIng = new Ingredient
                                {
                                    Name = item.IngredientName,
                                    PricePerPackage = item.EstimatedPrice,
                                    QuantityPerPackage = item.PackageQty,
                                    Unit = item.PackageUnit,
                                    YieldPercent = 100
                                };
                                _dbContext.Ingredients.Add(newIng);
                                await _dbContext.SaveChangesAsync();
                                AvailableIngredients.Add(newIng);
                                ingredientToUse = newIng;
                            }

                            var recipeItem = new RecipeItem
                            {
                                RecipeId = SelectedRecipe.Id,
                                IngredientId = ingredientToUse.Id,
                                UsageQty = item.UsageQty,
                                UsageUnit = item.UsageUnit
                            };
                            _dbContext.RecipeItems.Add(recipeItem);
                        }
                        await _dbContext.SaveChangesAsync();
                        await ReloadSelectedRecipe();
                    }
                }
                catch { }
            }
            IsAiLoading = false;
        }

        [RelayCommand]
        private async Task GenerateDescriptionAsync()
        {
            if (SelectedRecipe == null) return;
            IsAiLoading = true;
            var sb = new StringBuilder();
            foreach (var item in SelectedRecipe.Items)
                sb.Append($"{item.Ingredient.Name} ({item.UsageQty} {item.UsageUnit}), ");

            var result = await _aiService.GenerateMarketingCopyAsync(SelectedRecipe.Name, sb.ToString().TrimEnd(',', ' '));
            SelectedRecipe.Description = result;
            OnPropertyChanged(nameof(SelectedRecipe));
            _dbContext.Recipes.Update(SelectedRecipe);
            await _dbContext.SaveChangesAsync();
            IsAiLoading = false;
        }

        partial void OnSelectedIngredientToAddChanged(Ingredient? value)
        {
            if (value != null) SelectedUsageUnit = value.Unit;
        }

        private async Task ReloadSelectedRecipe()
        {
            if (SelectedRecipe == null) return;
            var id = SelectedRecipe.Id;
            var updatedRecipe = await _dbContext.Recipes
                .Include(r => r.Items).ThenInclude(i => i.Ingredient)
                .Include(r => r.Overheads) // Load Overhead juga pas reload
                .FirstOrDefaultAsync(r => r.Id == id);

            var index = Recipes.IndexOf(SelectedRecipe);
            if (index != -1 && updatedRecipe != null)
            {
                Recipes[index] = updatedRecipe;
                SelectedRecipe = updatedRecipe;
            }
        }
    }
}