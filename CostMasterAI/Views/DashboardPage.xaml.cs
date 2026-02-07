using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Extensions.DependencyInjection; // Wajib untuk DI
using CostMasterAI.ViewModels;

namespace CostMasterAI.Views
{
    public sealed partial class DashboardPage : Page
    {
        // Properti ViewModel (Read-only)
        public DashboardViewModel ViewModel { get; }

        public DashboardPage()
        {
            this.InitializeComponent();

            // 1. Ambil ViewModel dari Service Locator (Dependency Injection)
            // Container otomatis menyuntikkan AppDbContext ke dalam ViewModel
            ViewModel = App.Current.Services.GetRequiredService<DashboardViewModel>();

            // 2. Set DataContext agar Binding di XAML bekerja
            this.DataContext = ViewModel;
        }

        // Method ini dipanggil setiap kali halaman dibuka (Navigasi)
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Refresh data Dashboard setiap kali user kembali ke halaman ini
            // Menggunakan fire-and-forget (_) karena method LoadDashboardData adalah async Task
            if (ViewModel != null)
            {
                _ = ViewModel.LoadDashboardData();
            }
        }
    }
}
