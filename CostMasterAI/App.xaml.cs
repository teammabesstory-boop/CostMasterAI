using CostMasterAI.Views;
using CostMasterAI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System;

namespace CostMasterAI
{
    public partial class App : Application
    {
        // Properti Static agar MainWindow bisa diakses dari ViewModel (Penting untuk FilePicker)
        public static Window MainWindow { get; private set; }

        private Window m_window;
        public IServiceProvider Services { get; }
        public new static App Current => (App)Application.Current;

        public App()
        {
            this.InitializeComponent();

            var services = new ServiceCollection();

            // Database Service (Transient disarankan untuk EF Core di Desktop App)
            services.AddDbContext<AppDbContext>(options => { }, ServiceLifetime.Transient);

            // Register ViewModels & Services
            services.AddTransient<ViewModels.MainViewModel>();
            services.AddTransient<ViewModels.IngredientsViewModel>();
            services.AddTransient<ViewModels.RecipesViewModel>();
            services.AddSingleton<AIService>();
            services.AddTransient<ViewModels.SettingsViewModel>();
            services.AddTransient<ViewModels.DashboardViewModel>();
            services.AddTransient<ViewModels.ShoppingListViewModel>();

            Services = services.BuildServiceProvider();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Inisialisasi Database
            using (var scope = Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                // Pastikan database ada. Jangan gunakan EnsureDeleted() agar data tidak hilang.
                db.Database.EnsureCreated();
            }

            m_window = new MainWindow();

            // Assign instance window ke properti static
            MainWindow = m_window;

            m_window.Activate();
        }
    }
}