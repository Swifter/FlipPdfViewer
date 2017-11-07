using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.UI.Xaml.Media;
using Windows.UI.Core;

namespace PdfViewerHost
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class MainPage : Page
	{
		// a static reference to this MainPage object
		public static MainPage Current;

		/// <summary>
		/// This DependencyProperty is the name of the PDF document source for display on the MainPage header.
		/// </summary>
		public string SourceDisplayName
		{
			get { return (string)GetValue(SourceDisplayNameProperty); }
			set { SetValue(SourceDisplayNameProperty, value); }
		}

		/// <summary>
		/// This DependencyProperty is the name of the PDF document source for display on the MainPage header.
		/// </summary>
		public static readonly DependencyProperty SourceDisplayNameProperty =
			DependencyProperty.Register("SourceDisplayName", typeof(string), typeof(MainPage),
				new PropertyMetadata(null));


		public MainPage()
		{
			this.InitializeComponent();

            // This is a static public property that allows downstream pages to get a handle to the MainPage instance
            // in order to call methods that are in this class.
            Current = this;

        }

        /// <summary>
        /// Display a message to the user.
        /// This method may be called from any thread.
        /// </summary>
        /// <param name="strMessage"></param>
        /// <param name="type"></param>
        public void NotifyUser(string strMessage, NotifyType type)
        {
			// If called from the UI thread, then update immediately.
			// Otherwise, schedule a task on the UI thread to perform the update.

			if (Dispatcher.HasThreadAccess)
			{
				UpdateStatus(strMessage, type);
			}
			else
			{
				var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateStatus(strMessage, type));
			}
		}

		/// <summary>
		/// The XAML will display a green background for a Status message, and a red background for an ErrorMessage.
		/// </summary>
        public enum NotifyType
        {
            StatusMessage,
            ErrorMessage
        };

		/// <summary>
		/// This selects the background color of the PDF document to be displayed.  Changing this value
		/// does not update the background color of the currently displayed document, this value must
		/// be set before it is loaded and displayed.  Change these to whatever you want, but don't forget to change
		/// the Xaml ComboBox options to reflect whatever changes you make here.
		/// </summary>
		/// <returns></returns>
		private Color GetPdfBackgroundColor()
		{
			Color theColor = Colors.White;

			switch (ColorOptions.SelectedIndex)
			{
				case 0:
					{
						theColor = Colors.White;
						break;
					}
				case 1:
					{
						theColor = Colors.Wheat;
						break;
					}
				case 2:
					{
						theColor = Colors.Cornsilk;
						break;
					}
				case 3:
					{
						theColor = Colors.Ivory;
						break;
					}

				case 4:
					{
						theColor = Colors.LightGray;
						break;
					}
				case 5:
					{
						theColor = Colors.FloralWhite;
						break;
					}

			}

			return theColor;
		}

		/// <summary>
		/// Update the Status block.  This comes directly from a Microsoft sample.
		/// </summary>
		/// <param name="strMessage"></param>
		/// <param name="type"></param>
		private void UpdateStatus(string strMessage, NotifyType type)
        {
			switch (type)
			{
				case NotifyType.StatusMessage:
					StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Green);
					break;
				case NotifyType.ErrorMessage:
					StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Red);
					break;
			}

			StatusBlock.Text = strMessage;

			// Collapse the StatusBlock if it has no text to conserve real estate.
			StatusBorder.Visibility = (StatusBlock.Text != String.Empty) ? Visibility.Visible : Visibility.Collapsed;
			if (StatusBlock.Text != String.Empty)
			{
				StatusBorder.Visibility = Visibility.Visible;
				StatusPanel.Visibility = Visibility.Visible;
			}
			else
			{
				StatusBorder.Visibility = Visibility.Collapsed;
				StatusPanel.Visibility = Visibility.Collapsed;
			}
		}

		/// <summary>
		/// The click event handler for the Hamburger button, triggering the Splitter pane.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Splitter.IsPaneOpen = !Splitter.IsPaneOpen;
        }

		/// <summary>
		/// Launch the FileOpenPicker and get the name of a PDF document to load. This is
		/// an async method because it awaits an async method, but it doesn't return a
		/// Task so it can match the click handler signature.
		/// </summary>
		private async void LoadDocument()
		{
			LoadButton.IsEnabled = false;

			var picker = new FileOpenPicker();
			picker.FileTypeFilter.Add(".pdf");
			StorageFile file = await picker.PickSingleFileAsync();

			// if the FileOpenPicker gave us a filename, create a NavigationContext object
			// and set its parameters to pass to the LoadPdf page.
			if (null != file)
			{
				var nav = new NavigationContext();

				nav.IsFile = true;
				nav.PdfFile = file;

				nav.BackgroundColor = GetPdfBackgroundColor();

				// update the displayed loaded PDF name
				SourceDisplayName = file.Name;

				// navigate to the LoadPdf page
				PdfFrame.Navigate(typeof(Views.LoadPdf), nav);
			}

			LoadButton.IsEnabled = true;
		}

		/// <summary>
		/// This loads an embedded PDF file from the Assets directory.  Remember, any PDF file
		/// you want to load from Assets has to have its Build Action set to Content in Properties.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void LoadFromAssets_Click(object sender, RoutedEventArgs e)
		{
			var uri = new Uri("ms-appx:///Assets/semanticzoom.pdf");

			var nav = new NavigationContext();

			nav.IsFile = false;
			nav.PdfUri = uri;

			nav.BackgroundColor = GetPdfBackgroundColor();

			// update the displayed loaded PDF name
			SourceDisplayName = uri.ToString();

			// navigate to the LoadPdf page
			PdfFrame.Navigate(typeof(Views.LoadPdf), nav);
		}

		/// <summary>
		/// This loads a PDF file from an Internet URI.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void LoadFromUri_Click(object sender, RoutedEventArgs e)
		{
			var uri = new Uri("http://www.adobe.com/content/dam/Adobe/en/accessibility/products/acrobat/pdfs/acrobat-x-accessible-pdf-from-word.pdf");

			var nav = new NavigationContext();

			nav.IsFile = false;
			nav.PdfUri = uri;

			nav.BackgroundColor = GetPdfBackgroundColor();

			// update the displayed loaded PDF name
			SourceDisplayName = uri.ToString();

			// navigate to the LoadPdf page
			PdfFrame.Navigate(typeof(Views.LoadPdf), nav);

		}
	}
}
