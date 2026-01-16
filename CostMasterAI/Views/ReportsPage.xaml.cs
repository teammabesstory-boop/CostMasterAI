using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using CostMasterAI.ViewModels;
using CostMasterAI.Core.Models; // Wajib untuk akses tipe 'Ingredient'

namespace CostMasterAI.Views
{
    public sealed partial class ReportsPage : Page
    {
        public ReportsViewModel ViewModel { get; }

        public ReportsPage()
        {
            this.InitializeComponent();

            // Inisialisasi ViewModel
            ViewModel = new ReportsViewModel();
            this.DataContext = ViewModel;
        }

        // Method ini dipanggil setiap kali halaman dibuka
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Refresh data (Transaksi & Bahan Baku) setiap masuk halaman ini
            if (ViewModel != null)
            {
                await ViewModel.LoadDataAsync();
            }
        }

        // Event Handler saat Item Bahan Baku diklik
        private void IngredientList_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Ambil item yang diklik
            if (e.ClickedItem is Ingredient ingredient)
            {
                // 1. Eksekusi Command di ViewModel untuk mengisi form
                ViewModel.PickIngredientCommand.Execute(ingredient);

                // 2. Tutup Flyout secara manual
                // "PickButton" adalah nama tombol yang kita definisikan di XAML (x:Name="PickButton")
                if (PickButton?.Flyout != null)
                {
                    PickButton.Flyout.Hide();
                }
            }
        }
    }
}