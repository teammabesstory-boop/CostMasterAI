using Microsoft.UI.Xaml.Data;
using System;

namespace CostMasterAI.Helpers
{
    public class PercentageToWidthConv : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double v)
            {
                // Kalau ada parameter (faktor pengali), pakai itu. Default kali 2 biar barnya panjang.
                double factor = 2.0;
                if (parameter is string p && double.TryParse(p, out var f))
                {
                    factor = f;
                }

                // Pastikan hasil tidak negatif
                return Math.Max(0, v * factor);
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}