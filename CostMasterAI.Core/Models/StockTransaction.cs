using System;
using System.ComponentModel.DataAnnotations;

namespace CostMasterAI.Core.Models
{
    public class StockTransaction
    {
        [Key]
        public int Id { get; set; }

        public int IngredientId { get; set; }
        public virtual Ingredient? Ingredient { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        // "In" (Masuk), "Out" (Keluar/Jual), "Adjustment" (Opname/Koreksi)
        public string Type { get; set; } = "In";

        public double Quantity { get; set; } // Jumlah fisik yang berubah
        public string Unit { get; set; } = "";

        public string Description { get; set; } = ""; // Misal: "Belanja Pasar" atau "Penjualan"
        public string ReferenceId { get; set; } = ""; // Opsional: ID Transaksi Keuangan
    }
}