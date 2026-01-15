using CostMasterAI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace CostMasterAI.Views
{
    public sealed partial class ShoppingListPage : Page
    {
        public ShoppingListViewModel ViewModel { get; }

        public ShoppingListPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetService<ShoppingListViewModel>();
            this.DataContext = ViewModel;
        }
    }
}