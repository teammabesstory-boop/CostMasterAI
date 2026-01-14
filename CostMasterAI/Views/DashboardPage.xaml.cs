using CostMasterAI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace CostMasterAI.Views
{
    public sealed partial class DashboardPage : Page
    {
        public DashboardViewModel ViewModel { get; }

        public DashboardPage()
        {
            this.InitializeComponent();

            // Mengambil ViewModel dari Service Provider
            // Pastikan ViewModel ini sudah terdaftar di App.xaml.cs
            ViewModel = App.Current.Services.GetService<DashboardViewModel>();
            this.DataContext = ViewModel;
        }

        // --- METHOD SAKTI ---
        // Ini akan tereksekusi otomatis setiap kali lo klik menu "Dashboard"
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (ViewModel != null)
            {
                // Paksa refresh data
                await ViewModel.RefreshDashboardAsync();
            }
        }
    }
}