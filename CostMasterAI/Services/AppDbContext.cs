using Microsoft.EntityFrameworkCore;
using CostMasterAI.Models;
using System.IO;
using System;

namespace CostMasterAI.Services
{
    public class AppDbContext : DbContext
    {
        public DbSet<Ingredient> Ingredients { get; set; }
        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<RecipeItem> RecipeItems { get; set; }

        // --- TAMBAHAN BARU ---
        public DbSet<RecipeOverhead> RecipeOverheads { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public AppDbContext() { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var folder = Environment.SpecialFolder.LocalApplicationData;
                var path = Environment.GetFolderPath(folder);
                var dbPath = Path.Join(path, "costmaster.db");
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }
    }
}