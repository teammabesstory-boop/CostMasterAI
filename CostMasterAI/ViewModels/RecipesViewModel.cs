using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CostMasterAI.Services;
using CostMasterAI.Helpers;
using CostMasterAI.Core.Services;
using CostMasterAI.Core.Models;
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

        // Collections
        public ObservableCollection<Recipe> Recipes { get; } = new();
        public ObservableCollection<Ingredient> AvailableIngredients { get; } = new();
        public List<string> UnitOptions => UnitHelper.CommonUnits;

        // Selection & Basic Inputs
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DoughCost))]
        [NotifyPropertyChangedFor(nameof(ToppingCost))]
        [NotifyPropertyChangedFor(nameof(PackagingCost))]
        [NotifyPropertyChangedFor(nameof(OperationalCost))]
        [NotifyPropertyChangedFor(nameof(FinalHppBatch))]
        [NotifyPropertyChangedFor(nameof(FinalHppPerUnit))]
        [NotifyPropertyChangedFor(nameof(RecommendedPriceMargin))]
        [NotifyPropertyChangedFor(nameof(RecommendedPriceMarkup))]
        [NotifyPropertyChangedFor(nameof(TotalMainDoughWeight))]
        [NotifyPropertyChangedFor(nameof(EstimatedProfitPerUnit))]
        [NotifyPropertyChangedFor(nameof(EstimatedProfitPerBatch))]
        [NotifyPropertyChangedFor(nameof(BreakEvenPointQty))]
        [NotifyPropertyChangedFor(nameof(MainIngredients))]
        [NotifyPropertyChangedFor(nameof(SupportIngredients))]
        [NotifyPropertyChangedFor(nameof(PackagingItems))]
        private Recipe? _selectedRecipe;

        [ObservableProperty] private Ingredient? _selectedIngredientToAdd;
        [ObservableProperty] private string _usageQtyInput = "0";
        [ObservableProperty] private string _selectedUsageUnit = "Gram";
        [ObservableProperty] private string _newRecipeName = "";

        // Input Overhead
        [ObservableProperty] private string _newOverheadName = "";
        [ObservableProperty] private string _newOverheadCost = "";
        [ObservableProperty] private string _usageCycles = "1";

        // Input Simulator
        [ObservableProperty] private int _targetScalingQty;
        [ObservableProperty] private double _targetFlourQty;

        // --- PROPERTIES BARU UNTUK INPUT KATEGORI & PER PCS ---
        [ObservableProperty] private string _selectedCategoryToAdd = "Main";
        [ObservableProperty] private bool _isInputPerPiece;

        public List<string> RecipeCategories { get; } = new()
        {
            "Main",      // Bahan Utama
            "Support",   // Bahan Penolong
            "Packaging"  // Kemasan
        };

        // --- ADVANCED ANALYSIS PROPERTIES ---

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FinalHppBatch))]
        [NotifyPropertyChangedFor(nameof(FinalHppPerUnit))]
        [NotifyPropertyChangedFor(nameof(EstimatedProfitPerUnit))]
        private double _wasteBufferPercent = 5.0;

        // Flag untuk mencegah infinite loop (Harga -> Margin -> Harga -> Margin...)
        private bool _isUpdatingFromPrice = false;

        // 2. Target Margin (Slider)
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ProfitMarginDisplay))]
        [NotifyPropertyChangedFor(nameof(EstimatedProfitPerUnit))]
        [NotifyPropertyChangedFor(nameof(EstimatedProfitPerBatch))]
        [NotifyPropertyChangedFor(nameof(BreakEvenPointQty))]
        private double _targetMarginPercent = 40.0;

        // Trigger saat Slider Margin digeser user
        partial void OnTargetMarginPercentChanged(double value)
        {
            // Jika sedang update dari harga manual, jangan hitung harga lagi (biar tidak konflik)
            if (_isUpdatingFromPrice) return;

            if (FinalHppPerUnit > 0)
            {
                decimal marginFactor = 1 - (decimal)(value / 100.0);
                if (marginFactor <= 0.01m) marginFactor = 0.01m;

                decimal preTaxPrice = FinalHppPerUnit / marginFactor;
                decimal taxFactor = 1 + (decimal)(TaxPercent / 100.0);

                // Harga Jual Akhir = Harga Sebelum Pajak + Pajak
                decimal finalPrice = preTaxPrice * taxFactor;

                _manualSellingPriceInput = RoundUpToNearestHundred(finalPrice).ToString("F0");
                OnPropertyChanged(nameof(ManualSellingPriceInput));
            }
        }

        // 3. Tax / Pajak
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EstimatedProfitPerUnit))]
        private double _taxPercent = 0.0;

        partial void OnTaxPercentChanged(double value)
        {
            // Recalculate harga jual jika pajak berubah, pertahankan margin
            OnTargetMarginPercentChanged(TargetMarginPercent);
        }

        // 4. Manual Selling Price (Input Text Box)
        [ObservableProperty]
        private string _manualSellingPriceInput = "0";

        // FIX: Trigger saat User mengetik Harga Manual dan tekan Enter/LostFocus
        partial void OnManualSellingPriceInputChanged(string value)
        {
            RecalculateMarginFromPrice();
        }

        [RelayCommand]
        private void RecalculateMarginFromPrice()
        {
            if (!decimal.TryParse(ManualSellingPriceInput, out decimal finalPrice) || finalPrice <= 0 || FinalHppPerUnit <= 0) return;

            // Set flag agar perubahan Margin TIDAK mengubah balik Harga Jual
            _isUpdatingFromPrice = true;

            try
            {
                // Hitung mundur: Harga Jual -> Harga Sebelum Pajak -> Margin
                decimal taxFactor = 1 + (decimal)(TaxPercent / 100.0);
                decimal pricePreTax = finalPrice / taxFactor;

                if (pricePreTax > 0)
                {
                    // Rumus Margin: (HargaJual - HPP) / HargaJual
                    decimal marginDecimal = (pricePreTax - FinalHppPerUnit) / pricePreTax;
                    double newMarginPercent = (double)(marginDecimal * 100);

                    // Batasi agar slider tidak error
                    if (newMarginPercent < 0) newMarginPercent = 0;
                    if (newMarginPercent > 99.9) newMarginPercent = 99.9;

                    TargetMarginPercent = Math.Round(newMarginPercent, 1);
                }
            }
            finally
            {
                _isUpdatingFromPrice = false;
            }

            // Update UI properties lain
            OnPropertyChanged(nameof(EstimatedProfitPerUnit));
            OnPropertyChanged(nameof(EstimatedProfitPerBatch));
            OnPropertyChanged(nameof(BreakEvenPointQty));
            OnPropertyChanged(nameof(ProfitMarginDisplay));
        }

        private decimal RoundUpToNearestHundred(decimal price)
        {
            return Math.Ceiling(price / 100m) * 100m;
        }

        // --- UPDATED GROUPING LISTS ---
        public IEnumerable<RecipeItem> MainIngredients => _selectedRecipe?.Items.Where(i => i.UsageCategory == "Main") ?? Enumerable.Empty<RecipeItem>();
        public IEnumerable<RecipeItem> SupportIngredients => _selectedRecipe?.Items.Where(i => i.UsageCategory == "Support") ?? Enumerable.Empty<RecipeItem>();
        public IEnumerable<RecipeItem> PackagingItems => _selectedRecipe?.Items.Where(i => i.UsageCategory == "Packaging") ?? Enumerable.Empty<RecipeItem>();

        // Calculated Properties
        public decimal DoughCost => MainIngredients.Sum(i => i.CalculatedCost);
        public decimal ToppingCost => SupportIngredients.Sum(i => i.CalculatedCost);
        public decimal PackagingCost => PackagingItems.Sum(i => i.CalculatedCost);
        public decimal OperationalCost => (SelectedRecipe?.TotalOverheadCost ?? 0) + (SelectedRecipe?.TotalLaborCost ?? 0);

        public double TotalMainDoughWeight
        {
            get
            {
                double totalWeight = 0;
                foreach (var item in MainIngredients)
                {
                    double itemTotalUsage = item.UsageQty;
                    if (item.IsPerPiece && SelectedRecipe != null) itemTotalUsage *= SelectedRecipe.YieldQty;

                    double weightInGram = 0;
                    if (string.Equals(item.UsageUnit, "ML", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(item.UsageUnit, "Milliliter", StringComparison.OrdinalIgnoreCase))
                    {
                        weightInGram = itemTotalUsage;
                    }
                    else if (string.Equals(item.UsageUnit, "Liter", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(item.UsageUnit, "L", StringComparison.OrdinalIgnoreCase))
                    {
                        weightInGram = itemTotalUsage * 1000;
                    }
                    else if (item.IsUnitBased)
                    {
                        weightInGram = itemTotalUsage * UnitHelper.GetConversionRate(item.UsageUnit, "Gram");
                    }
                    else
                    {
                        double rate = UnitHelper.GetConversionRate(item.UsageUnit, "Gram");
                        if (rate > 0) weightInGram = itemTotalUsage * rate;
                    }
                    totalWeight += weightInGram;
                }
                return totalWeight;
            }
        }

        public decimal FinalHppBatch
        {
            get
            {
                if (SelectedRecipe == null) return 0;
                decimal baseCost = DoughCost + ToppingCost + PackagingCost + OperationalCost;
                decimal wasteCost = baseCost * (decimal)(WasteBufferPercent / 100.0);
                return baseCost + wasteCost;
            }
        }

        public decimal FinalHppPerUnit
        {
            get
            {
                if (SelectedRecipe == null || SelectedRecipe.YieldQty == 0) return 0;
                return FinalHppBatch / SelectedRecipe.YieldQty;
            }
        }

        public decimal RecommendedSellingPrice => decimal.TryParse(ManualSellingPriceInput, out var val) ? val : 0;

        public decimal EstimatedProfitPerUnit
        {
            get
            {
                if (FinalHppPerUnit <= 0) return 0;
                decimal currentPrice = decimal.TryParse(ManualSellingPriceInput, out var val) ? val : 0;
                decimal taxFactor = 1 + (decimal)(TaxPercent / 100.0);
                decimal cleanPrice = currentPrice / taxFactor;
                return cleanPrice - FinalHppPerUnit;
            }
        }

        public decimal EstimatedProfitPerBatch
        {
            get
            {
                if (SelectedRecipe == null) return 0;
                return EstimatedProfitPerUnit * SelectedRecipe.YieldQty;
            }
        }

        public int BreakEvenPointQty
        {
            get
            {
                if (EstimatedProfitPerUnit <= 0) return 0;
                decimal currentPrice = decimal.TryParse(ManualSellingPriceInput, out var val) ? val : 1;
                return (int)Math.Ceiling(FinalHppBatch / (currentPrice > 0 ? currentPrice : 1));
            }
        }

        public string ProfitMarginDisplay => $"{TargetMarginPercent:N1}%";
        public decimal RecommendedPriceMargin => FinalHppPerUnit > 0 ? FinalHppPerUnit / 0.6m : 0;
        public decimal RecommendedPriceMarkup => FinalHppPerUnit * 2;

        public string GenerateCostDetailString()
        {
            if (SelectedRecipe == null) return "Data tidak tersedia.";

            var sb = new StringBuilder();

            decimal baseDough = DoughCost;
            decimal baseTopping = ToppingCost;
            decimal basePack = PackagingCost;
            decimal baseOps = OperationalCost;
            decimal totalBase = baseDough + baseTopping + basePack + baseOps;

            // Kalkulasi Risiko & Waste
            decimal wasteValue = totalBase * (decimal)(WasteBufferPercent / 100.0);
            decimal totalWithWaste = totalBase + wasteValue;

            // Kalkulasi HPP per Unit
            decimal hppUnit = 0;
            if (SelectedRecipe.YieldQty > 0)
                hppUnit = totalWithWaste / (decimal)SelectedRecipe.YieldQty;

            // Analisa Harga Jual & Pajak
            decimal currentPrice = decimal.TryParse(ManualSellingPriceInput, out var p) ? p : 0;
            decimal taxFactor = 1 + (decimal)(TaxPercent / 100.0);
            decimal pricePreTax = currentPrice / taxFactor;
            decimal taxValue = currentPrice - pricePreTax;
            decimal profitValue = pricePreTax - hppUnit;

            sb.AppendLine("=== 1. STRUKTUR BIAYA DASAR (BATCH) ===");
            sb.AppendLine($"• Adonan\t: Rp {baseDough:N0}");
            sb.AppendLine($"• Topping\t: Rp {baseTopping:N0}");
            sb.AppendLine($"• Kemasan\t: Rp {basePack:N0}");
            sb.AppendLine($"• Overhead\t: Rp {baseOps:N0}");
            sb.AppendLine($"----------------------------------------");
            sb.AppendLine($"TOTAL MODAL AWAL\t: Rp {totalBase:N0}");
            sb.AppendLine();

            sb.AppendLine($"=== 2. RISIKO & WASTE ({WasteBufferPercent}%) ===");
            sb.AppendLine($"• Estimasi Buang\t: Rp {wasteValue:N0}");
            sb.AppendLine($"----------------------------------------");
            sb.AppendLine($"MODAL SETELAH WASTE\t: Rp {totalWithWaste:N0}");
            sb.AppendLine();

            sb.AppendLine("=== 3. HARGA POKOK (HPP) ===");
            sb.AppendLine($"• Total Modal / {SelectedRecipe.YieldQty} Pcs");
            sb.AppendLine($"• HPP BERSIH PER PCS\t: Rp {hppUnit:N2}");
            sb.AppendLine();

            sb.AppendLine($"=== 4. ANALISA HARGA JUAL ===");
            sb.AppendLine($"• Harga Jual Final\t: Rp {currentPrice:N0}");
            sb.AppendLine($"• Pajak ({TaxPercent}%)\t: -Rp {taxValue:N0}");
            sb.AppendLine($"• Pendapatan Bersih\t: Rp {pricePreTax:N0}");
            sb.AppendLine($"• HPP per Unit\t: -Rp {hppUnit:N0}");
            sb.AppendLine($"----------------------------------------");
            sb.AppendLine($"PROFIT BERSIH\t: Rp {profitValue:N0} / pcs");
            sb.AppendLine($"MARGIN AKTUAL\t: {ProfitMarginDisplay}");

            return sb.ToString();
        }

        // --- AI PROPERTIES ---
        [ObservableProperty] private bool _isAiLoading;
        [ObservableProperty] private string _aiAuditReport = "Klik tombol 'Audit' untuk memulai analisa HPP Donat.";
        [ObservableProperty] private string _aiSubstitutionReport = "Klik tombol 'Saran' untuk mencari alternatif topping/tepung.";
        [ObservableProperty] private string _aiWasteReport = "Klik tombol 'Analisa' untuk ide pemanfaatan sisa adonan.";

        [ObservableProperty] private bool _isAuditing;
        [ObservableProperty] private bool _isSubstituting;
        [ObservableProperty] private bool _isWasteAnalyzing;

        [ObservableProperty] private string _aiSocialCaption = "Pilih platform dan tone, lalu klik Generate.";
        [ObservableProperty] private string _aiHypnoticDesc = "Klik Generate untuk deskripsi menu donat yang menggugah selera.";
        [ObservableProperty] private string _aiImagePrompt = "Klik Generate untuk prompt gambar donat.";

        [ObservableProperty] private string _selectedSocialPlatform = "Instagram";
        [ObservableProperty] private string _selectedSocialTone = "Fun & Gaul";

        public List<string> SocialPlatforms { get; } = new() { "Instagram", "TikTok", "Facebook", "WhatsApp Blast" };
        public List<string> SocialTones { get; } = new() { "Fun & Gaul", "Elegan & Mewah", "Promo Hard Selling", "Storytelling", "Singkat & Padat" };

        [ObservableProperty] private bool _isGeneratingSocial;
        [ObservableProperty] private bool _isGeneratingHypnotic;
        [ObservableProperty] private bool _isGeneratingImagePrompt;

        // --- CONSTRUCTOR INJECTION ---
        public RecipesViewModel(AppDbContext dbContext, AIService aiService)
        {
            _dbContext = dbContext;
            _aiService = aiService;

            // INTEGRASI: Dengarkan perubahan harga dari halaman Bahan Baku
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
                await RefreshIngredientsList();
                await ReloadRecipesList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error LoadData: {ex.Message}");
            }
        }

        private async Task RefreshIngredientsList()
        {
            AvailableIngredients.Clear();
            var ingredients = await _dbContext.Ingredients.AsNoTracking().OrderBy(i => i.Name).ToListAsync();
            foreach (var i in ingredients) AvailableIngredients.Add(i);
        }

        private async Task ReloadRecipesList()
        {
            Recipes.Clear();
            _dbContext.ChangeTracker.Clear();

            var recipes = await _dbContext.Recipes
                .Include(r => r.Items).ThenInclude(i => i.Ingredient)
                .Include(r => r.Overheads)
                .AsNoTracking()
                .ToListAsync();

            foreach (var r in recipes)
            {
                Recipes.Add(r);
            }
        }

        // --- COMMANDS ---

        [RelayCommand]
        private async Task UpdateRecipeDetailsAsync()
        {
            if (SelectedRecipe == null) return;

            SelectedRecipe.LastUpdated = DateTime.Now;

            // FIX: Prioritaskan Harga Jual Manual jika ada input yang valid
            if (decimal.TryParse(ManualSellingPriceInput, out decimal manualPrice) && manualPrice > 0)
            {
                // Hitung balik margin dari harga manual ini
                RecalculateMarginFromPrice();

                // Simpan harga & margin yang baru dihitung ke object resep
                SelectedRecipe.ActualSellingPrice = manualPrice;
                SelectedRecipe.TargetMarginPercent = TargetMarginPercent;
            }
            // Jika tidak ada input valid, gunakan margin slider (fallback)
            else if (SelectedRecipe.TargetMarginPercent > 0)
            {
                // Biarkan logic slider bekerja (tetap simpan margin)
                // Harga jual akan mengikuti kalkulasi margin
            }

            _dbContext.ChangeTracker.Clear();
            _dbContext.Recipes.Update(SelectedRecipe);
            await _dbContext.SaveChangesAsync();

            await SyncSubRecipeToIngredients(SelectedRecipe);

            OnPropertyChanged(nameof(SelectedRecipe));
            NotifyRecalculation();
            await ReloadSelectedRecipe();

            // INTEGRASI: Kirim sinyal ke Dashboard
            WeakReferenceMessenger.Default.Send(new RecipesChangedMessage("Updated"));
        }

        [RelayCommand]
        private async Task DuplicateRecipeAsync(Recipe? recipe)
        {
            var target = recipe ?? SelectedRecipe;
            if (target == null) return;

            try
            {
                _dbContext.ChangeTracker.Clear();

                var source = await _dbContext.Recipes
                    .Include(r => r.Items)
                    .Include(r => r.Overheads)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == target.Id);

                if (source == null) return;

                var newRecipe = new Recipe
                {
                    Name = $"{source.Name} (Copy)",
                    Description = source.Description,
                    YieldQty = source.YieldQty,
                    TargetPortionSize = source.TargetPortionSize,
                    CookingLossPercent = source.CookingLossPercent,
                    LaborMinutes = source.LaborMinutes,
                    PrepMinutes = source.PrepMinutes,
                    ShelfLife = source.ShelfLife,
                    Version = "1.0",
                    IsSubRecipe = source.IsSubRecipe,
                    TargetMarginPercent = source.TargetMarginPercent,
                    LastUpdated = DateTime.Now
                };

                _dbContext.Recipes.Add(newRecipe);
                await _dbContext.SaveChangesAsync();

                foreach (var item in source.Items)
                {
                    _dbContext.RecipeItems.Add(new RecipeItem
                    {
                        RecipeId = newRecipe.Id,
                        IngredientId = item.IngredientId,
                        UsageQty = item.UsageQty,
                        UsageUnit = item.UsageUnit,
                        UsageCategory = item.UsageCategory,
                        IsPerPiece = item.IsPerPiece
                    });
                }

                foreach (var ov in source.Overheads)
                {
                    _dbContext.RecipeOverheads.Add(new RecipeOverhead
                    {
                        RecipeId = newRecipe.Id,
                        Name = ov.Name,
                        Cost = ov.Cost
                    });
                }

                await _dbContext.SaveChangesAsync();

                Recipes.Add(newRecipe);
                SelectedRecipe = newRecipe;

                await ReloadSelectedRecipe();
                await SyncSubRecipeToIngredients(SelectedRecipe);

                WeakReferenceMessenger.Default.Send(new RecipesChangedMessage("Created"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error Duplicate: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task DeleteRecipeAsync(Recipe? recipe)
        {
            var target = recipe ?? SelectedRecipe;
            if (target == null) return;

            try
            {
                _dbContext.ChangeTracker.Clear();

                var entry = await _dbContext.Recipes.FindAsync(target.Id);
                if (entry != null)
                {
                    _dbContext.Recipes.Remove(entry);
                    var items = _dbContext.RecipeItems.Where(ri => ri.RecipeId == target.Id);
                    _dbContext.RecipeItems.RemoveRange(items);
                    var overheads = _dbContext.RecipeOverheads.Where(ro => ro.RecipeId == target.Id);
                    _dbContext.RecipeOverheads.RemoveRange(overheads);
                    await _dbContext.SaveChangesAsync();
                }

                if (Recipes.Contains(target)) Recipes.Remove(target);

                if (SelectedRecipe == target)
                {
                    SelectedRecipe = null;
                    TargetScalingQty = 0;
                    ManualSellingPriceInput = "0";
                }

                WeakReferenceMessenger.Default.Send(new RecipesChangedMessage("Deleted"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Gagal menghapus resep: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task SaveAsNewRecipeAsync()
        {
            await DuplicateRecipeAsync(SelectedRecipe);
        }

        private void NotifyRecalculation()
        {
            OnPropertyChanged(nameof(DoughCost));
            OnPropertyChanged(nameof(ToppingCost));
            OnPropertyChanged(nameof(PackagingCost));
            OnPropertyChanged(nameof(OperationalCost));
            OnPropertyChanged(nameof(FinalHppBatch));
            OnPropertyChanged(nameof(FinalHppPerUnit));
            OnPropertyChanged(nameof(TotalMainDoughWeight));

            if (MainIngredients != null)
            {
                var flour = MainIngredients.FirstOrDefault(i => i.Ingredient != null && (i.Ingredient.Name.Contains("Tepung", StringComparison.OrdinalIgnoreCase) || i.Ingredient.Name.Contains("Terigu", StringComparison.OrdinalIgnoreCase)));
                if (flour != null)
                {
                    _targetFlourQty = flour.UsageQty;
                    OnPropertyChanged(nameof(TargetFlourQty));
                }
            }

            if (FinalHppPerUnit > 0)
            {
                decimal currentPrice = 0;
                decimal.TryParse(ManualSellingPriceInput, out currentPrice);

                if (currentPrice == 0)
                {
                    OnTargetMarginPercentChanged(TargetMarginPercent);
                }
                else
                {
                    RecalculateMarginFromPrice();
                }
            }

            OnPropertyChanged(nameof(ManualSellingPriceInput));
            OnPropertyChanged(nameof(RecommendedSellingPrice));
            OnPropertyChanged(nameof(EstimatedProfitPerUnit));
            OnPropertyChanged(nameof(EstimatedProfitPerBatch));
            OnPropertyChanged(nameof(BreakEvenPointQty));
            OnPropertyChanged(nameof(ProfitMarginDisplay));

            OnPropertyChanged(nameof(MainIngredients));
            OnPropertyChanged(nameof(SupportIngredients));
            OnPropertyChanged(nameof(PackagingItems));
        }

        [RelayCommand]
        private async Task ScaleRecipeAsync()
        {
            if (SelectedRecipe == null || TargetScalingQty <= 0) return;
            double ratio = (double)TargetScalingQty / SelectedRecipe.YieldQty;

            _dbContext.ChangeTracker.Clear();

            foreach (var item in SelectedRecipe.Items)
            {
                if (!item.IsPerPiece)
                {
                    item.UsageQty *= ratio;
                    _dbContext.RecipeItems.Update(item);
                }
            }

            SelectedRecipe.YieldQty = TargetScalingQty;
            _dbContext.Recipes.Update(SelectedRecipe);
            await _dbContext.SaveChangesAsync();

            await SyncSubRecipeToIngredients(SelectedRecipe);
            await ReloadSelectedRecipe();

            WeakReferenceMessenger.Default.Send(new RecipesChangedMessage("Updated"));
        }

        [RelayCommand]
        private async Task ResizeBatchByFlourAsync()
        {
            if (SelectedRecipe == null || TargetFlourQty <= 0) return;

            var flourItem = MainIngredients.FirstOrDefault(i =>
                i.Ingredient.Name.Contains("Tepung", StringComparison.OrdinalIgnoreCase) ||
                i.Ingredient.Name.Contains("Terigu", StringComparison.OrdinalIgnoreCase) ||
                i.Ingredient.Name.Contains("Flour", StringComparison.OrdinalIgnoreCase));

            if (flourItem == null && MainIngredients.Any())
                flourItem = MainIngredients.OrderByDescending(i => i.UsageQty).First();

            if (flourItem == null || flourItem.UsageQty <= 0) return;

            double ratio = TargetFlourQty / flourItem.UsageQty;
            if (ratio == 1 || ratio <= 0) return;

            _dbContext.ChangeTracker.Clear();

            foreach (var item in MainIngredients)
            {
                if (!item.IsPerPiece)
                {
                    item.UsageQty *= ratio;
                    _dbContext.RecipeItems.Update(item);
                }
            }

            SelectedRecipe.YieldQty = (int)Math.Round(SelectedRecipe.YieldQty * ratio);
            if (SelectedRecipe.YieldQty < 1) SelectedRecipe.YieldQty = 1;

            _dbContext.Recipes.Update(SelectedRecipe);
            await _dbContext.SaveChangesAsync();

            await SyncSubRecipeToIngredients(SelectedRecipe);
            await ReloadSelectedRecipe();
            TargetScalingQty = SelectedRecipe.YieldQty;

            WeakReferenceMessenger.Default.Send(new RecipesChangedMessage("Updated"));
        }

        [RelayCommand]
        private async Task RecalculateYieldFromMassAsync()
        {
            if (SelectedRecipe == null || SelectedRecipe.TargetPortionSize <= 0) return;
            double totalRawMass = TotalMainDoughWeight;
            if (totalRawMass > 0)
            {
                double lossFactor = 1 - (SelectedRecipe.CookingLossPercent / 100.0);
                double netMass = totalRawMass * lossFactor;
                int newYield = (int)(netMass / SelectedRecipe.TargetPortionSize);
                if (newYield < 1) newYield = 1;

                _dbContext.ChangeTracker.Clear();

                SelectedRecipe.YieldQty = newYield;
                _dbContext.Recipes.Update(SelectedRecipe);
                await _dbContext.SaveChangesAsync();

                await SyncSubRecipeToIngredients(SelectedRecipe);
                await ReloadSelectedRecipe();

                WeakReferenceMessenger.Default.Send(new RecipesChangedMessage("Updated"));
            }
        }

        [RelayCommand]
        private async Task AddItemToRecipeAsync()
        {
            if (SelectedRecipe == null || SelectedIngredientToAdd == null) return;
            if (double.TryParse(UsageQtyInput, out var qty) && qty > 0)
            {
                _dbContext.ChangeTracker.Clear();

                var newItem = new RecipeItem
                {
                    RecipeId = SelectedRecipe.Id,
                    IngredientId = SelectedIngredientToAdd.Id,
                    UsageQty = qty,
                    UsageUnit = SelectedUsageUnit,
                    UsageCategory = SelectedCategoryToAdd,
                    IsPerPiece = IsInputPerPiece
                };
                _dbContext.RecipeItems.Add(newItem);
                await _dbContext.SaveChangesAsync();
                await ReloadSelectedRecipe();
                await SyncSubRecipeToIngredients(SelectedRecipe);
                UsageQtyInput = "0";

                WeakReferenceMessenger.Default.Send(new RecipesChangedMessage("Updated"));
            }
        }

        [RelayCommand]
        private async Task RemoveItemFromRecipeAsync(RecipeItem? item)
        {
            if (item == null) return;

            _dbContext.ChangeTracker.Clear();

            _dbContext.RecipeItems.Remove(item);
            await _dbContext.SaveChangesAsync();
            await ReloadSelectedRecipe();
            if (SelectedRecipe != null) await SyncSubRecipeToIngredients(SelectedRecipe);

            WeakReferenceMessenger.Default.Send(new RecipesChangedMessage("Updated"));
        }

        [RelayCommand]
        private async Task AddOverheadAsync()
        {
            if (SelectedRecipe == null || string.IsNullOrWhiteSpace(NewOverheadName)) return;
            if (decimal.TryParse(NewOverheadCost, out var cost) && cost > 0 && double.TryParse(UsageCycles, out var cycles) && cycles > 0)
            {
                _dbContext.ChangeTracker.Clear();

                decimal finalCost = cost / (decimal)cycles;
                var overhead = new RecipeOverhead { RecipeId = SelectedRecipe.Id, Name = NewOverheadName + (cycles > 1 ? $" (1/{cycles} siklus)" : ""), Cost = finalCost };
                _dbContext.RecipeOverheads.Add(overhead);
                await _dbContext.SaveChangesAsync();
                await ReloadSelectedRecipe();
                if (SelectedRecipe != null) await SyncSubRecipeToIngredients(SelectedRecipe);
                NewOverheadName = ""; NewOverheadCost = ""; UsageCycles = "1";

                WeakReferenceMessenger.Default.Send(new RecipesChangedMessage("Updated"));
            }
        }

        [RelayCommand]
        private async Task RemoveOverheadAsync(RecipeOverhead? item)
        {
            if (item == null) return;

            _dbContext.ChangeTracker.Clear();

            _dbContext.RecipeOverheads.Remove(item);
            await _dbContext.SaveChangesAsync();
            await ReloadSelectedRecipe();
            if (SelectedRecipe != null) await SyncSubRecipeToIngredients(SelectedRecipe);

            WeakReferenceMessenger.Default.Send(new RecipesChangedMessage("Updated"));
        }

        [RelayCommand]
        private async Task CreateRecipeAsync()
        {
            if (string.IsNullOrWhiteSpace(NewRecipeName)) return;

            _dbContext.ChangeTracker.Clear();

            var newRecipe = new Recipe { Name = NewRecipeName, YieldQty = 1, Version = "1.0", LastUpdated = DateTime.Now };
            _dbContext.Recipes.Add(newRecipe);
            await _dbContext.SaveChangesAsync();
            Recipes.Add(newRecipe);
            SelectedRecipe = newRecipe;
            NewRecipeName = "";

            WeakReferenceMessenger.Default.Send(new RecipesChangedMessage("Created"));
        }

        // --- AI FEATURES ---
        [RelayCommand]
        private async Task GenerateDescriptionAsync()
        {
            if (SelectedRecipe == null) return;
            IsAiLoading = true;
            try
            {
                var sb = new StringBuilder();
                foreach (var item in SelectedRecipe.Items) sb.Append($"{item.Ingredient?.Name} ({item.UsageQty} {item.UsageUnit}), ");
                var result = await _aiService.GenerateMarketingCopyAsync(SelectedRecipe.Name, sb.ToString().TrimEnd(',', ' '));

                _dbContext.ChangeTracker.Clear();

                SelectedRecipe.Description = result;
                _dbContext.Recipes.Update(SelectedRecipe);
                await _dbContext.SaveChangesAsync();
                OnPropertyChanged(nameof(SelectedRecipe));
            }
            catch { }
            finally { IsAiLoading = false; }
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
                        _dbContext.ChangeTracker.Clear();

                        foreach (var item in aiItems)
                        {
                            var existingIngredient = await _dbContext.Ingredients.FirstOrDefaultAsync(i => i.Name.ToLower() == item.IngredientName.ToLower());
                            Ingredient ingredientToUse;
                            if (existingIngredient != null) ingredientToUse = existingIngredient;
                            else
                            {
                                var newIng = new Ingredient { Name = item.IngredientName, PricePerPackage = item.EstimatedPrice, QuantityPerPackage = item.PackageQty, Unit = item.PackageUnit, YieldPercent = 100, Category = "Auto" };
                                _dbContext.Ingredients.Add(newIng);
                                await _dbContext.SaveChangesAsync();
                                AvailableIngredients.Add(newIng);
                                ingredientToUse = newIng;
                            }
                            var recipeItem = new RecipeItem { RecipeId = SelectedRecipe.Id, IngredientId = ingredientToUse.Id, UsageQty = item.UsageQty, UsageUnit = item.UsageUnit, UsageCategory = "Main" };
                            _dbContext.RecipeItems.Add(recipeItem);
                        }
                        await _dbContext.SaveChangesAsync();
                        await ReloadSelectedRecipe();
                        await SyncSubRecipeToIngredients(SelectedRecipe);

                        WeakReferenceMessenger.Default.Send(new RecipesChangedMessage("Updated"));
                    }
                }
            }
            catch { }
            finally { IsAiLoading = false; }
        }

        [RelayCommand]
        private async Task AuditRecipeCostAsync()
        {
            if (SelectedRecipe == null) return;
            IsAuditing = true;
            AiAuditReport = "Sedang menganalisa resep...";
            try { AiAuditReport = await _aiService.AuditRecipeCostAsync(SelectedRecipe); }
            catch (Exception ex) { AiAuditReport = $"Error: {ex.Message}"; }
            finally { IsAuditing = false; }
        }

        [RelayCommand]
        private async Task GetSubstitutionIdeasAsync()
        {
            if (SelectedRecipe == null) return;
            IsSubstituting = true;
            AiSubstitutionReport = "Sedang mencari alternatif...";
            try { AiSubstitutionReport = await _aiService.GetSmartSubstitutionsAsync(SelectedRecipe); }
            catch (Exception ex) { AiSubstitutionReport = $"Error: {ex.Message}"; }
            finally { IsSubstituting = false; }
        }

        [RelayCommand]
        private async Task GetWasteIdeasAsync()
        {
            if (SelectedRecipe == null) return;
            IsWasteAnalyzing = true;
            AiWasteReport = "Sedang menganalisa limbah...";
            try { AiWasteReport = await _aiService.GetWasteReductionIdeasAsync(SelectedRecipe); }
            catch (Exception ex) { AiWasteReport = $"Error: {ex.Message}"; }
            finally { IsWasteAnalyzing = false; }
        }

        [RelayCommand]
        private async Task GenerateSocialMediaAsync()
        {
            if (SelectedRecipe == null) return;
            IsGeneratingSocial = true;
            try { AiSocialCaption = await _aiService.GenerateSocialMediaCaptionAsync(SelectedRecipe, SelectedSocialPlatform, SelectedSocialTone); }
            catch (Exception ex) { AiSocialCaption = $"Error: {ex.Message}"; }
            finally { IsGeneratingSocial = false; }
        }

        [RelayCommand]
        private async Task GenerateHypnoticDescAsync()
        {
            if (SelectedRecipe == null) return;
            IsGeneratingHypnotic = true;
            try { AiHypnoticDesc = await _aiService.GenerateHypnoticDescriptionAsync(SelectedRecipe); }
            catch (Exception ex) { AiHypnoticDesc = $"Error: {ex.Message}"; }
            finally { IsGeneratingHypnotic = false; }
        }

        [RelayCommand]
        private async Task GenerateImagePromptAsync()
        {
            if (SelectedRecipe == null) return;
            IsGeneratingImagePrompt = true;
            try { AiImagePrompt = await _aiService.GenerateImagePromptAsync(SelectedRecipe); }
            catch (Exception ex) { AiImagePrompt = $"Error: {ex.Message}"; }
            finally { IsGeneratingImagePrompt = false; }
        }

        private async Task SyncSubRecipeToIngredients(Recipe recipe)
        {
            if (recipe == null) return;

            _dbContext.ChangeTracker.Clear();

            var linkedIngredient = await _dbContext.Ingredients.FirstOrDefaultAsync(i => i.LinkedRecipeId == recipe.Id);

            if (recipe.IsSubRecipe)
            {
                if (linkedIngredient == null)
                {
                    linkedIngredient = new Ingredient { LinkedRecipeId = recipe.Id, Category = "Sub-Recipe" };
                    _dbContext.Ingredients.Add(linkedIngredient);
                }
                linkedIngredient.Name = $"[Resep] {recipe.Name}";
                linkedIngredient.PricePerPackage = FinalHppPerUnit;
                linkedIngredient.QuantityPerPackage = 1;
                linkedIngredient.Unit = "Porsi";
                linkedIngredient.YieldPercent = 100;
                await _dbContext.SaveChangesAsync();
            }
            else
            {
                if (linkedIngredient != null)
                {
                    _dbContext.Ingredients.Remove(linkedIngredient);
                    await _dbContext.SaveChangesAsync();
                }
            }
            await RefreshIngredientsList();
        }

        partial void OnSelectedIngredientToAddChanged(Ingredient? value)
        {
            if (value != null) SelectedUsageUnit = value.Unit;
        }

        private async Task ReloadSelectedRecipe()
        {
            if (SelectedRecipe == null) return;
            var id = SelectedRecipe.Id;

            _dbContext.ChangeTracker.Clear();

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
                TargetScalingQty = updatedRecipe.YieldQty;
                NotifyRecalculation();
            }
        }

        public void OnPriceBoxKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                _ = UpdateRecipeDetailsAsync();
            }
        }
    }
}