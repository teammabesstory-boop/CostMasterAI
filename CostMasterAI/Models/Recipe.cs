using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace CostMasterAI.Models
{
    public class Recipe
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        // --- TAMBAHAN BARU ---
        // Buat nyimpen hasil tulisan AI
        public string Description { get; set; } = string.Empty;
        // ---------------------

        public double YieldQty { get; set; } = 1;
        public double TargetMarginPercent { get; set; } = 30;

        public List<RecipeItem> Items { get; set; } = new();

        public decimal TotalCost => Items?.Sum(i => i.CalculatedCost) ?? 0;
        public decimal CostPerUnit => YieldQty > 0 ? TotalCost / (decimal)YieldQty : 0;
        public decimal SuggestedPrice => TargetMarginPercent < 100
            ? CostPerUnit / (decimal)(1 - (TargetMarginPercent / 100))
            : 0;
    }
}