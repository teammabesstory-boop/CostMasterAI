using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Generic;
using Windows.Storage;

namespace CostMasterAI.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        // Akses ke LocalSettings Windows
        private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

        [ObservableProperty]
        private string _apiKey = string.Empty;

        [ObservableProperty]
        private string _selectedModel = "gemini-1.5-flash";

        [ObservableProperty]
        private string _statusMessage = ""; // Buat ngasih tau "Tersimpan!"

        // List Model Gemini yang tersedia
        public List<string> AvailableModels { get; } = new()
        {
            "gemini-1.5-flash", // Cepat & Murah (Stabil)
            "gemini-1.5-pro",   // Lebih Pinter (Mahal dikit)
            "gemini-1.0-pro"    // Versi Lama
        };

        public SettingsViewModel()
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Ambil dari penyimpanan, kalau null pake default
            ApiKey = _localSettings.Values["ApiKey"] as string ?? "";
            SelectedModel = _localSettings.Values["AiModel"] as string ?? "gemini-1.5-flash";
        }

        [RelayCommand]
        private void SaveSettings()
        {
            // Simpan ke penyimpanan Windows
            _localSettings.Values["ApiKey"] = ApiKey;
            _localSettings.Values["AiModel"] = SelectedModel;

            StatusMessage = "✅ Pengaturan berhasil disimpan!";

            // Ilangin pesan status setelah 3 detik (Optional, pake Task.Delay)
            ClearStatusDelayed();
        }

        private async void ClearStatusDelayed()
        {
            await System.Threading.Tasks.Task.Delay(3000);
            StatusMessage = "";
        }
    }
}