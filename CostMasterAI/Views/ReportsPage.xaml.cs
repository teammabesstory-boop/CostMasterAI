using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Extensions.DependencyInjection; // Wajib untuk DI
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

            // 1. Ambil ViewModel dari Service Locator (Dependency Injection)
            // Container otomatis menyuntikkan AppDbContext ke dalam ViewModel
            ViewModel = App.Current.Services.GetService<ReportsViewModel>();

            // 2. Set DataContext
            this.DataContext = ViewModel;
        }

        // Method ini dipanggil setiap kali halaman dibuka (Navigasi)
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Refresh data (Transaksi, Bahan Baku, & Produk) setiap masuk halaman ini
            // Ini penting agar dropdown produk/bahan selalu up-to-date
            if (ViewModel != null)
            {
                await ViewModel.LoadDataAsync();
            }
        }

        // Event Handler saat Item Bahan Baku diklik di dalam Flyout
        private void IngredientList_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Ambil item yang diklik
            if (e.ClickedItem is Ingredient ingredient)
            {
                // 1. Eksekusi Command di ViewModel untuk mengisi form input manual
                if (ViewModel.PickIngredientCommand.CanExecute(ingredient))
                {
                    ViewModel.PickIngredientCommand.Execute(ingredient);
                }

                // 2. Tutup Flyout secara manual agar UX lebih responsif
                // "PickButton" adalah nama tombol yang didefinisikan di XAML
                if (PickButton?.Flyout != null)
                {
                    PickButton.Flyout.Hide();
                }
            }
        }
    }
}