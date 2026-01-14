using CommunityToolkit.Mvvm.ComponentModel;
using CostMasterAI.Services;
using CostMasterAI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection; // WAJIB ADA: Buat CreateScope
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace CostMasterAI.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;

        // --- STATISTIK UTAMA ---
        [ObservableProperty] private int _totalRecipes;
        [ObservableProperty] private int _totalIngredients;
        [ObservableProperty] private string _averageMargin = "0%";

        // --- INSIGHTS ---
        [ObservableProperty] private string _highestCostRecipeName = "-";
        [ObservableProperty] private decimal _highestCostValue;

        [ObservableProperty] private string _highestMarginRecipeName = "-";
        [ObservableProperty] private string _highestMarginValue = "0%";

        [ObservableProperty] private string _mostComplexRecipeName = "-";
        [ObservableProperty] private int _mostComplexCount;

        // --- STATUS MONITORING ---
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusMessage = "Ready";

        public DashboardViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task RefreshDashboardAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            StatusMessage = "Analysing Data...";

            try
            {
                // Buka koneksi fresh (Scope baru)
                // PENTING: Pake CreateScope() dari Extension Method biar aman
                using (var scope = _serviceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    // --- PERBAIKAN FATAL DI SINI ---
                    // Kita jalanin query SATU PER SATU (Sequential).
                    // Jangan pake Task.WhenAll di DbContext yang sama (bakal crash).

                    TotalRecipes = await db.Recipes.AsNoTracking().CountAsync();
                    TotalIngredients = await db.Ingredients.AsNoTracking().CountAsync();

                    if (TotalRecipes > 0)
                    {
                        // Tarik data snapshot (Read-Only)
                        var recipes = await db.Recipes
                            .AsNoTracking()
                            .Include(r => r.Items).ThenInclude(i => i.Ingredient)
                            .Include(r => r.Overheads)
                            .ToListAsync();

                        ProcessInsights(recipes);
                    }
                    else
                    {
                        ResetStats();
                    }
                }

                StatusMessage = $"Updated: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                StatusMessage = "Error loading data.";
                // Cek pesan error detail di Output Window Visual Studio
                System.Diagnostics.Debug.WriteLine($"❌ DASHBOARD ERROR: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ProcessInsights(List<Recipe> recipes)
        {
            if (recipes == null || !recipes.Any()) return;

            // 1. Avg Margin
            var avg = recipes.Average(r => r.TargetMarginPercent);
            AverageMargin = $"{avg:F0}%";

            // 2. Modal Tertinggi
            var mostExpensive = recipes.OrderByDescending(r => r.CostPerUnit).FirstOrDefault();
            HighestCostRecipeName = mostExpensive?.Name ?? "-";
            HighestCostValue = mostExpensive?.CostPerUnit ?? 0;

            // 3. Margin Tertinggi
            var mostProfitable = recipes.OrderByDescending(r => r.TargetMarginPercent).FirstOrDefault();
            HighestMarginRecipeName = mostProfitable?.Name ?? "-";
            HighestMarginValue = mostProfitable != null ? $"{mostProfitable.TargetMarginPercent}%" : "0%";

            // 4. Paling Kompleks
            var mostComplex = recipes.OrderByDescending(r => r.Items?.Count ?? 0).FirstOrDefault();
            MostComplexRecipeName = mostComplex?.Name ?? "-";
            MostComplexCount = mostComplex?.Items?.Count ?? 0;
        }

        private void ResetStats()
        {
            AverageMargin = "0%";
            HighestCostRecipeName = "-";
            HighestCostValue = 0;
            HighestMarginRecipeName = "-";
            HighestMarginValue = "0%";
            MostComplexRecipeName = "-";
            MostComplexCount = 0;
        }
    }
}