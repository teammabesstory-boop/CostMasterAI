using Microsoft.UI.Xaml.Data;
using System;

namespace CostMasterAI.Helpers
{
    public class DateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return "";

            string format = parameter as string ?? "dd MMM yyyy";

            if (value is DateTime dt)
                return dt.ToString(format);

            if (value is DateTimeOffset dto)
                return dto.ToString(format);

            return value.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
