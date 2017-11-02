using System.ComponentModel;
using Windows.UI.Xaml.Controls;

namespace PdfViewerHost.Views
{
	/// <summary>
	/// The Views of our sample are derived from BasePage, which implements the .Net property change notifications.
	/// </summary>
    public class BasePage : Page, INotifyPropertyChanged
    {
        public BasePage()
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public virtual void NotifyChanged(string propertyName)
        {
			// for those unfamiliar with the null-propagation operator of C# 6.0, see:
			// https://msdn.microsoft.com/en-us/magazine/dn802602.aspx

			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

