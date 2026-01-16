using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using CostMasterAI.Views;

namespace CostMasterAI.Views
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            // Mengatur TitleBar kustom
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            // Set Tema Default ke Light saat aplikasi mulai
            if (Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = ElementTheme.Light;
            }
        }

        // --- LOGIKA LOADING ---
        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            // Cek apakah ada menu items
            if (NavView.MenuItems.Count > 0)
            {
                // 1. Pilih item pertama (Dashboard) secara visual di menu
                NavView.SelectedItem = NavView.MenuItems[0];

                // 2. Navigasi manual ke halaman Dashboard
                ContentFrame.Navigate(typeof(DashboardPage));
            }
        }

        // --- LOGIKA NAVIGASI ---
        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            // Safety check: Pastikan args tidak null
            if (args == null) return;

            if (args.IsSettingsSelected)
            {
                ContentFrame.Navigate(typeof(SettingsPage));
            }
            else if (args.SelectedItem is NavigationViewItem selectedItem)
            {
                string tag = selectedItem.Tag?.ToString();

                switch (tag)
                {
                    case "dashboard":
                        ContentFrame.Navigate(typeof(DashboardPage));
                        break;
                    case "ingredients":
                        ContentFrame.Navigate(typeof(IngredientsPage));
                        break;
                    case "recipes":
                        ContentFrame.Navigate(typeof(RecipesPage));
                        break;
                    case "shopping":
                        ContentFrame.Navigate(typeof(ShoppingListPage));
                        break;
                    case "reports":
                        // Case Baru untuk Halaman Laporan
                        ContentFrame.Navigate(typeof(ReportsPage));
                        break;
                    case "profile":
                        // ContentFrame.Navigate(typeof(ProfilePage)); 
                        break;
                }
            }
        }

        // --- LOGIKA DARK MODE ---
        private void ToggleThemeButton_Click(object sender, RoutedEventArgs e)
        {
            if (Content is FrameworkElement rootElement)
            {
                if (rootElement.RequestedTheme == ElementTheme.Light)
                {
                    rootElement.RequestedTheme = ElementTheme.Dark;
                    // Ubah icon jadi Matahari (Sun) - Kode RemixIcon: ri-sun-line
                    if (ThemeIcon != null) ThemeIcon.Glyph = "\uEECB";
                }
                else
                {
                    rootElement.RequestedTheme = ElementTheme.Light;
                    // Ubah icon jadi Bulan (Moon) - Kode RemixIcon: ri-moon-line
                    if (ThemeIcon != null) ThemeIcon.Glyph = "\uEF56";
                }
            }
        }
    }
}