using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Windows.Storage;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CostMasterAI.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

        [ObservableProperty]
        private string _apiKey = string.Empty;

        [ObservableProperty]
        private string _selectedModel = "gemini-1.5-flash";

        [ObservableProperty]
        private string _statusMessage = "";

        public List<string> AvailableModels { get; } = new()
        {
            "gemini-2.5-flash", // <-- MODEL BARU DITAMBAHKAN
            "gemini-1.5-flash",
            "gemini-1.5-pro",
            "gemini-1.0-pro"
        };

        public SettingsViewModel()
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            ApiKey = _localSettings.Values["ApiKey"] as string ?? "";
            SelectedModel = _localSettings.Values["AiModel"] as string ?? "gemini-1.5-flash";
        }

        [RelayCommand]
        private void SaveSettings()
        {
            _localSettings.Values["ApiKey"] = ApiKey;
            _localSettings.Values["AiModel"] = SelectedModel;

            StatusMessage = "✅ Pengaturan berhasil disimpan!";
            ClearStatusDelayed();
        }

        private async void ClearStatusDelayed()
        {
            await Task.Delay(3000);
            StatusMessage = "";
        }
    }
}