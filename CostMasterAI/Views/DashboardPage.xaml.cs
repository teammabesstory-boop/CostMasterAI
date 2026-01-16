using CostMasterAI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace CostMasterAI.Views
{
    public sealed partial class DashboardPage : Page
    {
        public DashboardViewModel ViewModel { get; }

        public DashboardPage()
        {
            this.InitializeComponent();

            // Kita instansiasi manual agar sesuai dengan ViewModel terbaru
            // yang sudah meng-handle koneksi database sendiri.
            ViewModel = new DashboardViewModel();
            this.DataContext = ViewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Saat halaman dibuka, panggil method load data yang baru
            if (ViewModel != null)
            {
                // Menggunakan fire-and-forget (_) karena method ini void
                _ = ViewModel.LoadDashboardData();
            }
        }
    }
}