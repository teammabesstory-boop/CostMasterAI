using Microsoft.UI.Xaml.Data;
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CostMasterAI.Helpers
{
    public class CurrencyConverter : IValueConverter
    {
        // Dari Angka (Database) -> ke Teks Rupiah (Tampilan)
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is decimal d)
            {
                return d.ToString("C0", new CultureInfo("id-ID"));
            }
            if (value is double db)
            {
                return db.ToString("C0", new CultureInfo("id-ID"));
            }
            if (value is int i)
            {
                return i.ToString("C0", new CultureInfo("id-ID"));
            }
            return value;
        }

        // Dari Teks Rupiah (Edit di Tabel) -> ke Angka (Database)
        // INI YANG BIKIN ERROR SEBELUMNYA
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is string s)
            {
                // 1. Hapus semua karakter yang bukan angka dan bukan koma (buat desimal)
                // Kita apus "Rp", " titik", spasi, dll.
                // Regex: [^0-9,] artinya "selain angka 0-9 dan koma"
                var cleanString = Regex.Replace(s, @"[^0-9,]", "");

                // 2. Coba ubah jadi decimal
                if (decimal.TryParse(cleanString, NumberStyles.Any, new CultureInfo("id-ID"), out var result))
                {
                    return result;
                }
            }
            // Kalau gagal parse, balikin 0 aja biar gak crash
            return 0m;
        }
    }
}