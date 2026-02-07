using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using CostMasterAI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CostMasterAI.Views
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            this.InitializeComponent();

            // Setup Title Bar Custom
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            // Ambil ViewModel dari DI
            // Menggunakan casting aman untuk akses Services
            ViewModel = ((App)Application.Current).Services.GetRequiredService<MainViewModel>();

            // Default ke Dashboard saat aplikasi dibuka
            ContentFrame.Navigate(typeof(DashboardPage));
            NavView.SelectedItem = NavView.MenuItems[0];
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            // Opsional: Logic tambahan saat nav view loaded
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            // 1. Handle Built-in Settings Item (Tombol Gerigi di Bawah)
            // PERBAIKAN: Gunakan IsSettingsSelected (bukan IsSettingsInvoked) untuk event SelectionChanged
            if (args.IsSettingsSelected)
            {
                NavView_Navigate("settings", args.RecommendedNavigationTransitionInfo);
            }
            // 2. Handle Menu Item Biasa
            else if (args.SelectedItemContainer != null)
            {
                var navItemTag = args.SelectedItemContainer.Tag?.ToString();
                if (!string.IsNullOrEmpty(navItemTag))
                {
                    NavView_Navigate(navItemTag, args.RecommendedNavigationTransitionInfo);
                }
            }
        }

        private void NavView_Navigate(string navItemTag, NavigationTransitionInfo transitionInfo)
        {
            Type? _page = null;

            switch (navItemTag)
            {
                case "dashboard":
                    _page = typeof(DashboardPage);
                    break;
                case "ingredients":
                    _page = typeof(IngredientsPage);
                    break;
                case "recipes":
                    _page = typeof(RecipesPage);
                    break;
                case "shopping":
                    _page = typeof(ShoppingListPage);
                    break;
                case "reports":
                    _page = typeof(ReportsPage);
                    break;
                case "ai_assistant":
                    _page = typeof(AIAssistantPage);
                    break;
                // --- NAVIGASI SETTINGS ---
                case "settings":
                    _page = typeof(SettingsPage);
                    break;
            }

            // Hindari navigasi ulang ke halaman yang sama
            var preNavPageType = ContentFrame.CurrentSourcePageType;
            if (_page != null && preNavPageType != _page)
            {
                ContentFrame.Navigate(_page, null, transitionInfo);
            }
        }

        private void ToggleThemeButton_Click(object sender, RoutedEventArgs e)
        {
            // Simple Theme Toggler
            if (ContentFrame.XamlRoot != null)
            {
                var currentTheme = ContentFrame.ActualTheme;
                var newTheme = currentTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;

                if (ContentFrame.Content is Page currentPage)
                {
                    currentPage.RequestedTheme = newTheme;
                }
            }
        }
    }
}
