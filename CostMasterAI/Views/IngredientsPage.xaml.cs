using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection; // Wajib untuk akses DI Extension
using CostMasterAI.ViewModels;

namespace CostMasterAI.Views
{
    public sealed partial class IngredientsPage : Page
    {
        // Properti Read-Only untuk ViewModel
        public IngredientsViewModel ViewModel { get; }

        public IngredientsPage()
        {
            this.InitializeComponent();

            // 1. Ambil ViewModel dari Service Locator (Dependency Injection)
            // Container otomatis menyuntikkan AppDbContext ke dalam Constructor IngredientsViewModel
            ViewModel = App.Current.Services.GetService<IngredientsViewModel>();

            // 2. Set DataContext agar Binding di XAML bekerja
            this.DataContext = ViewModel;
        }
    }
}