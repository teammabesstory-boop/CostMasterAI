using CostMasterAI.Views;
using CostMasterAI.Services; // <--- Pastiin ada ini
using Microsoft.Extensions.DependencyInjection; // <--- Dan ini
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

            // Register Services
            services.AddDbContext<AppDbContext>();
            services.AddTransient<ViewModels.MainViewModel>();
            services.AddTransient<ViewModels.IngredientsViewModel>();
            services.AddTransient<ViewModels.RecipesViewModel>();
            services.AddSingleton<AIService>();
            services.AddTransient<ViewModels.SettingsViewModel>();

            Services = services.BuildServiceProvider();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // --- JURUS PERBAIKAN DIMULAI DARI SINI ---

            // Kita bikin scope sebentar cuma buat manggil database
            using (var scope = Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Perintah Sakti: "Cek database ada gak? Kalau gak ada, BIKIN SEKARANG + TABELNYA!"
                // Kita pake yang sinkronus (tanpa Async) biar dia nungguin sampe kelar baru lanjut.
                db.Database.EnsureCreated();
            }

            // --- PERBAIKAN SELESAI ---

            m_window = new MainWindow();
            m_window.Activate();
        }
    }
}