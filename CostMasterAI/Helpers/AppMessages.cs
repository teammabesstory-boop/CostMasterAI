using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CostMasterAI.Helpers
{
    // Pesan jika ada perubahan di Database Bahan Baku
    public class IngredientsChangedMessage : ValueChangedMessage<string>
    {
        public IngredientsChangedMessage(string action) : base(action) { }
    }

    // Pesan jika ada perubahan di Database Resep
    public class RecipesChangedMessage : ValueChangedMessage<string>
    {
        public RecipesChangedMessage(string action) : base(action) { }
    }

    // Pesan jika ada perubahan di Database Transaksi/Laporan
    public class TransactionsChangedMessage : ValueChangedMessage<string>
    {
        public TransactionsChangedMessage(string action) : base(action) { }
    }
}