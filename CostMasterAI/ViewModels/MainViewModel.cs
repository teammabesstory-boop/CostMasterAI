using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CostMasterAI.Services;
using System.Threading.Tasks;

namespace CostMasterAI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // Kita butuh akses ke Database Context
        private readonly AppDbContext _dbContext;

        [ObservableProperty]
        private string _welcomeMessage = "Welcome to CostMaster AI";

        [ObservableProperty]
        private string _status = "System Ready";

        // Constructor Injection: Otomatis diisi sama App.xaml.cs tadi
        public MainViewModel(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [RelayCommand]
        private async Task InitializeAppAsync()
        {
            Status = "Checking Database Connection...";

            // Wait dikit biar keliatan loadingnya (gaya doang)
            await Task.Delay(1000);

            // Perintah sakti: Pastiin file .db nya ada. Kalau belum, bikinin!
            // Note: Di production nanti kita pake Migrations, tapi buat start ini cukup.
            bool isCreated = await _dbContext.Database.EnsureCreatedAsync();

            if (isCreated)
            {
                Status = "Database Created Successfully!";
                WelcomeMessage = "New Brain Installed! Ready to learn.";
            }
            else
            {
                Status = "Database Loaded.";
                WelcomeMessage = "Welcome back, Master.";
            }
        }
    }
}