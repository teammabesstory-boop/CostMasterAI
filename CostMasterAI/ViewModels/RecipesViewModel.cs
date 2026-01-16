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
        // Update Analisa
        [NotifyPropertyChangedFor(nameof(EstimatedProfitPerUnit))]
        [NotifyPropertyChangedFor(nameof(EstimatedProfitPerBatch))]
        [NotifyPropertyChangedFor(nameof(BreakEvenPointQty))]
        // Update Lists
        [NotifyPropertyChangedFor(nameof(MainIngredients))]
        [NotifyPropertyChangedFor(nameof(SupportIngredients))]
        [NotifyPropertyChangedFor(nameof(PackagingItems))]
        private Recipe? _selectedRecipe;

        [ObservableProperty] private Ingredient? _selectedIngredientToAdd;
        [ObservableProperty] private string _usageQtyInput = "0";
        [ObservableProperty] private string _selectedUsageUnit = "Gram";
        [ObservableProperty] private string _newRecipeName = "";

        [ObservableProperty] private string _newOverheadName = "";
        [ObservableProperty] private string _newOverheadCost = "";
        [ObservableProperty] private string _usageCycles = "1";

        [ObservableProperty] private int _targetScalingQty;
        [ObservableProperty] private string _selectedCategoryToAdd = "Bahan Utama";
        [ObservableProperty] private bool _isInputPerPiece;

        public List<string> RecipeCategories { get; } = new()
        {
            "Bahan Utama", "Bahan Penolong", "Kemasan"
        };

        // --- ADVANCED ANALYSIS PROPERTIES ---

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FinalHppBatch))]
        [NotifyPropertyChangedFor(nameof(FinalHppPerUnit))]
        [NotifyPropertyChangedFor(nameof(EstimatedProfitPerUnit))]
        private double _wasteBufferPercent = 5.0;

        // FLAG: Untuk mencegah loop perhitungan
        private bool _isUpdatingFromPrice = false;

        // 2. Target Margin (Slider)
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ProfitMarginDisplay))]
        [NotifyPropertyChangedFor(nameof(EstimatedProfitPerUnit))]
        [NotifyPropertyChangedFor(nameof(EstimatedProfitPerBatch))]
        [NotifyPropertyChangedFor(nameof(BreakEvenPointQty))]
        private double _targetMarginPercent = 40.0;

        // Dipanggil saat Slider DIGESER
        partial void OnTargetMarginPercentChanged(double value)
        {
            // JIKA sedang update dari input harga manual, JANGAN hitung balik harga
            if (_isUpdatingFromPrice) return;

            if (FinalHppPerUnit > 0)
            {
                // Hitung Harga Jual berdasarkan Margin Baru
                decimal marginFactor = 1 - (decimal)(value / 100.0);
                if (marginFactor <= 0.01m) marginFactor = 0.01m;

                decimal preTaxPrice = FinalHppPerUnit / marginFactor;
                decimal taxAmount = preTaxPrice * (decimal)(TaxPercent / 100.0);

                // Update Textbox Harga (Dibulatkan ke 100 terdekat)
                _manualSellingPriceInput = RoundUpToNearestHundred(preTaxPrice + taxAmount);
                OnPropertyChanged(nameof(ManualSellingPriceInput));
            }
        }

        // 3. Tax / Pajak
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EstimatedProfitPerUnit))]
        private double _taxPercent = 0.0;

        partial void OnTaxPercentChanged(double value)
        {
            OnTargetMarginPercentChanged(TargetMarginPercent);
        }

        // 4. Manual Selling Price (Inputan User)
        [ObservableProperty]
        private decimal _manualSellingPriceInput;

        // METHOD INI DIPANGGIL SAAT ENTER DI TEXTBOX HARGA
        [RelayCommand]
        private void RecalculateMarginFromPrice()
        {
            if (ManualSellingPriceInput <= 0 || FinalHppPerUnit <= 0) return;

            // 1. KUNCI PINTU (Agar slider tidak merubah harga yang barusan kita ketik)
            _isUpdatingFromPrice = true;

            // 2. Hitung Margin Mundur
            decimal taxFactor = 1 + (decimal)(TaxPercent / 100.0);
            decimal cleanPrice = ManualSellingPriceInput / taxFactor;

            if (cleanPrice > 0)
            {
                decimal newMarginDecimal = (cleanPrice - FinalHppPerUnit) / cleanPrice;
                double newMarginPercent = (double)(newMarginDecimal * 100);

                // Clamp
                if (newMarginPercent < 0) newMarginPercent = 0;
                if (newMarginPercent > 99.9) newMarginPercent = 99.9;

                // Set Slider (UI Slider akan bergerak, tapi tidak akan trigger OnTargetMarginPercentChanged logic)
                TargetMarginPercent = Math.Round(newMarginPercent, 1);
            }

            // 3. BUKA KUNCI
            _isUpdatingFromPrice = false;

            // 4. Update UI Profit
            OnPropertyChanged(nameof(EstimatedProfitPerUnit));
            OnPropertyChanged(nameof(EstimatedProfitPerBatch));
            OnPropertyChanged(nameof(BreakEvenPointQty));
            OnPropertyChanged(nameof(ProfitMarginDisplay));
        }

        // --- HELPER: PEMBULATAN HARGA ---
        private decimal RoundUpToNearestHundred(decimal price)
        {
            return Math.Ceiling(price / 100m) * 100m;
        }

        // --- UPDATED GROUPING LISTS ---
        public IEnumerable<RecipeItem> MainIngredients => _selectedRecipe?.Items.Where(i => i.UsageCategory == "Bahan Utama") ?? Enumerable.Empty<RecipeItem>();
        public IEnumerable<RecipeItem> SupportIngredients => _selectedRecipe?.Items.Where(i => i.UsageCategory == "Bahan Penolong") ?? Enumerable.Empty<RecipeItem>();
        public IEnumerable<RecipeItem> PackagingItems => _selectedRecipe?.Items.Where(i => i.UsageCategory == "Kemasan") ?? Enumerable.Empty<RecipeItem>();

        // Calculated Properties
        public decimal DoughCost => MainIngredients.Sum(i => i.CalculatedCost);
        public decimal ToppingCost => SupportIngredients.Sum(i => i.CalculatedCost);
        public decimal PackagingCost => PackagingItems.Sum(i => i.CalculatedCost);
        public decimal OperationalCost => (SelectedRecipe?.TotalOverheadCost ?? 0) + (SelectedRecipe?.TotalLaborCost ?? 0);

        // --- TOTAL BERAT ADONAN ---
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

        // --- PROFITABILITY METRICS FOR UI ---

        public decimal RecommendedSellingPrice => ManualSellingPriceInput;

        public decimal EstimatedProfitPerUnit
        {
            get
            {
                if (FinalHppPerUnit <= 0) return 0;
                // Hitung Profit dari Harga Manual saat ini
                decimal taxFactor = 1 + (decimal)(TaxPercent / 100.0);
                decimal cleanPrice = ManualSellingPriceInput / taxFactor;
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
                return (int)Math.Ceiling(FinalHppBatch / (ManualSellingPriceInput > 0 ? ManualSellingPriceInput : 1));
            }
        }

        public string ProfitMarginDisplay => $"{TargetMarginPercent:N1}%";

        public decimal RecommendedPriceMargin => FinalHppPerUnit > 0 ? FinalHppPerUnit / 0.6m : 0;
        public decimal RecommendedPriceMarkup => FinalHppPerUnit * 2;

        // --- GENERATE DETAIL REPORT ---
        public string GenerateCostDetailString()
        {
            if (SelectedRecipe == null) return "Data tidak tersedia.";

            var sb = new StringBuilder();

            decimal baseDough = DoughCost;
            decimal baseTopping = ToppingCost;
            decimal basePack = PackagingCost;
            decimal baseOps = OperationalCost;
            decimal totalBase = baseDough + baseTopping + basePack + baseOps;

            decimal wasteValue = totalBase * (decimal)(WasteBufferPercent / 100.0);
            decimal totalWithWaste = totalBase + wasteValue;

            decimal hppUnit = 0;
            if (SelectedRecipe.YieldQty > 0)
                hppUnit = totalWithWaste / SelectedRecipe.YieldQty;

            decimal currentPrice = ManualSellingPriceInput;
            decimal taxFactor = 1 + (decimal)(TaxPercent / 100.0);
            decimal pricePreTax = currentPrice / taxFactor;
            decimal taxValue = currentPrice - pricePreTax;
            decimal profitValue = pricePreTax - hppUnit;

            sb.AppendLine("=== 1. STRUKTUR BIAYA DASAR (BATCH) ===");
            sb.AppendLine($"• Adonan\t: {baseDough:C0}");
            sb.AppendLine($"• Topping\t: {baseTopping:C0}");
            sb.AppendLine($"• Kemasan\t: {basePack:C0}");
            sb.AppendLine($"• Overhead\t: {baseOps:C0}");
            sb.AppendLine($"----------------------------------------");
            sb.AppendLine($"TOTAL MODAL AWAL\t: {totalBase:C0}");
            sb.AppendLine();

            sb.AppendLine($"=== 2. RISIKO & WASTE ({WasteBufferPercent}%) ===");
            sb.AppendLine($"• Estimasi Buang\t: {wasteValue:C0}");
            sb.AppendLine($"----------------------------------------");
            sb.AppendLine($"MODAL SETELAH WASTE\t: {totalWithWaste:C0}");
            sb.AppendLine();

            sb.AppendLine("=== 3. HARGA POKOK (HPP) ===");
            sb.AppendLine($"• Total Modal / {SelectedRecipe.YieldQty} Pcs");
            sb.AppendLine($"• HPP BERSIH PER PCS\t: {hppUnit:C0}");
            sb.AppendLine();

            sb.AppendLine($"=== 4. ANALISA HARGA JUAL ===");
            sb.AppendLine($"• Harga Jual Final\t: {currentPrice:C0}");
            sb.AppendLine($"• Pajak ({TaxPercent}%)\t: -{taxValue:C0}");
            sb.AppendLine($"• Pendapatan Bersih\t: {pricePreTax:C0}");
            sb.AppendLine($"• HPP per Unit\t: -{hppUnit:C0}");
            sb.AppendLine($"----------------------------------------");
            sb.AppendLine($"PROFIT BERSIH\t: {profitValue:C0} / pcs");
            sb.AppendLine($"MARGIN AKTUAL\t: {TargetMarginPercent:N2}%");

            return sb.ToString();
        }

        // --- AI PROPERTIES (SAMA) ---
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
            var ingredients = await _dbContext.Ingredients.AsNoTracking().ToListAsync();
            AvailableIngredients.Clear();
            foreach (var i in ingredients) AvailableIngredients.Add(i);
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

        private async Task SyncSubRecipeToIngredients(Recipe recipe)
        {
            if (recipe == null) return;

            var linkedIngredient = await _dbContext.Ingredients
                .FirstOrDefaultAsync(i => i.LinkedRecipeId == recipe.Id);

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

        [RelayCommand]
        private async Task UpdateRecipeDetailsAsync()
        {
            if (SelectedRecipe == null) return;

            SelectedRecipe.LastUpdated = DateTime.Now;

            if (SelectedRecipe.TargetMarginPercent > 0)
            {
                SelectedRecipe.ActualSellingPrice = ManualSellingPriceInput;
            }

            _dbContext.Recipes.Update(SelectedRecipe);
            await _dbContext.SaveChangesAsync();

            await SyncSubRecipeToIngredients(SelectedRecipe);

            OnPropertyChanged(nameof(SelectedRecipe));
            NotifyRecalculation();
            await ReloadSelectedRecipe();
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

            // Logic trigger awal
            if (FinalHppPerUnit > 0 && ManualSellingPriceInput == 0)
            {
                OnTargetMarginPercentChanged(TargetMarginPercent);
            }
            else if (FinalHppPerUnit > 0 && ManualSellingPriceInput > 0)
            {
                RecalculateMarginFromPrice();
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

            foreach (var item in SelectedRecipe.Items)
            {
                if (!item.IsPerPiece && !item.IsUnitBased)
                {
                    item.UsageQty = item.UsageQty * ratio;
                }
            }

            if (ratio > 1)
            {
                SelectedRecipe.LaborMinutes = SelectedRecipe.LaborMinutes * ratio * 0.8;
                SelectedRecipe.PrepMinutes = (int)(SelectedRecipe.PrepMinutes * ratio * 0.8);
            }
            else
            {
                SelectedRecipe.LaborMinutes = SelectedRecipe.LaborMinutes * ratio;
                SelectedRecipe.PrepMinutes = (int)(SelectedRecipe.PrepMinutes * ratio);
            }

            SelectedRecipe.YieldQty = TargetScalingQty;
            _dbContext.Recipes.Update(SelectedRecipe);
            await _dbContext.SaveChangesAsync();

            await SyncSubRecipeToIngredients(SelectedRecipe);
            await ReloadSelectedRecipe();
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
                SelectedRecipe.YieldQty = newYield;
                _dbContext.Recipes.Update(SelectedRecipe);
                await _dbContext.SaveChangesAsync();
                await SyncSubRecipeToIngredients(SelectedRecipe);
                await ReloadSelectedRecipe();
            }
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
                    IsUnitBased = false,
                    UsageCategory = SelectedCategoryToAdd,
                    IsPerPiece = IsInputPerPiece
                };
                _dbContext.RecipeItems.Add(newItem);
                await _dbContext.SaveChangesAsync();
                await ReloadSelectedRecipe();
                await SyncSubRecipeToIngredients(SelectedRecipe);
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
            if (SelectedRecipe != null) await SyncSubRecipeToIngredients(SelectedRecipe);
        }

        [RelayCommand]
        private async Task AddOverheadAsync()
        {
            if (SelectedRecipe == null || string.IsNullOrWhiteSpace(NewOverheadName)) return;
            if (decimal.TryParse(NewOverheadCost, out var cost) && cost > 0 && double.TryParse(UsageCycles, out var cycles) && cycles > 0)
            {
                decimal finalCost = cost / (decimal)cycles;
                var overhead = new RecipeOverhead { RecipeId = SelectedRecipe.Id, Name = NewOverheadName + (cycles > 1 ? $" (1/{cycles} siklus)" : ""), Cost = finalCost };
                _dbContext.RecipeOverheads.Add(overhead);
                await _dbContext.SaveChangesAsync();
                await ReloadSelectedRecipe();
                if (SelectedRecipe != null) await SyncSubRecipeToIngredients(SelectedRecipe);
                NewOverheadName = ""; NewOverheadCost = ""; UsageCycles = "1";
            }
        }

        [RelayCommand]
        private async Task RemoveOverheadAsync(RecipeOverhead? item)
        {
            if (item == null) return;
            _dbContext.RecipeOverheads.Remove(item);
            await _dbContext.SaveChangesAsync();
            await ReloadSelectedRecipe();
            if (SelectedRecipe != null) await SyncSubRecipeToIngredients(SelectedRecipe);
        }

        [RelayCommand]
        private async Task ToggleItemUnitBasedAsync(RecipeItem item)
        {
            if (item == null) return;
            _dbContext.RecipeItems.Update(item);
            await _dbContext.SaveChangesAsync();
            await ReloadSelectedRecipe();
            if (SelectedRecipe != null) await SyncSubRecipeToIngredients(SelectedRecipe);
        }

        [RelayCommand]
        private async Task CreateRecipeAsync()
        {
            if (string.IsNullOrWhiteSpace(NewRecipeName)) return;
            var newRecipe = new Recipe { Name = NewRecipeName, YieldQty = 1, Version = "1.0", LastUpdated = DateTime.Now };
            _dbContext.Recipes.Add(newRecipe);
            await _dbContext.SaveChangesAsync();
            Recipes.Add(newRecipe);
            SelectedRecipe = newRecipe;
            NewRecipeName = "";
        }

        // --- COMMANDS: AI FEATURES (SAMA) ---
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
                        await SyncSubRecipeToIngredients(SelectedRecipe);
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
            try
            {
                AiAuditReport = await _aiService.AuditRecipeCostAsync(SelectedRecipe);
            }
            catch (Exception ex)
            {
                AiAuditReport = $"Gagal menghubungi AI: {ex.Message}";
            }
            finally
            {
                IsAuditing = false;
            }
        }

        [RelayCommand]
        private async Task GetSubstitutionIdeasAsync()
        {
            if (SelectedRecipe == null) return;
            IsSubstituting = true;
            AiSubstitutionReport = "Sedang mencari alternatif bahan...";
            try
            {
                AiSubstitutionReport = await _aiService.GetSmartSubstitutionsAsync(SelectedRecipe);
            }
            catch (Exception ex)
            {
                AiSubstitutionReport = $"Gagal menghubungi AI: {ex.Message}";
            }
            finally
            {
                IsSubstituting = false;
            }
        }

        [RelayCommand]
        private async Task GetWasteIdeasAsync()
        {
            if (SelectedRecipe == null) return;
            IsWasteAnalyzing = true;
            AiWasteReport = "Sedang menganalisa potensi limbah...";
            try
            {
                AiWasteReport = await _aiService.GetWasteReductionIdeasAsync(SelectedRecipe);
            }
            catch (Exception ex)
            {
                AiWasteReport = $"Gagal menghubungi AI: {ex.Message}";
            }
            finally
            {
                IsWasteAnalyzing = false;
            }
        }

        [RelayCommand]
        private async Task GenerateSocialMediaAsync()
        {
            if (SelectedRecipe == null) return;
            IsGeneratingSocial = true;
            AiSocialCaption = "Sedang meracik kata-kata viral...";
            try
            {
                AiSocialCaption = await _aiService.GenerateSocialMediaCaptionAsync(SelectedRecipe, SelectedSocialPlatform, SelectedSocialTone);
            }
            catch (Exception ex)
            {
                AiSocialCaption = $"Error: {ex.Message}";
            }
            finally
            {
                IsGeneratingSocial = false;
            }
        }

        [RelayCommand]
        private async Task GenerateHypnoticDescAsync()
        {
            if (SelectedRecipe == null) return;
            IsGeneratingHypnotic = true;
            AiHypnoticDesc = "Sedang menyusun kalimat hipnotis...";
            try
            {
                AiHypnoticDesc = await _aiService.GenerateHypnoticDescriptionAsync(SelectedRecipe);
            }
            catch (Exception ex)
            {
                AiHypnoticDesc = $"Error: {ex.Message}";
            }
            finally
            {
                IsGeneratingHypnotic = false;
            }
        }

        [RelayCommand]
        private async Task GenerateImagePromptAsync()
        {
            if (SelectedRecipe == null) return;
            IsGeneratingImagePrompt = true;
            AiImagePrompt = "Sedang membayangkan visual terbaik...";
            try
            {
                AiImagePrompt = await _aiService.GenerateImagePromptAsync(SelectedRecipe);
            }
            catch (Exception ex)
            {
                AiImagePrompt = $"Error: {ex.Message}";
            }
            finally
            {
                IsGeneratingImagePrompt = false;
            }
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
    }
}