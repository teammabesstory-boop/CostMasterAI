using CostMasterAI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CostMasterAI.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage()
        {
            this.InitializeComponent();

            // Ambil ViewModel dari DI Container
            // Menggunakan casting (App) untuk memastikan akses ke properti Services
            ViewModel = ((App)Application.Current).Services.GetRequiredService<SettingsViewModel>();

            this.DataContext = ViewModel;
        }
    }
}