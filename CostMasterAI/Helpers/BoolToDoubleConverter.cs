using Microsoft.UI.Xaml.Data;
using System;

namespace CostMasterAI.Helpers
{
    public class BoolToDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // Jika Expanded (True) -> Return PositiveInfinity (Bukan NaN)
            if (value is bool isExpanded && isExpanded)
            {
                return double.PositiveInfinity;
            }

            // Jika Collapsed (False) -> Return tinggi default dari parameter (misal: 120)
            if (parameter != null && double.TryParse(parameter.ToString(), out double defaultHeight))
            {
                return defaultHeight;
            }

            return 120.0; // Fallback default
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}