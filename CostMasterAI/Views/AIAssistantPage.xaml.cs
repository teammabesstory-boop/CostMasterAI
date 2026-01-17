using Microsoft.UI.Xaml.Controls;
using CostMasterAI.ViewModels;

namespace CostMasterAI.Views
{
    public sealed partial class AIAssistantPage : Page
    {
        public AIAssistantViewModel ViewModel { get; }

        public AIAssistantPage()
        {
            this.InitializeComponent();
            ViewModel = new AIAssistantViewModel();
        }
    }
}