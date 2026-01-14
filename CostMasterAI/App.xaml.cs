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

            // Database Service (Transient biar aman thread-nya)
            services.AddDbContext<AppDbContext>(options => { }, ServiceLifetime.Transient);

            services.AddTransient<ViewModels.MainViewModel>();
            services.AddTransient<ViewModels.IngredientsViewModel>();
            services.AddTransient<ViewModels.RecipesViewModel>();
            services.AddSingleton<AIService>();
            services.AddTransient<ViewModels.SettingsViewModel>();
            services.AddTransient<ViewModels.DashboardViewModel>();

            Services = services.BuildServiceProvider();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            using (var scope = Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // --- PERBAIKAN DI SINI ---
                // HAPUS atau COMMENT baris EnsureDeleted() ini selamanya!
                // db.Database.EnsureDeleted(); <--- JANGAN DIAKTIFKAN LAGI

                // Pastikan database dibuat kalau belum ada
                db.Database.EnsureCreated();
            }

            m_window = new MainWindow();
            m_window.Activate();
        }
    }
}