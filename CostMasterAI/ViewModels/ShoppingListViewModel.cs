using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging; // Wajib untuk integrasi
using CostMasterAI.Helpers;
using CostMasterAI.Core.Models;
using CostMasterAI.Core.Services;
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

        public ShoppingListViewModel()
        {
            _dbContext = new AppDbContext();
            _ = LoadRecipesAsync();
        }

        public ShoppingListViewModel(AppDbContext dbContext)
        {
            _dbContext = dbContext;
            _ = LoadRecipesAsync();
        }

        public async Task LoadRecipesAsync()
        {
            await _dbContext.Database.EnsureCreatedAsync();
            var recipes = await _dbContext.Recipes
                .AsNoTracking()
                .Include(r => r.Items).ThenInclude(i => i.Ingredient) // Penting: Include Bahan
                .ToListAsync();

            AllRecipes.Clear();
            foreach (var r in recipes)
            {
                // Hitung cost manual jika properti calculated tidak tersimpan
                // r.RecalculateCosts(); 
                AllRecipes.Add(r);
            }
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

        // --- FITUR INTEGRASI BARU: MARK AS BOUGHT ---
        [RelayCommand]
        private async Task MarkAsBoughtAsync(ShoppingItem item)
        {
            if (item == null) return;

            // 1. Buat Transaksi Pengeluaran Otomatis
            var expense = new Transaction
            {
                Date = DateTime.Now,
                Description = $"Belanja: {item.IngredientName} ({item.TotalQuantity:N1} {item.Unit})",
                Amount = item.EstimatedCost, // Harga estimasi jadi harga real
                Type = "Expense",
                PaymentMethod = "Cash (Auto)"
            };

            _dbContext.Transactions.Add(expense);
            await _dbContext.SaveChangesAsync();

            // 2. Kabari Laporan & Dashboard bahwa ada pengeluaran baru
            WeakReferenceMessenger.Default.Send(new TransactionsChangedMessage("AutoExpense"));

            // 3. Hapus dari daftar belanja visual (Tandai Selesai)
            if (ShoppingList.Contains(item))
            {
                ShoppingList.Remove(item);
                TotalEstimatedBudget -= item.EstimatedCost;
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
                        requiredQty = item.UsageQty * plan.TargetQty; // Per Pcs
                    else if (item.IsPerPiece)
                        requiredQty = item.UsageQty * plan.TargetQty; // Per Pcs juga
                    else
                        requiredQty = item.UsageQty * batchRatio; // Per Batch (Ratio)

                    // Konversi Satuan (Misal Resep pakai Gram, Stok pakai Kg)
                    // Sementara kita asumsikan unit sama, atau gunakan helper konversi sederhana
                    double conversionRate = 1;
                    // double conversionRate = UnitHelper.GetConversionRate(item.UsageUnit, item.Ingredient.Unit);
                    // if (conversionRate <= 0) conversionRate = 1;

                    double finalQty = requiredQty * conversionRate;

                    if (aggregation.ContainsKey(item.IngredientId))
                    {
                        aggregation[item.IngredientId].TotalQuantity += finalQty;

                        // Recalculate cost addition
                        decimal costPerUnit = item.Ingredient.QuantityPerPackage > 0
                            ? item.Ingredient.PricePerPackage / (decimal)item.Ingredient.QuantityPerPackage
                            : 0;

                        aggregation[item.IngredientId].EstimatedCost += (decimal)finalQty * costPerUnit;
                    }
                    else
                    {
                        decimal costPerUnit = item.Ingredient.QuantityPerPackage > 0
                            ? item.Ingredient.PricePerPackage / (decimal)item.Ingredient.QuantityPerPackage
                            : 0;

                        aggregation[item.IngredientId] = new ShoppingItem
                        {
                            IngredientName = item.Ingredient.Name,
                            Unit = item.UsageUnit, // Gunakan unit dari resep agar tidak bingung
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