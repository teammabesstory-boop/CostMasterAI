using CommunityToolkit.Mvvm.ComponentModel;
using CostMasterAI.Services;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace CostMasterAI.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly AppDbContext _dbContext;

        [ObservableProperty]
        private int _totalRecipes;

        [ObservableProperty]
        private int _totalIngredients;

        [ObservableProperty]
        private decimal _averageMargin;

        public DashboardViewModel(AppDbContext dbContext)
        {
            _dbContext = dbContext;
            LoadStatsAsync();
        }

        public async void LoadStatsAsync()
        {
            TotalRecipes = await _dbContext.Recipes.CountAsync();
            TotalIngredients = await _dbContext.Ingredients.CountAsync();

            // Hitung rata-rata margin (kalau ada resep)
            if (TotalRecipes > 0)
            {
                AverageMargin = (decimal)await _dbContext.Recipes.AverageAsync(r => r.TargetMarginPercent);
            }
        }
    }
}