using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;

using FlipPdfViewerControl;

// For the DependencyPropertyChangedHelper
using PdfViewerHost.Behaviors;

// Since the FlipPdfViewer is still under development, we're using a simple but effective Logger for UWP,
// MetroLog, which you can find here:  https://github.com/onovotny/MetroLog
//
// The logs may be found at c:/Users/yourUserName/AppData/Local/Packages/{GuidForYourApp}/LocalState/MetroLogs.
// You can find the {GuidForYourApp} by clicking the Package.appxmanifest file in Visual Studio, navigating to 
// the Packaging tab, and copying the Package Name, which is a Guid. A different log file is generated each day,
// which changes at midnight UTC.
using MetroLog;

namespace PdfViewerHost.Views
{
    public sealed partial class LoadPdf : BasePage
    {
		// a reference to the MainPage
        private MainPage rootPage = MainPage.Current;

		// backing store variables for public properties
        private Uri _pdfSource;
        private string _pdfPassword;
		private bool _printingIsSupported = false;

		// MetroLog logger
		private ILogger log = LogManagerFactory.DefaultLogManager.GetLogger<LoadPdf>();

		/// <summary>
		/// The Default constructor.
		/// </summary>
		public LoadPdf()
        {
            this.InitializeComponent();
        }

		/// <summary>
		/// The Uri for the Pdf file from Internet URLs or embedded resource PDF files.  The FlipViewerControl's
		/// PdfSource dependency property is bound to this in the xaml, so a change here will trigger a change in
		/// the Pdf file in the control.
		/// </summary>
        public Uri PdfSource
        {
            get
            {
                return _pdfSource;
            }

            set
            {
                if (_pdfSource != value)
                {
                    _pdfSource = value;
                    NotifyChanged("PdfSource");
                }
            }
        }

		/// <summary>
		/// A boolean indicating that the FlipPdfViewer control is running on a system where
		/// printing is supported. This will be true on all desktop and most mobile systems, 
		/// including Windows Phone.  However, it gets set to False when loading a PDF from a 
		/// file, so it's now hardcoded to True.
		/// </summary>
		public bool PrintingIsSupported
		{
			get
			{
				//return _printingIsSupported;

				// returning true because printing is always supported on the desktop
				// and somehow this changes to False when we load from a file and I can't
				// yet figure out why, so this hack gets around that. But, it's a hack.
				// It works normally on embedded and URI-loaded PDF files.
				return true;
			}

			set
			{
				if(_printingIsSupported != value)
				{
					log.Trace(string.Format("PrintingIsSupported now is {0}", _printingIsSupported.ToString()));

					_printingIsSupported = value;
					NotifyChanged("PrintingIsSupported");
				}
			}
		}

		/// <summary>
		/// The password for password-protected PDF documents.
		/// </summary>
        public string PdfPassword
        {
            get
            {
                return _pdfPassword;
            }

            set
            {
                if (_pdfPassword != value)
                {
                    _pdfPassword = value;
                    NotifyChanged("PdfPassword");
                }
            }
        }

		/// <summary>
		/// The text size (font size) for the buttons on this page holding our FlipPdfViewer.
		/// </summary>
        public double TextSize { get; set; } = 15;

		/// <summary>
		/// When we leave the page, unregister the FlipPdfViewer from the print system.
		/// </summary>
		/// <param name="e">NavigationEventArgs, ignored here.</param>
		protected override void OnNavigatedFrom(NavigationEventArgs e)
		{
			if(PrintingIsSupported)
			{
				FlipPdfViewer.UnRegisterForPrinting();
			}

			SizeChanged -= LoadPdf_SizeChanged;
		}

		/// <summary>
		/// Triggered by the MainPage's PdfFrame.Navigate calls in the various Button
		/// event handlers for loading Pdf documents from embedded PDF files, the file system,
		/// or an Internet URI.
		/// </summary>
		/// <param name="e">NavigationEventArgs, a NavigationContext object.</param>
		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);

			// cast the passed parameter as a NavigationContext object
			var parameters = e.Parameter as NavigationContext;

			SizeChanged += LoadPdf_SizeChanged;

			// if we actually have an object passed...
			if(null != parameters)
			{
				// set the FlipPdfViewer's background color before rendering
				FlipPdfViewer.PdfBackgroundColor = parameters.BackgroundColor;

				bool isFile = parameters.IsFile;

				// if the passed NavigationContext is a StorageFile
				if (isFile)
				{
					StorageFile pdfFile = parameters.PdfFile;

					// open the Pdf file
					OpenStorageFile(pdfFile);
					ForwardStatusMessage("Opening PDF File from Storage");
				}
				else
				{
					// It's not a file but a Uri, so set our Uri property
					PdfSource = parameters.PdfUri;
				}

				// register for changes in the FlipPdfViewer's PdfStatusMessage DependencyProperty through a DependencyPropertyChangedHelper
				DependencyPropertyChangedHelper helperStatus = new DependencyPropertyChangedHelper(this.FlipPdfViewer, nameof(this.FlipPdfViewer.PdfStatusMessage));

				// hook up the StatusChanged event handler
				helperStatus.PropertyChanged += PdfStatusMessage_PropertyChanged;

				// register for changes in the FlipPdfViewer's PdfErrorMessage DependencyProperty
				DependencyPropertyChangedHelper helperError = new DependencyPropertyChangedHelper(this.FlipPdfViewer, nameof(this.FlipPdfViewer.PdfErrorMessage));

				// hook up the ErrorChanged event handler
				helperError.PropertyChanged += PdfErrorMessage_PropertyChanged;

				PrintingIsSupported = FlipPdfViewer.PrintingIsSupported;

				// set forward message delegate instances
				FlipPdfViewer.HostStatusMsgHandler = ForwardStatusMessage;
				FlipPdfViewer.HostErrorMsgHandler = ForwardErrorMessage;

				// if the FliipPdfViewer supports printing, register it with the print system.  We do this here
				// so it can be unregisterd in OnNavigatedFrom.
				if (PrintingIsSupported)
				{
					// register FlipPdfViewer for printing
					FlipPdfViewer.RegisterForPrinting();
				}
			}
		}

		/// <summary>
		/// We have to resize the Pdf page currently displayed when we resize.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void LoadPdf_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			FlipPdfViewer.ZoomReset();
		}

		/// <summary>
		/// Report the FlipPdfViewer's PdfStatusMessage changes to the Mainpage's Status area
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void PdfStatusMessage_PropertyChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			try
			{
				rootPage.NotifyUser((string)e.NewValue, MainPage.NotifyType.StatusMessage);
			}
			catch (Exception ex)
			{
				rootPage.NotifyUser(ex.Message, MainPage.NotifyType.ErrorMessage);
			}		
		}

		/// <summary>
		/// Report the FlipPdfViewer's PdfErrorMessage changes to the Mainpage's Status area
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void PdfErrorMessage_PropertyChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			try
			{
				rootPage.NotifyUser((string)e.NewValue, MainPage.NotifyType.ErrorMessage);
			}
			catch (Exception ex)
			{
				rootPage.NotifyUser(ex.Message, MainPage.NotifyType.ErrorMessage);
			}
		}

		/// <summary>
		/// Send the status message to the MainPage
		/// </summary>
		/// <param name="message"></param>
		public void ForwardStatusMessage(string message)
		{
			try
			{
				rootPage.NotifyUser(message, MainPage.NotifyType.StatusMessage);
			}
			catch (Exception ex)
			{
				rootPage.NotifyUser(ex.Message, MainPage.NotifyType.ErrorMessage);
			}
		}

		/// <summary>
		/// Send the error message to the MainPage
		/// </summary>
		/// <param name="message"></param>
		public void ForwardErrorMessage(string message)
		{
			try
			{
				rootPage.NotifyUser(message, MainPage.NotifyType.ErrorMessage);
			}
			catch (Exception ex)
			{
				rootPage.NotifyUser(ex.Message, MainPage.NotifyType.ErrorMessage);
			}
		}

		/// <summary>
		/// Open a StorageFile as a RandomAccessStream.
		/// </summary>
		/// <param name="file">A StorageFile passed in the NavigationEventArgs NavigationContext object.</param>
		/// <returns>A Task</returns>
		private void OpenStorageFile(StorageFile file)
		{

			if (file != null)
			{
				rootPage.NotifyUser("Rendering PDF", MainPage.NotifyType.StatusMessage);

				FlipPdfViewer.StorageFileSource = file;
			}
		}

		/// <summary>
		/// Decrement the Pdf page shown in the FlipPdfViewer.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void MoveFlipViewBack(object sender, RoutedEventArgs e)
        {
            FlipPdfViewer.DecrementPage();
        }

		/// <summary>
		/// Increment the Pdf page shown in the FlipPdfViewer.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
        private void MoveFlipViewForward(object sender, RoutedEventArgs e)
        {
            FlipPdfViewer.IncrementPage();
        }

		/// <summary>
		/// Zoom into the current page of the FlipPdfViewer.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
        private void ZoomIn(object sender, RoutedEventArgs e)
        {
            FlipPdfViewer.ZoomIn();
        }

		/// <summary>
		/// Zoom out of the current page of the FlipPdfViewer.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
        private void ZoomOut(object sender, RoutedEventArgs e)
        {
            FlipPdfViewer.ZoomOut();
        }

		/// <summary>
		/// Restore the current page of the FlipPdfViewer to its original size and aspect ratio.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
        private void DoFitDocumentImageToScreen(object sender, RoutedEventArgs e)
        {
			FlipPdfViewer.ZoomReset();
		}

		/// <summary>
		/// Print the current Pdf document.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
        private async void OnPrintButtonClick(object sender, RoutedEventArgs e)
        {
			await FlipPdfViewer.OnPrintButtonClick();
		}

	
	}
}
