using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media; // <--- INI OBATNYA BUAT MICABACKDROP
using System;

namespace CostMasterAI.Views
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "CostMaster AI - Final Release";

            // Sekarang dia udah kenal MicaBackdrop
            SystemBackdrop = new MicaBackdrop();
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            NavView.SelectedItem = NavView.MenuItems[0];
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                ContentFrame.Navigate(typeof(SettingsPage));
            }
            else if (args.SelectedItem is NavigationViewItem selectedItem)
            {
                string tag = selectedItem.Tag.ToString();
                switch (tag)
                {
                    case "home":
                        ContentFrame.Navigate(typeof(DashboardPage));
                        break;
                    case "ingredients":
                        ContentFrame.Navigate(typeof(IngredientsPage));
                        break;
                    case "recipes":
                        ContentFrame.Navigate(typeof(RecipesPage));
                        break;
                    case "settings": // Handle tag manual juga biar aman
                        ContentFrame.Navigate(typeof(SettingsPage));
                        break;
                }
            }
        }
    }
}