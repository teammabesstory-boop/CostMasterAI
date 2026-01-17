using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using CostMasterAI.Helpers; // Helper untuk Theme

namespace CostMasterAI.Views
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            // Set Title Bar Custom
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            // Default ke Dashboard saat aplikasi dibuka
            NavView.SelectedItem = NavView.MenuItems[0];
            NavView_Navigate("dashboard", new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            // Opsional: Logic tambahan saat nav view loaded
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                // Navigasi ke halaman Settings (jika ada)
                // ContentFrame.Navigate(typeof(SettingsPage));
            }
            else
            {
                var selectedItem = args.SelectedItemContainer as NavigationViewItem;
                if (selectedItem != null)
                {
                    string navItemTag = selectedItem.Tag.ToString();
                    NavView_Navigate(navItemTag, args.RecommendedNavigationTransitionInfo);
                }
            }
        }

        private void NavView_Navigate(string navItemTag, Microsoft.UI.Xaml.Media.Animation.NavigationTransitionInfo transitionInfo)
        {
            Type _page = null;

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

                // --- FIX: INTEGRASI AI ASSISTANT ---
                case "ai_assistant":
                    _page = typeof(AIAssistantPage);
                    break;

                case "profile":
                    // _page = typeof(ProfilePage); // Uncomment jika sudah ada halaman profile
                    break;
            }

            // Hindari navigasi ulang ke halaman yang sama
            var preNavPageType = ContentFrame.CurrentSourcePageType;
            if (_page != null && !Type.Equals(preNavPageType, _page))
            {
                ContentFrame.Navigate(_page, null, transitionInfo);
            }
        }

        private void ToggleThemeButton_Click(object sender, RoutedEventArgs e)
        {
            // Simple Theme Toggler menggunakan Helper
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