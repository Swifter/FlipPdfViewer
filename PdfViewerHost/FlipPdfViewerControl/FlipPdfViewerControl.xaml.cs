using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Graphics.Printing;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;


// Since the FlipPdfViewer is still under development, we're using a simple but effective Logger for UWP,
// MetroLog, which you can find here:  https://github.com/onovotny/MetroLog
//
// The logs may be found at c:/Users/yourUserName/AppData/Local/Packages/{GuidForYourApp}/LocalState/MetroLogs.
// You can find the {GuidForYourApp} by clicking the Package.appxmanifest file in Visual Studio, navigating to 
// the Packaging tab, and copying the Package Name, which is a Guid. A different log file is generated each day,
// which changes at midnight UTC.
using MetroLog;
using FlipPdfViewerControl.PrintCode;

namespace FlipPdfViewerControl
{
    public sealed partial class FlipPdfViewerControl : UserControl, INotifyPropertyChanged
    {
		// this supports the INotifyPropertyChanged interface
		public event PropertyChangedEventHandler PropertyChanged;

		// call PropertyChanged on a changed property
		public void NotifyChanged(string propertyName)
		{
			// for those unfamiliar with the null-propagation operator of C# 6.0, see:
			// https://msdn.microsoft.com/en-us/magazine/dn802602.aspx

			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		// Metrolog logger
		private static ILogger log= LogManagerFactory.DefaultLogManager.GetLogger<FlipPdfViewerControl>();

		/// <summary>
		/// This represents a Pdf document source Uri, either an Internet URL, or an embedded resource Uri.  A change
		/// in this property will trigger loading a new Pdf document from this source.
		/// </summary>
		public Uri Source
        {
            get { return (Uri)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(Uri), typeof(FlipPdfViewerControl),
                new PropertyMetadata(null, OnSourceChanged));

		/// <summary>
		/// This represents a StorageFile from opening a Pdf document from the file system.  See LoadPdf.xaml.cs for
		/// how to generate this StorageFile from a UWP FilePicker.  A change in this property will trigger loading a
		/// new Pdf document from this source.
		/// </summary>
		public StorageFile StorageFileSource
		{
			get { return (StorageFile)GetValue(StorageFileSourceProperty); }
			set { SetValue(StorageFileSourceProperty, value); }
		}

		// Using a DependencyProperty as the backing store for StorageFileSource.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty StorageFileSourceProperty =
			DependencyProperty.Register("StorageFileSource", typeof(StorageFile), typeof(FlipPdfViewerControl),
				new PropertyMetadata(null, OnStorageFileSourceChanged));


		/// <summary>
		/// The optional password of the Pdf Document to be loaded.
		/// </summary>
		public string PdfPassword
		{
			get { return (string)GetValue(PdfPasswordProperty); }
			set { SetValue(PdfPasswordProperty, value); }
		}

		// Using a DependencyProperty as the backing store for PdfPassword.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty PdfPasswordProperty =
			DependencyProperty.Register("PdfPassword", typeof(string), typeof(FlipPdfViewerControl),
				new PropertyMetadata(string.Empty));

		/// <summary>
		/// The background color with which we will render the Pdf document.
		/// </summary>
		public Color PdfBackgroundColor
		{
			get { return (Color)GetValue(PdfBackgroundColorProperty); }
			set { SetValue(PdfBackgroundColorProperty, value); }
		}

		// Using a DependencyProperty as the backing store for PdfBackgroundColor.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty PdfBackgroundColorProperty =
			DependencyProperty.Register("PdfBackgroundColor", typeof(Color), typeof(FlipPdfViewerControl),
				new PropertyMetadata(null));

		/// <summary>
		/// Set True if Zoom is enabled on the Pdf Document.
		/// </summary>
		public bool IsZoomEnabled
        {
            get { return (bool)GetValue(IsZoomEnabledProperty); }
            set { SetValue(IsZoomEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsZoomEnabledProperty =
            DependencyProperty.Register("IsZoomEnabled", typeof(bool), typeof(FlipPdfViewerControl),
                new PropertyMetadata(true, OnIsZoomEnabledChanged));

		/// <summary>
		/// The 1-based count of pages in the Pdf Document.
		/// </summary>
        public int PageCount
        {
            get { return (int)GetValue(PageCountProperty); }
            set { SetValue(PageCountProperty, value);  }
        }

        public static readonly DependencyProperty PageCountProperty =
            DependencyProperty.Register("PageCount", typeof(int), typeof(FlipPdfViewerControl),
            new PropertyMetadata(null));

		/// <summary>
		/// The 1-based count of the currently displayed page number.
		/// </summary>
        public int CurrentPageNumber
        {
            get { return (int)GetValue(CurrentPageNumberProperty); }
            set { SetValue(CurrentPageNumberProperty, value);  }
        }

        public static readonly DependencyProperty CurrentPageNumberProperty =
            DependencyProperty.Register("CurrentPageNumberCount", typeof(int), typeof(FlipPdfViewerControl),
            new PropertyMetadata(null));

		/// <summary>
		/// Any status message sent by the control.
		/// </summary>
		public string PdfStatusMessage
		{
			get { return (string)GetValue(PdfStatusMessageProperty); }
			set { SetValue(PdfStatusMessageProperty, value); }
		}

		// Using a DependencyProperty as the backing store for PdfStatusMessage.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty PdfStatusMessageProperty =
			DependencyProperty.Register("PdfStatusMessage", typeof(string), typeof(FlipPdfViewerControl),
				new PropertyMetadata(null));

		/// <summary>
		/// Any error message sent by the control.
		/// </summary>
		public string PdfErrorMessage
		{
			get { return (string)GetValue(PdfErrorMessageProperty); }
			set { SetValue(PdfErrorMessageProperty, value); }
		}

		// Using a DependencyProperty as the backing store for PdfStatusMessage.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty PdfErrorMessageProperty =
			DependencyProperty.Register("PdfErrorMessage", typeof(string), typeof(FlipPdfViewerControl),
				new PropertyMetadata(null));

		// Set in Load, used throughout
		private PdfDocument _currentPdfDocument = null;

		// set in the constructor by querying the PrintManager object.  However,
		// this gets wrongly set to false when loading a Pdf Document from the file system.
		// Callers should just assume printing is supported on all desktop systems.  See LoadPdf.xaml.cs
		private bool _printingIsSupported = true;

		// a single static HttpClient instance is used for downloading Pdf documents from URIs
		private static HttpClient _httpClient = new HttpClient();

		// The count of the last Pdf page loaded in the PdfPages observable collection.
		// This could be different from PageCount, the total number of pages in the Pdf Document.
		// Pages are loaded one at a time to avoid FlipView data virtualization issues.
		private int _lastPdfImageLoaded = 0;

		// the printing object, instantiated in the constructor if printing is supported
		private FlipViewPagePrintHelper _printHelper;

		// error constants for Pdf document loading failures.
		const int WrongPassword = unchecked((int)0x8007052b); // HRESULT_FROM_WIN32(ERROR_WRONG_PASSWORD)
		const int GenericFail = unchecked((int)0x80004005);   // E_FAIL

		/// <summary>
		/// An instance of this delegate should be supplied by the host UserControl to pass
		/// error and other messages back to it.
		/// </summary>
		/// <param name="message"></param>
		public delegate void NotifyHost(string message);

		/// <summary>
		/// Assign the host control's NotifyHost for Status to this variable
		/// </summary>
		public NotifyHost HostStatusMsgHandler;

		/// <summary>
		/// Assign the host control's NotifyHost for Errors to this variable
		/// </summary>
		public NotifyHost HostErrorMsgHandler;

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
				return _printingIsSupported;
			}

			set
			{
				if (_printingIsSupported != value)
				{
					log.Trace(string.Format("PrintingIsSupported now is {0}", _printingIsSupported.ToString()));

					_printingIsSupported = value;
					NotifyChanged(nameof(PrintingIsSupported));
				}
			}
		}

		// an internal property
		internal ZoomMode ZoomMode
        {
            get { return IsZoomEnabled ? ZoomMode.Enabled : ZoomMode.Disabled; }
        }

		/// <summary>
		/// Clear PdfPages, set sources to null, clear counters.
		/// </summary>
        public void ClearPdfImages()
        {
            // clear it out
            PdfPages.Clear();
            PageCount = 0;
			_lastPdfImageLoaded = 0;
            Source = null;
			StorageFileSource = null;
        }

		/// <summary>
		/// Allows adding of BitmapImage objects to the control without a Pdf Document, so the 
		/// control can act as an image viewer as well as a Pdf Document viewer.
		/// </summary>
		/// <param name="img"></param>
        public void AddPdfImage(BitmapImage img)
        {
            if(null != img)
            {
                PdfPages.Add(img);
                _lastPdfImageLoaded++;
            }      
        }

		/// <summary>
		/// Increments the page shown in the control.  Adds the page to the PdfPages ObservableCollection if
		/// it has not yet been loaded. This is done to avoid problems with FlipView data virtualization.
		/// </summary>
        public async void IncrementPage()
        {
            if(flipView.SelectedIndex >= 0 && flipView.SelectedIndex < PageCount - 1)
            {
				if(flipView.SelectedIndex >= _lastPdfImageLoaded - 1)
				{
					await LoadPdfPage((uint)(flipView.SelectedIndex + 1));
				}
                flipView.SelectedIndex += 1;
            }
        }

		/// <summary>
		/// Decrements the page shown in the control.
		/// </summary>
        public void DecrementPage()
        {
            if(flipView.SelectedIndex > 0)
            {
                flipView.SelectedIndex -= 1;
            }
        }

		/// <summary>
		/// If Zoom is enabled, zooms into the current page in the control, through its ImageViewer.
		/// </summary>
        public void ZoomIn()
        {
            if(IsZoomEnabled)
            {
                FlipViewItem test = (FlipViewItem)flipView.ContainerFromIndex(flipView.SelectedIndex);
                var viewer = (ImageViewer)test.ContentTemplateRoot;

                viewer.ZoomIn();
            }
        }

		/// <summary>
		/// If Zoom is enabled, zooms out of the current page in the control, through its ImageViewer.
		/// </summary>
        public void ZoomOut()
        {
            if(IsZoomEnabled)
            {
                FlipViewItem test = (FlipViewItem)flipView.ContainerFromIndex(flipView.SelectedIndex);
                var viewer = (ImageViewer)test.ContentTemplateRoot;

                viewer.ZoomOut();
            }
        }

		/// <summary>
		/// Resets the current page in the control to its correct size and aspect ratio, through its ImageViewer.
		/// </summary>
        public void ZoomReset()
        {	
			if(_lastPdfImageLoaded > 0)
			{
				FlipViewItem test = (FlipViewItem)flipView.ContainerFromIndex(flipView.SelectedIndex);
				var viewer = (ImageViewer)test.ContentTemplateRoot;

				viewer.ZoomReset();
			}
		}

		/// <summary>
		/// This gates the multiple calls of Source dependency property change events.
		/// </summary>
		private bool _autoLoad = true;

		// This is where we store the loaded Pdf Document pages after they are loaded. 
		// Changes to this collection update the UI.
        internal ObservableCollection<BitmapImage> PdfPages
        {
            get;
            set;
        } = new ObservableCollection<BitmapImage>();

		/// <summary>
		/// Default constructor.
		/// </summary>
        public FlipPdfViewerControl()
        {
			// default background color
            this.Background = new SolidColorBrush(Colors.DarkGray);

			// hook the keyboard
            Window.Current.CoreWindow.CharacterReceived += CoreWindow_CharacterReceived;

            // Loaded is where we hook up component control event handlers
            Loaded += FlipPdfViewerControl_Loaded;

			// set the default Pdf background color
			PdfBackgroundColor = Windows.UI.Colors.Beige;

			// see if printing is supported by this OS and if so, instantiate the print code.
			if(PrintManager.IsSupported())
			{
				PrintingIsSupported = true;
				_printHelper = new FlipViewPagePrintHelper(this, SetStatusMessage, SetErrorMessage);
			}
			else
			{
				PrintingIsSupported = false;
				PdfErrorMessage = "Printing is not supported.";
				HostErrorMsgHandler?.Invoke(PdfErrorMessage);
			}

			this.InitializeComponent();
        }

		/// <summary>
		/// Register the control for printing.  This should be called in the OnNavigatedTo override of
		/// the page that hosts this control.
		/// </summary>
		public void RegisterForPrinting()
		{
			if (null != _printHelper)
			{
				_printHelper.RegisterForPrinting();
				PdfStatusMessage = "Registered for Printing";
				HostStatusMsgHandler?.Invoke(PdfStatusMessage);
			}
			else
			{
				PrintingIsSupported = false;
				PdfErrorMessage = "Printing is not supported.";
				HostErrorMsgHandler?.Invoke(PdfErrorMessage);
			}
		}

		/// <summary>
		/// Un-register the control for printing. This should be called in the OnNavigatedFrom override
		/// of the page that hosts this control.
		/// </summary>
		public void UnRegisterForPrinting()
		{
			if(null != _printHelper)
			{
				_printHelper.UnregisterForPrinting();
				PdfStatusMessage = "Unregistered for Printing";
				HostStatusMsgHandler?.Invoke(PdfStatusMessage);
			}
			else
			{
				PrintingIsSupported = false;
				PdfErrorMessage = "Printing is not supported.";
				HostErrorMsgHandler?.Invoke(PdfErrorMessage);
			}
		}

		/// <summary>
		/// Passed to the printing code so it can update our host page with status messages.
		/// </summary>
		/// <param name="message"></param>
		private async void SetStatusMessage(string message)
		{
			await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
				() =>
				{
					// this will trigger change notifications
					PdfStatusMessage = message;
					HostStatusMsgHandler?.Invoke(PdfErrorMessage);
				});


		}

		/// <summary>
		/// Passed to the printing code so it can update our host page with error messages.
		/// </summary>
		/// <param name="message"></param>
		private async void SetErrorMessage(string message)
		{
			await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
				() =>
				{
					// this will trigger change notifications
					PdfErrorMessage = message;
					HostErrorMsgHandler?.Invoke(PdfErrorMessage);
				});
		}

		/// <summary>
		/// This is where we hook up internal event handlers.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
        private void FlipPdfViewerControl_Loaded(object sender, RoutedEventArgs e)
        {
            flipView.SelectionChanged += FlipView_SelectionChanged;
        }

        /// <summary>
		/// Increment/decrement the page counter.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
        private void FlipView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int i = ((FlipView)sender).SelectedIndex;
            CurrentPageNumber = i + 1;
        }

		/// <summary>
		/// Handle keyboard zoom interaction with the flipView control.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		private void CoreWindow_CharacterReceived(CoreWindow sender, CharacterReceivedEventArgs args)
        {
            if (args.KeyCode == 43 || args.KeyCode == 61) // plus sign / equals sign
            {
                ZoomIn();
            }

            if (args.KeyCode == 45) // minus sign
            {
                ZoomOut();
            }

            if (args.KeyCode == 8) // enter key
            {

                ZoomReset();
            }
        }

		/// <summary>
		/// Hooks the instance method for changes in ZoomEnabled property.
		/// </summary>
		/// <param name="d"></param>
		/// <param name="e"></param>
        private static void OnIsZoomEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((FlipPdfViewerControl)d).OnIsZoomEnabledChanged();
        }

		/// <summary>
		/// Hooks the instance method for changes in Pdf Document Source property.
		/// </summary>
		/// <param name="d"></param>
		/// <param name="e"></param>
        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((FlipPdfViewerControl)d).OnSourceChanged();
        }

		/// <summary>
		/// Hooks the instance method for changed in Pdf Document StorageFileSource property.
		/// </summary>
		/// <param name="d"></param>
		/// <param name="e"></param>
		private static void OnStorageFileSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			((FlipPdfViewerControl)d).OnStorageFileSourceChanged();
		}

		/// <summary>
		/// Calls the INotifyPropertyChanged interface for Zoom mode changes.
		/// </summary>
        private void OnIsZoomEnabledChanged()
        {
            NotifyChanged(nameof(ZoomMode));
        }

		/// <summary>
		/// Event handler for loading a Pdf Document from a web or file Uri source.
		/// </summary>
        private async void OnSourceChanged()
        {
            log.Trace("In OnSourceChanged()");

            if(null != Source)
            {
                log.Trace(string.Format("Source = {0}", Source.ToString()));
            }
            else
            {
                log.Trace("Source was null.");
            }

			try
			{
				if (_autoLoad && Source != null)
				{
					// OnSourceChanged is getting called twice each time through 
					// the Dependency Property system, so let's
					// gate LoadAsync() from being called again until Load() is finished.
					// We'll set _autoLoad to true again at the end of the Load() method.
					_autoLoad = false;

					log.Trace("_autoLoad is true, about to LoadAsync()");
					await LoadAsync();
				}
			}
			catch (Exception ex)
			{
				PdfErrorMessage = string.Format("Exception in OnSourceChanged:{0}", ex.Message);
				HostErrorMsgHandler?.Invoke(PdfErrorMessage);
			}
        }

		/// <summary>
		/// Event handler for loading a Pdf Document from a StorageFile source.
		/// </summary>
		private async void OnStorageFileSourceChanged()
		{
			log.Trace("In OnStorageFileSourceChanged()");

			if (null != Source)
			{
				log.Trace(string.Format("Source = {0}", StorageFileSource.Name.ToString()));
			}
			else
			{
				log.Trace("StoragefileSource was null.");
			}

			try
			{
				if (_autoLoad && StorageFileSource != null)
				{

					// OnStorageFileSourceChanged is getting called twice each time through 
					// the Dependency Property system, so let's
					// gate Load() from being called again until Load() is finished.
					// We'll set _autoLoad to true again at the end of the Load() method.
					_autoLoad = false;

					PdfDocument pdfDocument = null;

					try
					{
						pdfDocument = await PdfDocument.LoadFromFileAsync(StorageFileSource, PdfPassword);
					}
					catch (Exception ex)
					{
						switch (ex.HResult)
						{
							case WrongPassword:
								PdfErrorMessage = "Document is password-protected and password is incorrect.";
								break;

							case GenericFail:
								PdfErrorMessage = "Document is not a valid PDF.";
								break;

							default:
								// File I/O errors are reported as exceptions.
								PdfErrorMessage = ex.Message;
								break;
						}
					}
					
					// loads the first page of the PdfDocument into PdfPages collection.
					await Load(pdfDocument);
				}
			}
			catch (Exception ex)
			{
				///PdfErrorMessage = string.Format("Exception in OnStorageFileSourceChanged:{0}{1}Storage File:{2} ", ex.Message, Environment.NewLine, StorageFileSource.Name);
				HostErrorMsgHandler?.Invoke(PdfErrorMessage);
			}
		}

		/// <summary>
		/// Determines the type of Uri in the Source property and calls the appropriate loading method.
		/// </summary>
		/// <returns></returns>
        private async Task LoadAsync()
        {
            log.Trace("In LoadAsync()");

			try
			{
				if (Source.IsFile || !Source.IsWebUri())
				{
					log.Trace("Source.IsFile and not IsWebUri, about to LoadFromLocalAsync.");

					HostStatusMsgHandler?.Invoke("Opening PDF File from Storage");

					await LoadFromLocalAsync();
				}
				else if (Source.IsWebUri())
				{
					log.Trace("Source.IsWebUri and not IsFile, about to LoadFromRemoteAsync.");

					HostStatusMsgHandler?.Invoke("Opening PDF File from Web");

					await LoadFromRemoteAsync();
				}
				else
				{
					throw new ArgumentException($"Source '{Source.ToString()}' could not be recognized!");
				}
			} 
			catch (Exception ex)
			{
				PdfErrorMessage = string.Format("Exception in LoadAsync():{0}", ex.Message);
				HostErrorMsgHandler?.Invoke(PdfErrorMessage);
			}
        }

		/// <summary>
		/// Loads a remote Pdf Document from a web Uri.
		/// </summary>
		/// <returns></returns>
        private async Task LoadFromRemoteAsync()
        {
			try
			{
				// uses the single static HttpClient instance
				var stream = await _httpClient.GetStreamAsync(Source);

				var memStream = new MemoryStream();

				await stream.CopyToAsync(memStream);

				memStream.Position = 0;

				PdfDocument doc = null;

				try
				{
					doc = await PdfDocument.LoadFromStreamAsync(memStream.AsRandomAccessStream(), PdfPassword);
				}
				catch (Exception ex)
				{
					switch (ex.HResult)
					{
						case WrongPassword:
							PdfErrorMessage = "Document is password-protected and password is incorrect.";
							break;

						case GenericFail:
							PdfErrorMessage = "Document is not a valid PDF.";
							break;

						default:
							// File I/O errors are reported as exceptions.
							PdfErrorMessage = ex.Message;
							break;
					}
				}


				log.Trace("In LoadFromLocalAsync(), about to call Load()");

				await Load(doc);

				HostStatusMsgHandler?.Invoke("Web PDF File Loaded");
			}
			catch (Exception ex)
			{
				//PdfErrorMessage = string.Format("Exception in LoadFromRemoteAsync():{0}", ex.Message);
				HostErrorMsgHandler?.Invoke(PdfErrorMessage);
			}
        }

		/// <summary>
		/// Loads a Pdf file from a local filesystem Uri, including embedded resource files from //Assets
		/// </summary>
		/// <returns></returns>
        private async Task LoadFromLocalAsync()
        {
			try
			{
				StorageFile f = await StorageFile.GetFileFromApplicationUriAsync(Source);

				PdfDocument doc = null;

				try
				{
					doc = await PdfDocument.LoadFromFileAsync(f, PdfPassword);
				}
				catch (Exception ex)
				{
					switch (ex.HResult)
					{
						case WrongPassword:
							PdfErrorMessage = "Document is password-protected and password is incorrect.";
							break;

						case GenericFail:
							PdfErrorMessage = "Document is not a valid PDF.";
							break;

						default:
							// File I/O errors are reported as exceptions.
							PdfErrorMessage = ex.Message;
							break;
					}
				}

				log.Trace("In LoadFromLocalAsync(), about to call Load()");

				await Load(doc);

				HostStatusMsgHandler?.Invoke("Storage PDF File Loaded");
			}
			catch (Exception ex)
			{
				//PdfErrorMessage = string.Format("Exception in LoadFromLocalAsync():{0}", ex.Message);
				HostErrorMsgHandler?.Invoke(PdfErrorMessage);
			}
        }

		/// <summary>
		/// Loads the first page of an opened PdfDocument into PdfPages ObservableCollection, sets page numbers
		/// and counters.
		/// </summary>
		/// <param name="pdfDoc"></param>
		/// <returns></returns>
        private async Task Load(PdfDocument pdfDoc)
        {
			try
			{
				ClearPdfImages();

				log.Trace(string.Format("In Load(), about to load first page of PdfDocument of {0} pages.", pdfDoc.PageCount));

				if (null != pdfDoc)
				{
					_currentPdfDocument = pdfDoc;

					PageCount = (int)pdfDoc.PageCount;

					CurrentPageNumber = 1;

					// load the first page into the FlipView
					await LoadPdfPage(0);
				}
				else
				{
					PdfErrorMessage = string.Format("Load(pdfDocument) passed null pdfDocument argument.");
				}

				log.Trace("Leaving Load(), about to set_autoLoad to true");

				// we've finished loading a document, so set _autoLoad to true to 
				// enable another load, because the UWP framework calls OnSourceChanged twice
				// through the DependencyProperty system and it will double load the document
				// if you don't do this.
				_autoLoad = true;
			}
			catch (Exception ex)
			{
				PdfErrorMessage = string.Format("Exception in Load(pdfDocument):{0}", ex.Message);
				HostErrorMsgHandler?.Invoke(PdfErrorMessage);
			}
        }

		/// <summary>
		/// Loads a single Pdf page into the PdfPages collection.
		/// </summary>
		/// <param name="pageIndex"></param>
		/// <returns></returns>
		private async Task LoadPdfPage(uint pageIndex)
		{
			try
			{
				BitmapImage pageImage = await GetPageImage(pageIndex);

				// add the pageImage to the observablecollection and increment counter
				AddPdfImage(pageImage);

				HostStatusMsgHandler?.Invoke(string.Format("Loaded PDF Page {0}", pageIndex + 1));
			}
			catch (Exception ex)
			{
				PdfErrorMessage = string.Format("Exception in LoadPdfPage({0}):{1}", pageIndex, ex.Message);
				HostErrorMsgHandler?.Invoke(PdfErrorMessage);
			}
		}

		/// <summary>
		/// Loads and returns a single Pdf page from the current document without placing it
		/// into the PdfPages collection.  Used to generate pages for print.  Called from printer code.
		/// </summary>
		/// <param name="pageIndex"></param>
		/// <returns>BitmapImage of PdfPage</returns>
		public async Task<BitmapImage> GetPdfImageForPrint(uint pageIndex)
		{
			try
			{
				BitmapImage pageImage = await GetPageImage(pageIndex);

				return pageImage;
			}
			catch (Exception ex)
			{
				PdfErrorMessage = string.Format("Exception in GetPdfImageForPrint({0}):{1}", pageIndex, ex.Message);
				HostErrorMsgHandler?.Invoke(PdfErrorMessage);
			}

			// null is the error return
			return null;
		}

		/// <summary>
		/// Decodes a PdfPage from the currentPdfDocument and returns it as a BitmapImage with the 
		/// currently set PdfBackgroundColor.
		/// </summary>
		/// <param name="pageIndex"></param>
		/// <returns>BitmapImage</returns>
		private async Task<BitmapImage> GetPageImage(uint pageIndex)
		{
			BitmapImage src = null;

			try
			{
				if (null != _currentPdfDocument && pageIndex < _currentPdfDocument.PageCount)
				{
					using (PdfPage page = _currentPdfDocument.GetPage(pageIndex))
					{
						var stream = new InMemoryRandomAccessStream();

						var options = new PdfPageRenderOptions();

						// the Beige default is set in the constructor and that will
						// be used to render the Pdf document unless the caller has
						// set that property to something else.
						options.BackgroundColor = PdfBackgroundColor;

						// View actual size.
						await page.RenderToStreamAsync(stream, options);

						src = new BitmapImage();

						await src.SetSourceAsync(stream);
					}
				}
				else
				{
					PdfErrorMessage = string.Format("GetPageImage({0}) pageIndex out of range.", pageIndex);
					HostErrorMsgHandler?.Invoke(PdfErrorMessage);
				}
			}
			catch (Exception ex)
			{
				PdfErrorMessage = string.Format("Exception in GetPageImage({0}):{1}", pageIndex, ex.Message);
			}

			// null is the error return
			return src;			
		}

		/// <summary>
		/// This is the click handler for the 'Print' button.
		/// </summary>
		public async Task OnPrintButtonClick()
		{
			HostStatusMsgHandler?.Invoke("Printing document.");
			await _printHelper.ShowPrintUIAsync();
		}

		/// <summary>
		/// Gets the PageCount of the current Pdf Document on the UI thread.  Called from print code.
		/// </summary>
		/// <returns>Number of pages in the current PdfDocument</returns>
		public async Task<int> GetPrintPageCount()
		{
			int numberOfPages = 0;

			await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
			() =>
			{
				numberOfPages = PageCount;
			});

			return numberOfPages;
		}
	}
}
