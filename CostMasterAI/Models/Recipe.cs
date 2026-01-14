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

        public string Description { get; set; } = string.Empty;

        // Output Total (Pcs/Porsi)
        public int YieldQty { get; set; } = 1;

        // Target Berat per Porsi (misal 20g)
        public double TargetPortionSize { get; set; } = 0;

        // --- FITUR BARU: ADVANCED COSTING ---

        // 1. COOKING LOSS (Penyusutan)
        // Contoh: Donat menyusut 10% pas digoreng. Nasi nambah berat (minus loss).
        // Default 0.
        public double CookingLossPercent { get; set; } = 0;

        // 2. LABOR COST (Tenaga Kerja)
        // Berapa menit resep ini dikerjakan?
        public double LaborMinutes { get; set; } = 0;

        // Berapa gaji/biaya per jam karyawan? (Bisa diset default di settings nanti)
        // Contoh: UMR dibagi jam kerja. Misal Rp 15.000/jam.
        public decimal LaborHourlyRate { get; set; } = 0;

        public double TargetMarginPercent { get; set; } = 50;

        // Relasi
        public List<RecipeItem> Items { get; set; } = new();
        public List<RecipeOverhead> Overheads { get; set; } = new();

        // --- KALKULATOR TOTAL ---

        public decimal TotalMaterialCost => Items?.Sum(i => i.CalculatedCost) ?? 0;

        public decimal TotalOverheadCost => Overheads?.Sum(o => o.Cost) ?? 0;

        // Hitung Biaya Tenaga Kerja: (Menit / 60) * Tarif per Jam
        public decimal TotalLaborCost => (decimal)(LaborMinutes / 60.0) * LaborHourlyRate;

        // TOTAL HPP (Batch) = Bahan + Overhead + Tenaga Kerja
        public decimal TotalCost => TotalMaterialCost + TotalOverheadCost + TotalLaborCost;

        public decimal CostPerUnit => YieldQty > 0 ? TotalCost / (decimal)YieldQty : 0;

        public decimal SuggestedPrice
        {
            get
            {
                if (CostPerUnit <= 0 || TargetMarginPercent >= 100) return 0;
                decimal marginDecimal = (decimal)TargetMarginPercent / 100m;
                return CostPerUnit / (1 - marginDecimal);
            }
        }
    }
}