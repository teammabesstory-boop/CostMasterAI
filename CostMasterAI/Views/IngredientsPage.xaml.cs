using CostMasterAI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace CostMasterAI.Views
{
    public sealed partial class IngredientsPage : Page
    {
        public IngredientsViewModel ViewModel { get; }

        public IngredientsPage()
        {
            this.InitializeComponent();

            // Mengambil ViewModel dari Dependency Injection
            ViewModel = App.Current.Services.GetService<IngredientsViewModel>();
            this.DataContext = ViewModel;
        }
    }
}