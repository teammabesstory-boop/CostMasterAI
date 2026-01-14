using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq; // Wajib buat Sum

namespace CostMasterAI.Models
{
    public class Recipe
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int YieldQty { get; set; } = 1;

        // Relasi
        public List<RecipeItem> Items { get; set; } = new();

        // --- TAMBAHAN RELASI ---
        public List<RecipeOverhead> Overheads { get; set; } = new();

        // --- LOGIKA HITUNG CANGGIH ---

        // 1. Biaya Bahan Baku (Food Cost)
        public decimal TotalMaterialCost => Items?.Sum(i => i.CalculatedCost) ?? 0;

        // 2. Biaya Operasional (Overhead)
        public decimal TotalOverheadCost => Overheads?.Sum(o => o.Cost) ?? 0;

        // 3. GRAND TOTAL (HPP Batch)
        public decimal TotalCost => TotalMaterialCost + TotalOverheadCost;

        // 4. HPP Per Porsi
        public decimal CostPerUnit => YieldQty > 0 ? TotalCost / YieldQty : 0;

        // 5. Harga Jual (Margin 50% - Standard Resto)
        public decimal SuggestedPrice => CostPerUnit > 0 ? CostPerUnit * 2 : 0;
    }
}