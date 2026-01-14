using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace CostMasterAI.Helpers
{
    // Tugas: Kalau True -> Muncul (Visible), Kalau False -> Ilang (Collapsed)
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool b)
            {
                // Kalau parameter dikasih string "Inverse", kita balik logikanya
                if (parameter as string == "Inverse")
                    return b ? Visibility.Collapsed : Visibility.Visible;

                return b ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}