using ExcelDataReader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace CostMasterAI.Services
{
    public class DataSeeder
    {
        private readonly AppDbContext _dbContext;

        public DataSeeder(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task SeedFromExcelAsync(StorageFile file)
        {
            // 1. Registrasi Encoding (Wajib untuk ExcelDataReader)
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            var newIngredients = new List<Ingredient>();

            // 2. Buka Stream dari File
            using (var stream = await file.OpenStreamForReadAsync())
            {
                // Auto-detect format (xls atau xlsx)
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    // 3. Baca Header dulu (Skip baris pertama)
                    if (!reader.Read()) return;

                    // 4. Baca Data Baris per Baris
                    while (reader.Read())
                    {
                        // Pastikan baris tidak kosong
                        if (reader.GetValue(0) == null) continue;

                        try
                        {
                            // Mapping Kolom Excel (Sesuai urutan file Anda)
                            // 0: Category, 1: Brand, 2: Variant, 3: PackSizes, 4: PriceLow, 5: PriceHigh

                            string category = reader.GetValue(0)?.ToString()?.Trim() ?? "";
                            string brand = reader.GetValue(1)?.ToString()?.Trim() ?? "";
                            string variant = reader.GetValue(2)?.ToString()?.Trim() ?? "";
                            string packSizeRaw = reader.GetValue(3)?.ToString()?.Split('/')[0].Trim() ?? "1pcs"; // Ambil ukuran pertama

                            // Parse Harga (Average Low & High)
                            double pLow = ConvertToDouble(reader.GetValue(4));
                            double pHigh = ConvertToDouble(reader.GetValue(5));
                            decimal avgPrice = (decimal)(pLow + pHigh) / 2;

                            // Parse Unit & Qty
                            (double qty, string unit) = ParsePackSize(packSizeRaw);

                            // Buat Nama Lengkap
                            string fullName = $"{category} {brand} {variant}".Trim();

                            // Cek Duplikasi (Optional: Skip jika nama persis sudah ada)
                            if (!_dbContext.Ingredients.Any(i => i.Name == fullName))
                            {
                                newIngredients.Add(new Ingredient
                                {
                                    Name = fullName,
                                    Category = category, // Simpan kategori jika ada field-nya di Model Ingredient
                                    PricePerPackage = avgPrice,
                                    QuantityPerPackage = qty,
                                    Unit = unit,
                                    YieldPercent = 100
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error parsing row: {ex.Message}");
                            // Lanjut ke baris berikutnya walau ada error di satu baris
                        }
                    }
                }
            }

            // 5. Simpan ke Database
            if (newIngredients.Any())
            {
                await _dbContext.Ingredients.AddRangeAsync(newIngredients);
                await _dbContext.SaveChangesAsync();
            }
        }

        // Helper: Convert object Excel ke Double dengan aman
        private double ConvertToDouble(object value)
        {
            if (value == null) return 0;
            if (double.TryParse(value.ToString(), out double result)) return result;
            return 0;
        }

        // Helper: Logic Parsing Ukuran (Sama seperti sebelumnya)
        private (double, string) ParsePackSize(string raw)
        {
            raw = raw.ToLower().Replace(" ", "");

            if (raw.Contains("kg"))
            {
                double.TryParse(raw.Replace("kg", ""), out double val);
                return (val * 1000, "Gram");
            }
            else if (raw.Contains("gr") || raw.Contains("g"))
            {
                double.TryParse(raw.Replace("gr", "").Replace("g", ""), out double val);
                return (val, "Gram");
            }
            else if (raw.Contains("ml"))
            {
                double.TryParse(raw.Replace("ml", ""), out double val);
                return (val, "ML");
            }
            else if (raw.Contains("l") && !raw.Contains("ml")) // Liter
            {
                double.TryParse(raw.Replace("l", ""), out double val);
                return (val * 1000, "ML");
            }

            // Default
            double.TryParse(System.Text.RegularExpressions.Regex.Match(raw, @"\d+").Value, out double defVal);
            return (defVal > 0 ? defVal : 1, "Pcs");
        }
    }
}