using System;
using System.ComponentModel.DataAnnotations;

namespace CostMasterAI.Models
{
    public class Ingredient
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public decimal PricePerPackage { get; set; } // Harga Beli / HPP Resep
        public double QuantityPerPackage { get; set; } // Berat Beli / Yield Resep

        public string Unit { get; set; } = "Gram";
        public string Category { get; set; } = "General";
        public double YieldPercent { get; set; } = 100;

        // --- SUB-RECIPE LINK (BARU) ---
        // Jika tidak null, berarti ingredient ini adalah representasi dari sebuah Resep (Sub-Recipe)
        public int? LinkedRecipeId { get; set; }

        public decimal RealCostPerUnit
        {
            get
            {
                if (QuantityPerPackage <= 0 || YieldPercent <= 0) return 0;
                var usableQty = (decimal)QuantityPerPackage * ((decimal)YieldPercent / 100m);
                return PricePerPackage / usableQty;
            }
        }
    }
}