using CostMasterAI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using System;
using Windows.System; // Penting untuk VirtualKey

namespace CostMasterAI.Views
{
    public sealed partial class RecipesPage : Page
    {
        // 1. Deklarasi ViewModel
        public RecipesViewModel ViewModel { get; }

        // 2. Constructor
        public RecipesPage()
        {
            this.InitializeComponent();
            // Ambil ViewModel dari Service Locator (App.xaml.cs)
            ViewModel = App.Current.Services.GetService<RecipesViewModel>();
            this.DataContext = ViewModel;
        }

        // 3. Event Handler: Tombol Lihat Detail (Pop-up)
        private async void ShowDetail_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            string detailText = ViewModel.GenerateCostDetailString();

            ContentDialog dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Rincian Perhitungan Harga",
                CloseButtonText = "Tutup",
                DefaultButton = ContentDialogButton.Close
            };

            var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var textBlock = new TextBlock
            {
                Text = detailText,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas, Courier New"),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            };

            scrollViewer.Content = textBlock;
            dialog.Content = scrollViewer;

            await dialog.ShowAsync();
        }

        // 4. Event Handler: Input Harga Manual (Tekan Enter)
        private void OnPriceBoxKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                // Paksa update binding data dari TextBox ke ViewModel sebelum command jalan
                var textBox = sender as TextBox;
                var binding = textBox?.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();

                // Panggil command hitung margin
                ViewModel.RecalculateMarginFromPriceCommand.Execute(null);

                // Hilangkan fokus dari TextBox agar keyboard menutup / user tahu input masuk
                // 'this' merujuk pada Page (RecipesPage), yang merupakan Control/UIElement
                this.Focus(FocusState.Programmatic);
            }
        }
    }
}