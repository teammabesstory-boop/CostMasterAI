using System;
using System.Collections.Generic;

namespace CostMasterAI.Helpers
{
    public static class UnitHelper
    {
        // Kita definisikan semua ke "Base Unit"
        // Berat -> Base: Gram
        // Volume -> Base: ML
        // Pcs -> Base: Pcs

        private static readonly Dictionary<string, double> ToBaseMultiplier = new()
        {
            // Mass (Base: Gram)
            { "Gram", 1 },
            { "Kg", 1000 },
            { "Ons", 100 },

            // Volume (Base: ML)
            { "ML", 1 },
            { "Liter", 1000 },
            { "Sdm", 15 }, // Sendok Makan (Est. 15ml)
            { "Sdt", 5 },  // Sendok Teh (Est. 5ml)
            { "Cup", 240 }, // Cup Amerika

            // Count
            { "Pcs", 1 }
        };

        // Kategori Satuan biar gak salah convert (Masa Kg di-convert ke Pcs?)
        public static string GetCategory(string unit)
        {
            return unit switch
            {
                "Gram" or "Kg" or "Ons" => "Mass",
                "ML" or "Liter" or "Sdm" or "Sdt" or "Cup" => "Volume",
                "Pcs" => "Count",
                _ => "Unknown"
            };
        }

        public static double GetConversionRate(string fromUnit, string toUnit)
        {
            // Kalau satuannya sama, ya 1:1
            if (fromUnit == toUnit) return 1;

            // Cek apakah ada di kamus kita
            if (!ToBaseMultiplier.ContainsKey(fromUnit) || !ToBaseMultiplier.ContainsKey(toUnit))
            {
                return 0; // Gak kenal satuannya
            }

            // Validasi Kategori (Cegah Kg -> Liter tanpa Massa Jenis)
            // *Note: Buat simplifikasi MVP, kita anggap 1 ML ~= 1 Gram (Density Air) 
            // kalau user maksa convert Mass <-> Volume. Ini "Magic" yang user suka.
            var cat1 = GetCategory(fromUnit);
            var cat2 = GetCategory(toUnit);

            // Kalau Pcs, harus ke Pcs lagi (kecuali kita punya data berat per pcs, tapi itu nanti)
            if (cat1 == "Count" || cat2 == "Count")
            {
                if (cat1 != cat2) return 0; // Gak bisa convert Pcs ke Gram tanpa data spesifik
            }

            // Rumus: (1 * Multiplier Awal) / Multiplier Akhir
            // Contoh: 1 Kg ke Gram -> (1 * 1000) / 1 = 1000
            // Contoh: 1 Gram ke Kg -> (1 * 1) / 1000 = 0.001
            return ToBaseMultiplier[fromUnit] / ToBaseMultiplier[toUnit];
        }

        // List satuan buat Dropdown
        public static List<string> CommonUnits => new()
        {
            "Gram", "Kg", "Ons",
            "ML", "Liter", "Sdm", "Sdt", "Cup",
            "Pcs"
        };
    }
}