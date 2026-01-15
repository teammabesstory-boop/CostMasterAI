using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using CostMasterAI.Helpers; // Pastikan namespace ini ada jika butuh helper

namespace CostMasterAI.Views
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            // 1. EXTEND CONTENT INTO TITLE BAR (MODERN LOOK)
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar); // Kita set Grid 'AppTitleBar' sebagai area drag

            // Set Judul Window
            this.Title = "CostMaster AI";
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            // Default page saat pertama buka: Dashboard
            NavView.SelectedItem = NavView.MenuItems[0];
            Navigate("dashboard");
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                ContentFrame.Navigate(typeof(SettingsPage));
            }
            else
            {
                var selectedItem = args.SelectedItem as NavigationViewItem;
                if (selectedItem != null)
                {
                    string pageTag = selectedItem.Tag.ToString();
                    Navigate(pageTag);
                }
            }
        }

        private void Navigate(string navItemTag)
        {
            Type pageType = null;

            switch (navItemTag)
            {
                case "dashboard":
                    pageType = typeof(DashboardPage);
                    break;
                case "recipes":
                    pageType = typeof(RecipesPage);
                    break;
                case "ingredients":
                    pageType = typeof(IngredientsPage);
                    break;
                case "shopping":
                    pageType = typeof(ShoppingListPage);
                    break;
                default:
                    return; // Do nothing if tag unknown
            }

            // Cek biar gak reload page yang sama
            if (ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType);
            }
        }
    }
}