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
            ViewModel = App.Current.Services.GetService<IngredientsViewModel>();
            this.DataContext = ViewModel;

            // --- JURUS ANTI-GAGAL BINDING ---
            // Kita daftarin ViewModel sebagai "Resource" dengan nama kunci "TheViewModel"
            // Jadi tombol di dalem tabel nanti tinggal panggil "TheViewModel" aja.
            // Gak perlu nyari-nyari nama Page lagi.
            this.Resources["TheViewModel"] = ViewModel;
        }
    }
}