using System;
using Windows.UI.Xaml.Data;

namespace Music
{
    public class SliderValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var seconds = System.Convert.ToInt32(value) / 10;
            return string.Format("{0:00}:{1:00}", seconds / 60 % 60, seconds % 60);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}