using CostMasterAI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Input;

namespace CostMasterAI.Views
{
    public sealed partial class IngredientsPage : Page
    {
        public IngredientsViewModel ViewModel { get; }

        public IngredientsPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetService<IngredientsViewModel>();
            this.DataContext = ViewModel;

            this.Resources["TheViewModel"] = ViewModel;
        }

        private void TabView_AddTabButtonClick(TabView sender, object args)
        {
            ViewModel.AddNewTabCommand.Execute(null);
        }

        private void TabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            if (args.Item is IngredientTab tabToClose)
            {
                ViewModel.CloseTabCommand.Execute(tabToClose);
            }
        }

        // --- HANDLER ENTER KEY (Save Header) ---
        private void HeaderTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (sender is TextBox textBox && textBox.DataContext is IngredientTab tab)
                {
                    ViewModel.SaveTabHeaderCommand.Execute(tab);
                    this.Focus(FocusState.Programmatic);
                }
            }
        }

        // --- HANDLER BARU: DOUBLE CLICK HEADER ---
        private void Header_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            // Ambil DataContext (IngredientTab) dari elemen yang diklik
            if (sender is FrameworkElement element && element.DataContext is IngredientTab tab)
            {
                // Panggil Command StartEditTab
                ViewModel.StartEditTabCommand.Execute(tab);
            }
        }
    }
}