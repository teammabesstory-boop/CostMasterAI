using CostMasterAI.Views;
using CostMasterAI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System;

namespace CostMasterAI
{
    public partial class App : Application
    {
        public Window m_window;
        public IServiceProvider Services { get; }
        public new static App Current => (App)Application.Current;

        public App()
        {
            this.InitializeComponent();

            var services = new ServiceCollection();

            // --- PERBAIKAN PENTING DI SINI ---
            // Ganti jadi ServiceLifetime.Transient biar tiap ViewModel dapet koneksi database sendiri-sendiri.
            // Ini mencegah crash "Concurrency" pas generate AI sambil buka halaman lain.
            services.AddDbContext<AppDbContext>(options => { }, ServiceLifetime.Transient);

            services.AddTransient<ViewModels.MainViewModel>();
            services.AddTransient<ViewModels.IngredientsViewModel>();
            services.AddTransient<ViewModels.RecipesViewModel>();
            services.AddSingleton<AIService>();
            services.AddTransient<ViewModels.SettingsViewModel>();

            Services = services.BuildServiceProvider();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Pake scope buat inisialisasi awal database
            using (var scope = Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();
            }

            m_window = new MainWindow();
            m_window.Activate();
        }
    }
}