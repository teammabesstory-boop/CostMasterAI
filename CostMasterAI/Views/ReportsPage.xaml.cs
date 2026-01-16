using Microsoft.UI.Xaml.Controls;
using CostMasterAI.ViewModels;

namespace CostMasterAI.Views
{
    /// <summary>
    /// Halaman Laporan Keuangan dan Analisa Penjualan.
    /// Logika bisnis dan data ditangani oleh ReportsViewModel.
    /// </summary>
    public sealed partial class ReportsPage : Page
    {
        // Property ViewModel agar bisa diakses jika nanti menggunakan x:Bind
        public ReportsViewModel ViewModel { get; }

        public ReportsPage()
        {
            this.InitializeComponent();

            // Inisialisasi ViewModel
            ViewModel = new ReportsViewModel();

            // Set DataContext halaman ini ke ViewModel
            // Ini penting agar Binding di XAML (seperti {Binding TotalRevenue}) bekerja
            this.DataContext = ViewModel;
        }
    }
}