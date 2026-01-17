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

        // --- CONSTRUCTOR INJECTION ---
        public ShoppingListViewModel(AppDbContext dbContext)
        {
            _dbContext = dbContext;
            _ = LoadRecipesAsync();
        }

        public async Task LoadRecipesAsync()
        {
            try
            {
                await _dbContext.Database.EnsureCreatedAsync();

                // Pastikan tracking dimatikan untuk performa (AsNoTracking)
                // Kita include Ingredients agar data lengkap
                var recipes = await _dbContext.Recipes
                    .AsNoTracking()
                    .Include(r => r.Items).ThenInclude(i => i.Ingredient)
                    .ToListAsync();

                AllRecipes.Clear();
                foreach (var r in recipes)
                {
                    AllRecipes.Add(r);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error LoadRecipes: {ex.Message}");
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

            // Otomatis generate ulang list belanja saat rencana berubah
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

        // --- FITUR REAL INVENTORY SYSTEM: MARK AS BOUGHT ---
        // Method ini dipanggil saat user klik "Beli" / "Selesai" di tabel belanja
        [RelayCommand]
        private async Task MarkAsBoughtAsync(ShoppingItem item)
        {
            if (item == null) return;

            try
            {
                // 1. CARI BAHAN ASLI DI DATABASE
                // Kita cari berdasarkan nama karena ShoppingItem adalah objek sementara (agregasi)
                var ingredient = await _dbContext.Ingredients
                    .FirstOrDefaultAsync(i => i.Name == item.IngredientName);

                if (ingredient != null)
                {
                    // A. Update Stok Fisik (Bertambah karena beli)
                    ingredient.CurrentStock += item.TotalQuantity;
                    _dbContext.Ingredients.Update(ingredient);

                    // B. Catat di Kartu Stok (Stock Transaction)
                    // Pastikan class StockTransaction sudah dibuat di Project Core
                    var stockLog = new StockTransaction
                    {
                        IngredientId = ingredient.Id,
                        Date = DateTime.Now,
                        Type = "In", // Barang Masuk
                        Quantity = item.TotalQuantity,
                        Unit = item.Unit,
                        Description = "Pembelian via Shopping List"
                    };
                    _dbContext.StockTransactions.Add(stockLog);
                }

                // 2. CATAT KEUANGAN (Pengeluaran Kas)
                var expense = new Transaction
                {
                    Date = DateTime.Now,
                    Description = $"Belanja: {item.IngredientName} ({item.TotalQuantity:N1} {item.Unit})",
                    Amount = item.EstimatedCost, // Harga estimasi dianggap harga real
                    Type = "Expense",
                    PaymentMethod = "Cash (Auto)"
                };
                _dbContext.Transactions.Add(expense);

                // Simpan semua perubahan (Stok & Uang) ke database sekaligus
                await _dbContext.SaveChangesAsync();

                // 3. NOTIFIKASI KE SISTEM LAIN (Messaging Center)
                // Kabari Dashboard agar Cash Balance berkurang
                WeakReferenceMessenger.Default.Send(new TransactionsChangedMessage("AutoExpense"));
                // Kabari Halaman Bahan Baku agar Stok terupdate di UI
                WeakReferenceMessenger.Default.Send(new IngredientsChangedMessage("StockUpdated"));

                // 4. HAPUS DARI LIST BELANJA (Visual UI)
                if (ShoppingList.Contains(item))
                {
                    ShoppingList.Remove(item);
                    TotalEstimatedBudget -= item.EstimatedCost;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error MarkAsBought: {ex.Message}");
                // Jika error CS1061/CS0246 muncul di sini, berarti Project Core belum di-Rebuild
                // atau file StockTransaction.cs belum ada di namespace CostMasterAI.Core.Models
            }
        }

        // --- THE MAGIC: AGGREGATION LOGIC ---
        // Menghitung total kebutuhan bahan dari semua resep yang direncanakan
        [RelayCommand]
        public void GenerateShoppingList()
        {
            ShoppingList.Clear();
            TotalEstimatedBudget = 0;

            var aggregation = new Dictionary<int, ShoppingItem>();

            foreach (var plan in ProductionPlan)
            {
                if (plan.TargetQty <= 0) continue;

                // Hitung rasio batch. Misal Yield resep 10 pcs, mau bikin 50 pcs, berarti 5x lipat.
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

                    // Asumsi konversi 1:1 untuk simplifikasi awal
                    // (Nanti bisa ditambah UnitConverterService)
                    double conversionRate = 1;
                    double finalQty = requiredQty * conversionRate;

                    if (aggregation.ContainsKey(item.IngredientId))
                    {
                        // Jika bahan sudah ada di list, tambahkan qty-nya
                        aggregation[item.IngredientId].TotalQuantity += finalQty;

                        // Hitung tambahan biaya
                        decimal costPerUnit = item.Ingredient.QuantityPerPackage > 0
                            ? item.Ingredient.PricePerPackage / (decimal)item.Ingredient.QuantityPerPackage
                            : 0;

                        aggregation[item.IngredientId].EstimatedCost += (decimal)finalQty * costPerUnit;
                    }
                    else
                    {
                        // Jika belum ada, buat entry baru
                        decimal costPerUnit = item.Ingredient.QuantityPerPackage > 0
                            ? item.Ingredient.PricePerPackage / (decimal)item.Ingredient.QuantityPerPackage
                            : 0;

                        aggregation[item.IngredientId] = new ShoppingItem
                        {
                            IngredientName = item.Ingredient.Name,
                            Unit = item.UsageUnit, // Gunakan unit dari resep
                            TotalQuantity = finalQty,
                            EstimatedCost = (decimal)finalQty * costPerUnit,
                            Category = item.Ingredient.Category
                        };
                    }
                }
            }

            // Render ke UI
            foreach (var kvp in aggregation)
            {
                kvp.Value.TotalQuantity = Math.Round(kvp.Value.TotalQuantity, 2);
                ShoppingList.Add(kvp.Value);
                TotalEstimatedBudget += kvp.Value.EstimatedCost;
            }
        }
    }
}