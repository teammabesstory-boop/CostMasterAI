using CostMasterAI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace CostMasterAI.Views
{
    public sealed partial class RecipesPage : Page
    {
        public RecipesViewModel ViewModel { get; }

        public RecipesPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetService<RecipesViewModel>();
            this.DataContext = ViewModel;

            // INI PENTING BUAT BINDING TOMBOL DELETE
            this.Name = "RootPage";
        }
    }
}