using System.ComponentModel.DataAnnotations;

namespace CostMasterAI.Core.Models
{
    public class RecipeOverhead
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty; // Contoh: "Gas Elpiji 1 Jam"

        public decimal Cost { get; set; } // Contoh: 5000

        // Relasi ke Resep
        public int RecipeId { get; set; }
        public Recipe Recipe { get; set; } = null!;
    }
}
