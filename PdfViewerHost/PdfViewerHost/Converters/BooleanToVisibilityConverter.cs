using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace PdfViewerHost.Views
{
	[Obsolete("In Windows 10 15063 and beyond, System can bind Visibility to bool value natively, so used here for legacy OS versions.")]
	class BooleanToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// If set to True, conversion is reversed: True will become Collapsed.
        /// </summary>
        public bool IsReversed { get; set; }
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var val = System.Convert.ToBoolean(value);
            if (IsReversed)
                val = !val;

            return val
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
