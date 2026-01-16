using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using System;
using CostMasterAI.Core.Services; // FIXED: Mengarah ke Project Core
using CostMasterAI.ViewModels;
using CostMasterAI.Services;      // Untuk AIService (yang masih di project UI)

namespace CostMasterAI
{
    public partial class App : Application
    {
        // Properti Static agar MainWindow bisa diakses global (Penting untuk FilePicker/Dialog)
        public static Window MainWindow { get; private set; }

        private Window m_window;
        public IServiceProvider Services { get; }
        public new static App Current => (App)Application.Current;

        public App()
        {
            this.InitializeComponent();

            var services = new ServiceCollection();

            // --- 1. DATABASE SERVICE ---
            // Register AppDbContext dari Core.
            // Meskipun ViewModel mungkin pakai 'new AppDbContext()', 
            // kita tetap butuh ini untuk inisialisasi awal database (EnsureCreated).
            services.AddDbContext<AppDbContext>();

            // --- 2. VIEWMODELS ---
            services.AddTransient<MainViewModel>();
            services.AddTransient<IngredientsViewModel>();
            services.AddTransient<RecipesViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<ShoppingListViewModel>();
            services.AddTransient<ReportsViewModel>(); // Tambahan baru

            // --- 3. SERVICES LAIN ---
            services.AddSingleton<AIService>();

            Services = services.BuildServiceProvider();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // A. Inisialisasi Database (Buat file .db jika belum ada)
            using (var scope = Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();
            }

            // B. Membuat Window Utama
            // PENTING: Gunakan namespace lengkap 'CostMasterAI.Views.MainWindow'
            // untuk menghindari error "Ambiguous Reference" jika ada sisa file di Core.
            m_window = new CostMasterAI.Views.MainWindow();

            // C. Assign ke properti static
            MainWindow = m_window;

            // D. Tampilkan Window (Solusi agar aplikasi muncul)
            m_window.Activate();
        }
    }
}