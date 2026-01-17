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
        private string _appVersion = "1.0.0 (Gemini AI Edition)";

        [ObservableProperty]
        private string _googleApiKey = string.Empty;

        [ObservableProperty]
        private string _selectedModel = "gemini-1.5-flash"; // Default fallback

        [ObservableProperty]
        private string _statusMessage = "";

        // Daftar Model yang bisa dipilih user
        public List<string> AvailableModels { get; } = new()
        {
            "gemini-2.5-flash",     // <-- MODEL BARU (Prioritas)
            "gemini-2.0-flash-exp", // Versi Experimental
            "gemini-1.5-flash",     // Versi Stabil (Cepat & Murah)
            "gemini-1.5-pro",       // Versi Pro (Lebih Pintar)
        };

        public SettingsViewModel()
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Load API Key
            if (_localSettings.Values.TryGetValue("GoogleApiKey", out var key))
            {
                GoogleApiKey = key.ToString() ?? string.Empty;
            }

            // Load Pilihan Model (Default ke 1.5 Flash jika belum ada)
            if (_localSettings.Values.TryGetValue("GeminiModelId", out var modelId))
            {
                SelectedModel = modelId.ToString() ?? "gemini-1.5-flash";
            }
        }

        [RelayCommand]
        private void SaveSettings()
        {
            // Simpan ke Local Storage agar bisa dibaca oleh AIService
            _localSettings.Values["GoogleApiKey"] = GoogleApiKey;
            _localSettings.Values["GeminiModelId"] = SelectedModel;

            StatusMessage = "✅ Konfigurasi AI berhasil disimpan!";
            ClearStatusDelayed();

            System.Diagnostics.Debug.WriteLine($"[Settings] Key Saved. Model: {SelectedModel}");
        }

        private async void ClearStatusDelayed()
        {
            await Task.Delay(3000);
            StatusMessage = "";
        }
    }
}