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

        // Harga Beli (Kotor)
        public decimal PricePerPackage { get; set; }

        // Berat Beli (Kotor)
        public double QuantityPerPackage { get; set; }

        public string Unit { get; set; } = "Gram";

        public string Category { get; set; } = "General";

        // --- TAMBAHAN BARU ---
        // Yield Percent: Berapa % bagian yang bisa dipake?
        // Default 100% (misal Susu, Gula). Kalau Kentang/Udang mungkin 80% atau 60%.
        public double YieldPercent { get; set; } = 100;

        // --- LOGIKA BARU ---
        // Harga Asli per Unit Bersih (Ini yang dipake hitung HPP!)
        // Rumus: Harga Beli / (Berat Beli x Yield%)
        public decimal RealCostPerUnit
        {
            get
            {
                if (QuantityPerPackage <= 0 || YieldPercent <= 0) return 0;

                // Hitung berat bersih yang didapat dari 1 kemasan
                var usableQty = (decimal)QuantityPerPackage * ((decimal)YieldPercent / 100m);

                // Harga dibagi berat bersih
                return PricePerPackage / usableQty;
            }
        }
    }
}