using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using CostMasterAI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace CostMasterAI.Views
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }
        private readonly Dictionary<string, Type> _pageMap;

        public MainWindow()
        {
            this.InitializeComponent();

            // Setup Title Bar Custom
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            // Ambil ViewModel dari DI
            // Menggunakan casting aman untuk akses Services
            ViewModel = ((App)Application.Current).Services.GetRequiredService<MainViewModel>();

            _pageMap = new Dictionary<string, Type>
            {
                ["dashboard"] = typeof(DashboardPage),
                ["ingredients"] = typeof(IngredientsPage),
                ["recipes"] = typeof(RecipesPage),
                ["shopping"] = typeof(ShoppingListPage),
                ["reports"] = typeof(ReportsPage),
                ["ai_assistant"] = typeof(AIAssistantPage),
                ["settings"] = typeof(SettingsPage)
            };

            InitializeDefaultState();
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            // Opsional: Logic tambahan saat nav view loaded
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            // 1. Handle Built-in Settings Item (Tombol Gerigi di Bawah)
            // PERBAIKAN: Gunakan IsSettingsSelected (bukan IsSettingsInvoked) untuk event SelectionChanged
            if (args.IsSettingsSelected)
            {
                NavView_Navigate("settings", args.RecommendedNavigationTransitionInfo);
            }
            // 2. Handle Menu Item Biasa
            else if (args.SelectedItemContainer != null)
            {
                var navItemTag = args.SelectedItemContainer.Tag?.ToString();
                if (!string.IsNullOrEmpty(navItemTag))
                {
                    NavView_Navigate(navItemTag, args.RecommendedNavigationTransitionInfo);
                }
            }
        }

        private void NavView_Navigate(string navItemTag, NavigationTransitionInfo transitionInfo)
        {
            _pageMap.TryGetValue(navItemTag, out var page);

            // Hindari navigasi ulang ke halaman yang sama
            var preNavPageType = ContentFrame.CurrentSourcePageType;
            if (page != null && preNavPageType != page)
            {
                ContentFrame.Navigate(page, null, transitionInfo);
            }
        }

        private void ToggleThemeButton_Click(object sender, RoutedEventArgs e)
        {
            // Simple Theme Toggler
            if (ContentFrame.XamlRoot != null)
            {
                var currentTheme = ContentFrame.ActualTheme;
                var newTheme = currentTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;

                if (ContentFrame.Content is Page currentPage)
                {
                    currentPage.RequestedTheme = newTheme;
                }
            }
        }

        private void GlobalSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            var query = args.QueryText?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(query))
            {
                return;
            }

            if (query.Contains("lapor") || query.Contains("report"))
            {
                NavView_Navigate("reports", new EntranceNavigationTransitionInfo());
                ShowGrowl("Laporan Dibuka", "Menampilkan laporan operasional terbaru.", InfoBarSeverity.Success);
            }
            else if (query.Contains("belanja") || query.Contains("shopping"))
            {
                NavView_Navigate("shopping", new EntranceNavigationTransitionInfo());
                ShowGrowl("Belanja Cerdas", "Akses daftar belanja dan rekomendasi AI.", InfoBarSeverity.Informational);
            }
            else if (query.Contains("resep") || query.Contains("recipe"))
            {
                NavView_Navigate("recipes", new EntranceNavigationTransitionInfo());
                ShowGrowl("Resep Aktif", "Menampilkan daftar resep utama.", InfoBarSeverity.Informational);
            }
            else if (query.Contains("bahan") || query.Contains("ingredient"))
            {
                NavView_Navigate("ingredients", new EntranceNavigationTransitionInfo());
                ShowGrowl("Bahan Baku", "Menampilkan modul bahan baku.", InfoBarSeverity.Warning);
            }
            else if (query.Contains("ai"))
            {
                NavView_Navigate("ai_assistant", new EntranceNavigationTransitionInfo());
                ShowGrowl("AI Assistant", "Masuk ke asisten AI CostMaster.", InfoBarSeverity.Success);
            }
            else
            {
                ShowGrowl("Pencarian Tidak Ditemukan", $"Tidak ada modul cocok untuk \"{query}\".", InfoBarSeverity.Error);
            }
        }

        private void QuickAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string tag)
            {
                NavView_Navigate(tag, new EntranceNavigationTransitionInfo());
                ShowGrowl("Navigasi Cepat", $"Menu {tag.Replace('_', ' ')} aktif.", InfoBarSeverity.Informational);
            }
        }

        private void QuickOpsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowGrowl("Quick Ops", "Pilih operasi cepat dari menu.", InfoBarSeverity.Informational);
        }

        private void CopyReportLink_Click(object sender, RoutedEventArgs e)
        {
            ShowGrowl("Link Laporan Disalin", "Bagikan tautan dashboard kepada tim.", InfoBarSeverity.Success);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            SyncProgress.IsActive = true;
            LastSyncText.Text = $"Terakhir sinkron: {DateTimeOffset.Now:dd MMM yyyy HH:mm}";
            ShowGrowl("Sinkronisasi", "Memperbarui data operasional...", InfoBarSeverity.Informational);
            StatusSummaryText.Text = "Sinkronisasi data real-time berjalan.";
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            ShowGrowl("Bantuan", "Hubungi tim support melalui menu bantuan.", InfoBarSeverity.Informational);
        }

        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            ShowGrowl("Profil", "Kelola profil dan akses tim di sini.", InfoBarSeverity.Informational);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            NavView_Navigate("settings", new EntranceNavigationTransitionInfo());
        }

        private void BroadcastButton_Click(object sender, RoutedEventArgs e)
        {
            ShowGrowl("Broadcast Terkirim", "Notifikasi operasi terkirim ke tim.", InfoBarSeverity.Success);
        }

        private void InitializeDefaultState()
        {
            // Default ke Dashboard saat aplikasi dibuka
            ContentFrame.Navigate(typeof(DashboardPage));
            NavView.SelectedItem = NavView.MenuItems[0];

            HealthSwitch.Value = "Healthy";
            StatusSummaryText.Text = "Semua modul aktif, data biaya tersinkronisasi.";
            LastSyncText.Text = $"Terakhir sinkron: {DateTimeOffset.Now:dd MMM yyyy HH:mm}";
        }

        private void ShowGrowl(string title, string message, InfoBarSeverity severity)
        {
            StatusGrowl.Title = title;
            StatusGrowl.Message = message;
            StatusGrowl.Severity = severity;
            StatusGrowl.IsOpen = true;
        }
    }
}
