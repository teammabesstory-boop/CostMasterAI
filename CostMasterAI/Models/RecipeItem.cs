using System.ComponentModel.DataAnnotations;
using CostMasterAI.Helpers;

namespace CostMasterAI.Models
{
    public class RecipeItem
    {
        [Key]
        public int Id { get; set; }

        public int RecipeId { get; set; }
        public Recipe Recipe { get; set; } // Parent

        public int IngredientId { get; set; }
        public Ingredient Ingredient { get; set; }

        public double UsageQty { get; set; } // Jumlah Pakai (Gram/Pcs)
        public string UsageUnit { get; set; } // Satuan Pakai

        // --- TAMBAHAN BARU ---
        // Jika True: UsageQty dianggap "Per Pcs". Total biaya = Biaya x YieldQty.
        // Jika False: UsageQty dianggap "Total Batch".
        public bool IsUnitBased { get; set; } = false;

        // --- LOGIKA BIAYA DINAMIS ---
        public decimal CalculatedCost
        {
            get
            {
                if (Ingredient == null) return 0;

                // 1. Konversi satuan pakai ke satuan beli (Gram ke Gram, atau Gram ke Kg)
                double quantityInPackageUnit = 0;

                // Cek konversi langsung
                double rate = UnitHelper.GetConversionRate(UsageUnit, Ingredient.Unit);
                if (rate > 0)
                {
                    quantityInPackageUnit = UsageQty * rate;
                }
                else
                {
                    // Fallback: Coba konversi via standar Gram/ML
                    // Misalnya: Usage(Sendok) -> Gram -> Package(Kg)
                    // Sederhananya kita asumsikan user input satuan yg valid dulu.
                    // Kalau gagal, anggap 0.
                    quantityInPackageUnit = 0;
                }

                // 2. Hitung harga dasar bahan yang dipakai
                decimal baseCost = 0;
                if (Ingredient.QuantityPerPackage > 0)
                {
                    decimal pricePerUnit = Ingredient.PricePerPackage / (decimal)Ingredient.QuantityPerPackage;
                    baseCost = pricePerUnit * (decimal)quantityInPackageUnit;
                }

                // 3. LOGIC BARU: HITUNG PER UNIT VS TOTAL
                // Kalau IsUnitBased nyala, berarti ini biaya per 1 donat.
                // Kita harus kali dengan Total Donat (YieldQty) buat dapet Total HPP Batch.
                if (IsUnitBased && Recipe != null)
                {
                    return baseCost * Recipe.YieldQty;
                }

                return baseCost;
            }
        }
    }
}