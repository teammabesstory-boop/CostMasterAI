using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // Tambahkan ini

namespace CostMasterAI.Core.Models
{
    public class Transaction
    {
        // ... (Property Database yang tadi) ...
        [Key] public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "Cash";

        // --- HELPER UNTUK UI (TIDAK DISIMPAN DI DATABASE) ---
        [NotMapped]
        public string AmountPrefix => Type == "Income" ? "+ " : "- ";

        [NotMapped]
        public string StatusColor => Type == "Income" ? "#10B981" : "#EF4444";

        [NotMapped]
        public string DisplayType => Type == "Income" ? "Pemasukan" : "Pengeluaran";
    }
}