using Microsoft.EntityFrameworkCore;
using System.IO;
using System;
using CostMasterAI.Core.Models;

namespace CostMasterAI.Core.Services
{
    public class AppDbContext : DbContext
    {
        // Tabel Database yang sudah ada
        public DbSet<Ingredient> Ingredients { get; set; }
        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<RecipeItem> RecipeItems { get; set; }
        public DbSet<RecipeOverhead> RecipeOverheads { get; set; }

        // --- TAMBAHAN BARU (UNTUK HALAMAN LAPORAN/PEMBUKUAN) ---
        public DbSet<Transaction> Transactions { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public AppDbContext() { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Menggunakan folder AppData lokal user agar tidak perlu izin administrator
                var folder = Environment.SpecialFolder.LocalApplicationData;
                var path = Environment.GetFolderPath(folder);

                // Nama file database
                var dbPath = Path.Join(path, "costmaster.db");

                // Koneksi ke SQLite
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }
    }
}