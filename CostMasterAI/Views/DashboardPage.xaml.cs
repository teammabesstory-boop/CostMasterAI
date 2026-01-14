using CostMasterAI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace CostMasterAI.Views // <--- Pastiin namespace-nya ini
{
    public sealed partial class DashboardPage : Page
    {
        public DashboardViewModel ViewModel { get; }

        public DashboardPage()
        {
            this.InitializeComponent(); // Sekarang harusnya gak merah lagi

            // Pastiin ViewModel ini udah didaftarin di App.xaml.cs ya!
            // Kalau error null, berarti lupa daftarin services.AddTransient...
            ViewModel = App.Current.Services.GetService<DashboardViewModel>();

            this.DataContext = ViewModel;
        }
    }
}