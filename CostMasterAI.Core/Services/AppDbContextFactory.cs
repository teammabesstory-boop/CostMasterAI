using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.IO;

namespace CostMasterAI.Core.Services
{
    // Class ini khusus digunakan oleh Visual Studio saat Add-Migration / Update-Database
    // Class ini TIDAK AKAN dipanggil saat aplikasi dijalankan oleh user.
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            // 1. Tentukan lokasi database (sama persis dengan yang ada di AppDbContext)
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var path = Environment.GetFolderPath(folder);
            var dbPath = Path.Join(path, "costmaster.db");

            // 2. Buat Builder
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlite($"Data Source={dbPath}");

            // 3. Return Context baru
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}