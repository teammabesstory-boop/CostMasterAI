using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CostMasterAI.Core.Models
{
    public class RecipeItem
    {
        [Key]
        public int Id { get; set; }

        public int RecipeId { get; set; }
        [ForeignKey("RecipeId")]
        public Recipe Recipe { get; set; }

        public int IngredientId { get; set; }
        [ForeignKey("IngredientId")]
        public Ingredient Ingredient { get; set; }

        public double UsageQty { get; set; }
        public string UsageUnit { get; set; } = "Gram";

        public bool IsUnitBased { get; set; } = false; // True = Inputan Pcs (misal: 2 Telur)

        // --- FIELD BARU: Kategori Penggunaan ---
        public string UsageCategory { get; set; } = "Main";

        // --- FIELD BARU: Hitung Per Pcs ---
        // Jika True, maka UsageQty adalah berat per 1 donat
        public bool IsPerPiece { get; set; } = false;

        [NotMapped]
        public decimal CalculatedCost
        {
            get
            {
                if (Ingredient == null) return 0;

                // 1. Tentukan Total Qty yang dibutuhkan untuk 1 Batch
                double totalQtyNeeded = UsageQty;

                // Jika mode "Per Pcs" aktif, kalikan dengan jumlah Yield (Output Donat)
                if (IsPerPiece && Recipe != null)
                {
                    totalQtyNeeded = UsageQty * Recipe.YieldQty;
                }

                decimal pricePerBaseUnit = 0;

                // 2. Hitung Harga Satuan Dasar
                if (IsUnitBased)
                {
                    // Case: Beli per butir/pcs
                    if (Ingredient.QuantityPerPackage > 0)
                        pricePerBaseUnit = Ingredient.PricePerPackage / (decimal)Ingredient.QuantityPerPackage;
                }
                else
                {
                    // Case: Berat/Volume
                    if (Ingredient.QuantityPerPackage > 0)
                        pricePerBaseUnit = Ingredient.PricePerPackage / (decimal)Ingredient.QuantityPerPackage;
                }

                // 3. Final Cost
                return pricePerBaseUnit * (decimal)totalQtyNeeded;
            }
        }
    }
}