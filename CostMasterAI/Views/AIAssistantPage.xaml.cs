using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection; // Wajib untuk DI
using CostMasterAI.ViewModels;

namespace CostMasterAI.Views
{
    public sealed partial class AIAssistantPage : Page
    {
        // Properti ViewModel
        public AIAssistantViewModel ViewModel { get; }

        public AIAssistantPage()
        {
            this.InitializeComponent();

            // 1. Ambil ViewModel dari Service Locator (Dependency Injection)
            // Container otomatis menyuntikkan AppDbContext & AIService ke dalam ViewModel
            ViewModel = App.Current.Services.GetService<AIAssistantViewModel>();

            // 2. Set DataContext agar Binding di XAML bekerja
            this.DataContext = ViewModel;
        }
    }
}