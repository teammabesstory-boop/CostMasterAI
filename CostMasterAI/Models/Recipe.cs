using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CostMasterAI.Models
{
    public class Recipe : ObservableObject
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // --- SUB-RECIPE FEATURE (BARU) ---
        // Jika True, resep ini akan muncul sebagai Bahan Baku di resep lain.
        private bool _isSubRecipe;
        public bool IsSubRecipe
        {
            get => _isSubRecipe;
            set => SetProperty(ref _isSubRecipe, value);
        }

        // --- 1. META DATA ---
        public string Version { get; set; } = "1.0";
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public string ShelfLife { get; set; } = "1 Hari";
        public int PrepMinutes { get; set; } = 0;

        // --- 2. PRODUCTION VARIABLES ---
        public int YieldQty { get; set; } = 1;
        public double TargetPortionSize { get; set; } = 0;
        public double CookingLossPercent { get; set; } = 0;

        // --- 3. OPERATIONAL ---
        public double LaborMinutes { get; set; } = 0;
        public decimal LaborHourlyRate { get; set; } = 0;

        // --- 4. BUSINESS & PRICING ---
        private double _targetMarginPercent = 50;
        public double TargetMarginPercent
        {
            get => _targetMarginPercent;
            set
            {
                if (SetProperty(ref _targetMarginPercent, value))
                {
                    NotifyPricingUpdates();
                }
            }
        }

        public int EstMonthlySales { get; set; } = 0;

        private decimal _actualSellingPrice = 0;
        public decimal ActualSellingPrice
        {
            get => _actualSellingPrice;
            set
            {
                if (SetProperty(ref _actualSellingPrice, value))
                {
                    NotifyPricingUpdates();
                }
            }
        }

        private double _onlineMarkupPercent = 20;
        public double OnlineMarkupPercent
        {
            get => _onlineMarkupPercent;
            set
            {
                if (SetProperty(ref _onlineMarkupPercent, value))
                {
                    OnPropertyChanged(nameof(OnlineAppPrice));
                }
            }
        }

        // Relasi
        public List<RecipeItem> Items { get; set; } = new();
        public List<RecipeOverhead> Overheads { get; set; } = new();

        // --- CALCULATED COST ---
        public decimal TotalMaterialCost => Items?.Sum(i => i.CalculatedCost) ?? 0;
        public decimal TotalOverheadCost => Overheads?.Sum(o => o.Cost) ?? 0;
        public decimal TotalLaborCost => (decimal)((LaborMinutes + PrepMinutes) / 60.0) * LaborHourlyRate;

        public decimal TotalBatchCost => TotalMaterialCost + TotalOverheadCost + TotalLaborCost;

        public decimal TotalCost => TotalBatchCost; // Alias

        public decimal CostPerUnit => YieldQty > 0 ? TotalBatchCost / (decimal)YieldQty : 0;

        // --- PRICING OUTPUT ---
        public decimal DineInPrice
        {
            get
            {
                if (ActualSellingPrice > 0) return ActualSellingPrice;
                if (CostPerUnit <= 0) return 0;
                if (TargetMarginPercent >= 99) return CostPerUnit * 100;
                decimal marginDecimal = (decimal)TargetMarginPercent / 100m;
                return CostPerUnit / (1 - marginDecimal);
            }
        }

        public decimal OnlineAppPrice => DineInPrice * (decimal)(1 + (OnlineMarkupPercent / 100.0));
        public decimal WholesalePrice => CostPerUnit * 1.2m;

        // --- METRICS ---
        public double FoodCostPercentage
        {
            get
            {
                if (DineInPrice <= 0) return 0;
                return Math.Round((double)(CostPerUnit / DineInPrice) * 100, 1);
            }
        }

        public double RealMarginPercent
        {
            get
            {
                if (DineInPrice <= 0) return 0;
                return (double)((DineInPrice - CostPerUnit) / DineInPrice) * 100;
            }
        }

        public decimal GrossProfitPerUnit => DineInPrice - CostPerUnit;
        public decimal SuggestedPrice => DineInPrice;

        // Helper biar kodingan rapi
        private void NotifyPricingUpdates()
        {
            OnPropertyChanged(nameof(DineInPrice));
            OnPropertyChanged(nameof(OnlineAppPrice));
            OnPropertyChanged(nameof(SuggestedPrice));
            OnPropertyChanged(nameof(FoodCostPercentage));
            OnPropertyChanged(nameof(RealMarginPercent));
            OnPropertyChanged(nameof(GrossProfitPerUnit));
        }
    }
}