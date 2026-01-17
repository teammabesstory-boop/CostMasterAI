using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection; // Wajib untuk DI
using CostMasterAI.ViewModels;

namespace CostMasterAI.Views
{
    public sealed partial class ShoppingListPage : Page
    {
        // Properti ViewModel (Read-Only)
        public ShoppingListViewModel ViewModel { get; }

        public ShoppingListPage()
        {
            this.InitializeComponent();

            // 1. Ambil ViewModel dari Service Locator (Dependency Injection)
            // Container otomatis menyuntikkan AppDbContext ke dalam Constructor ShoppingListViewModel
            ViewModel = App.Current.Services.GetService<ShoppingListViewModel>();

            // 2. Set DataContext agar Binding di XAML bekerja
            this.DataContext = ViewModel;
        }
    }
}