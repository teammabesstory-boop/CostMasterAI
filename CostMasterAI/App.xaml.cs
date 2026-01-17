using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Extensions.DependencyInjection; // Namespace untuk DI
using System;
using CostMasterAI.Views;
using CostMasterAI.ViewModels;
using CostMasterAI.Services;
using CostMasterAI.Core.Services;

namespace CostMasterAI
{
    public partial class App : Application
    {
        // Properti Static agar MainWindow bisa diakses global (Penting untuk FilePicker/Dialog)
        public static Window MainWindow { get; private set; }

        private Window m_window;

        // Container Dependency Injection
        public IServiceProvider Services { get; }

        public new static App Current => (App)Application.Current;

        public App()
        {
            this.InitializeComponent();

            // 1. Konfigurasi Services & ViewModel saat aplikasi start
            Services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // --- A. REGISTER CORE SERVICES ---

            // AIService: Singleton karena menggunakan HttpClient yang sebaiknya reuse connection
            services.AddSingleton<AIService>();

            // AppDbContext: Transient agar setiap kali diminta, dibuat instance baru.
            // Ini mencegah konflik thread dan memastikan koneksi database selalu segar.
            services.AddTransient<AppDbContext>();

            // --- B. REGISTER VIEWMODELS ---
            // ViewModel didaftarkan sebagai Transient (dibuat baru saat halaman dibuka)
            // Container akan otomatis mengisikan Constructor yang dibutuhkan (misal: AIService, AppDbContext)

            services.AddTransient<MainViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<IngredientsViewModel>();
            services.AddTransient<RecipesViewModel>();
            services.AddTransient<ShoppingListViewModel>();
            services.AddTransient<ReportsViewModel>();
            services.AddTransient<AIAssistantViewModel>(); // Fitur Baru
            services.AddTransient<SettingsViewModel>();

            return services.BuildServiceProvider();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // A. Inisialisasi Database
            // Kita buat scope sementara hanya untuk memastikan DB terbentuk
            using (var scope = Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();
            }

            // B. Membuat Window Utama
            m_window = new CostMasterAI.Views.MainWindow();

            // C. Assign ke properti static agar bisa diakses global
            MainWindow = m_window;

            // D. Tampilkan Window
            m_window.Activate();
        }
    }
}