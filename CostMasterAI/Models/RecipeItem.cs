using System.ComponentModel.DataAnnotations;
using CostMasterAI.Helpers; // Panggil helper tadi

namespace CostMasterAI.Models
{
    public class RecipeItem
    {
        [Key]
        public int Id { get; set; }

        public int RecipeId { get; set; }
        public Recipe Recipe { get; set; }

        public int IngredientId { get; set; }
        public Ingredient Ingredient { get; set; }

        public double UsageQty { get; set; }

        // --- UPDATE BARU: Satuan Pakai ---
        public string UsageUnit { get; set; } = "Gram";
        // ---------------------------------

        // Logika Biaya Paling Canggih
        public decimal CalculatedCost
        {
            get
            {
                if (Ingredient == null) return 0;

                // 1. Dapatkan rasio konversi dari Satuan Pakai (Resep) ke Satuan Beli (Bahan)
                // Contoh: Resep pake "Sdm", Bahan beli "Liter".
                double rate = UnitHelper.GetConversionRate(UsageUnit, Ingredient.Unit);

                // 2. Hitung jumlah bahan yang kepake dalam satuan ASLINYA bahan
                // Contoh: 2 Sdm * (15/1000) = 0.03 Liter
                double realUsageInIngredientUnit = UsageQty * rate;

                // 3. Kalikan dengan Harga Real (yang udah memperhitungkan Yield)
                return Ingredient.RealCostPerUnit * (decimal)realUsageInIngredientUnit;
            }
        }
    }
}