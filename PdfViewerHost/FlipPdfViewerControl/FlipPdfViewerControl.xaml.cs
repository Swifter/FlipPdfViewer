using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;


using MetroLog;


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



		public string PdfPassword
		{
			get { return (string)GetValue(PdfPasswordProperty); }
			set { SetValue(PdfPasswordProperty, value); }
		}

		// Using a DependencyProperty as the backing store for PdfPassword.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty PdfPasswordProperty =
			DependencyProperty.Register("PdfPassword", typeof(string), typeof(FlipPdfViewerControl),
				new PropertyMetadata(string.Empty));



		public Color PdfBackgroundColor
		{
			get { return (Color)GetValue(PdfBackgroundColorProperty); }
			set { SetValue(PdfBackgroundColorProperty, value); }
		}

		// Using a DependencyProperty as the backing store for PdfBackgroundColor.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty PdfBackgroundColorProperty =
			DependencyProperty.Register("PdfBackgroundColor", typeof(Color), typeof(FlipPdfViewerControl),
				new PropertyMetadata(null));

		public bool IsZoomEnabled
        {
            get { return (bool)GetValue(IsZoomEnabledProperty); }
            set { SetValue(IsZoomEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsZoomEnabledProperty =
            DependencyProperty.Register("IsZoomEnabled", typeof(bool), typeof(FlipPdfViewerControl),
                new PropertyMetadata(true, OnIsZoomEnabledChanged));

        public int PageCount
        {
            get { return (int)GetValue(PageCountProperty); }
            set { SetValue(PageCountProperty, value);  }
        }

        public static readonly DependencyProperty PageCountProperty =
            DependencyProperty.Register("PageCount", typeof(int), typeof(FlipPdfViewerControl),
            new PropertyMetadata(null));

        public int CurrentPageNumber
        {
            get { return (int)GetValue(CurrentPageNumberProperty); }
            set { SetValue(CurrentPageNumberProperty, value);  }
        }

        public static readonly DependencyProperty CurrentPageNumberProperty =
            DependencyProperty.Register("CurrentPageNumberCount", typeof(int), typeof(FlipPdfViewerControl),
            new PropertyMetadata(null));


		public string PdfStatusMessage
		{
			get { return (string)GetValue(PdfStatusMessageProperty); }
			set { SetValue(PdfStatusMessageProperty, value); }
		}

		// Using a DependencyProperty as the backing store for PdfStatusMessage.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty PdfStatusMessageProperty =
			DependencyProperty.Register("PdfStatusMessage", typeof(string), typeof(FlipPdfViewerControl),
				new PropertyMetadata(null));

		public string PdfErrorMessage
		{
			get { return (string)GetValue(PdfErrorMessageProperty); }
			set { SetValue(PdfErrorMessageProperty, value); }
		}

		// Using a DependencyProperty as the backing store for PdfStatusMessage.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty PdfErrorMessageProperty =
			DependencyProperty.Register("PdfErrorMessage", typeof(string), typeof(FlipPdfViewerControl),
				new PropertyMetadata(null));


		private PdfDocument _currentPdfDocument = null;

		private bool _printingIsSupported = true;

		// a single static HttpClient instance is used for downloading Pdf documents from URIs
		private static HttpClient _httpClient = new HttpClient();

		private int _lastPdfImageLoaded = 0;

		const int WrongPassword = unchecked((int)0x8007052b); // HRESULT_FROM_WIN32(ERROR_WRONG_PASSWORD)
		const int GenericFail = unchecked((int)0x80004005);   // E_FAIL

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

		internal ZoomMode ZoomMode
        {
            get { return IsZoomEnabled ? ZoomMode.Enabled : ZoomMode.Disabled; }
        }

        public void ClearPdfImages()
        {
            // clear it out
            PdfPages.Clear();
            PageCount = 0;
			_lastPdfImageLoaded = 0;
            Source = null;
			StorageFileSource = null;
        }

        public void AddPdfImage(BitmapImage img)
        {
            if(null != img)
            {
                PdfPages.Add(img);
                _lastPdfImageLoaded++;
            }      
        }

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

        public void DecrementPage()
        {
            if(flipView.SelectedIndex > 0)
            {
                flipView.SelectedIndex -= 1;
            }
        }

        public void ZoomIn()
        {
            if(IsZoomEnabled)
            {
                FlipViewItem test = (FlipViewItem)flipView.ContainerFromIndex(flipView.SelectedIndex);
                var viewer = (ImageViewer)test.ContentTemplateRoot;

                viewer.ZoomIn();
            }
        }

        public void ZoomOut()
        {
            if(IsZoomEnabled)
            {
                FlipViewItem test = (FlipViewItem)flipView.ContainerFromIndex(flipView.SelectedIndex);
                var viewer = (ImageViewer)test.ContentTemplateRoot;

                viewer.ZoomOut();
            }
        }

        public void ZoomReset()
        {	
			if(_lastPdfImageLoaded > 0)
			{
				FlipViewItem test = (FlipViewItem)flipView.ContainerFromIndex(flipView.SelectedIndex);
				var viewer = (ImageViewer)test.ContentTemplateRoot;

				viewer.ZoomReset();
			}
		}

        public bool AutoLoad { get; set; }

        internal ObservableCollection<BitmapImage> PdfPages
        {
            get;
            set;
        } = new ObservableCollection<BitmapImage>();

        public FlipPdfViewerControl()
        {
            this.Background = new SolidColorBrush(Colors.DarkGray);

            Window.Current.CoreWindow.CharacterReceived += CoreWindow_CharacterReceived;

            // Loaded is where we hook up component control event handlers
            Loaded += FlipPdfViewerControl_Loaded;

			// set the default Pdf background color
			PdfBackgroundColor = Windows.UI.Colors.Beige;

			this.InitializeComponent();
        }

        private void FlipPdfViewerControl_Loaded(object sender, RoutedEventArgs e)
        {
            flipView.SelectionChanged += FlipView_SelectionChanged;
        }

        // increment/decrement the page counter
        private void FlipView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int i = ((FlipView)sender).SelectedIndex;
            CurrentPageNumber = i + 1;
        }

        // handle keyboard zoom interaction with the flipView
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

        private static void OnIsZoomEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((FlipPdfViewerControl)d).OnIsZoomEnabledChanged();
        }

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((FlipPdfViewerControl)d).OnSourceChanged();
        }

		private static void OnStorageFileSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			((FlipPdfViewerControl)d).OnStorageFileSourceChanged();
		}

        private void OnIsZoomEnabledChanged()
        {
            NotifyChanged(nameof(ZoomMode));
        }

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
				if (AutoLoad && Source != null)
				{
					// OnSourceChanged is getting called twice each time through 
					// the Dependency Property system, so let's
					// gate LoadAsync() from being called again until Load() is finished.
					// We'll set AutoLoad to true again at the end of the Load() method.
					AutoLoad = false;

					log.Trace("AutoLoad is true, about to LoadAsync()");
					await LoadAsync();
				}
			}
			catch (Exception ex)
			{
				PdfErrorMessage = string.Format("Exception in OnSourceChanged:{0}", ex.Message);
			}
        }

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
				if (AutoLoad && StorageFileSource != null)
				{

					// OnStorageFileSourceChanged is getting called twice each time through 
					// the Dependency Property system, so let's
					// gate Load() from being called again until Load() is finished.
					// We'll set AutoLoad to true again at the end of the Load() method.
					AutoLoad = false;

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
					

					await Load(pdfDocument);
				}
			}
			catch (Exception ex)
			{
				PdfErrorMessage = string.Format("Exception in OnStorageFileSourceChanged:{0}{1}Storage File:{2} ", ex.Message, Environment.NewLine, StorageFileSource.Name);
			}
		}

        private async Task LoadAsync()
        {
            log.Trace("In LoadAsync()");

			try
			{
				if (Source.IsFile || !Source.IsWebUri())
				{
					log.Trace("Source.IsFile and not IsWebUri, about to LoadFromLocalAsync.");

					await LoadFromLocalAsync();
				}
				else if (Source.IsWebUri())
				{
					log.Trace("Source.IsWebUri and not IsFile, about to LoadFromRemoteAsync.");

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
			}
        }

        private async Task LoadFromRemoteAsync()
        {
			try
			{
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
			}
			catch (Exception ex)
			{
				PdfErrorMessage = string.Format("Exception in LoadFromRemoteAsync():{0}", ex.Message);
			}
        }

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
			}
			catch (Exception ex)
			{
				PdfErrorMessage = string.Format("Exception in LoadFromLocalAsync():{0}", ex.Message);
			}
        }

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

				log.Trace("Leaving Load(), about to set AutoLoad to true");

				// we've finished loading a document, so set AutoLoad to true to 
				// enable another load, because the UWP framework calls OnSourceChanged twice
				// through the DependencyProperty system and it will double load the document
				// if you don't do this.
				AutoLoad = true;
			}
			catch (Exception ex)
			{
				PdfErrorMessage = string.Format("Exception in Load(pdfDocument):{0}", ex.Message);
			}
        }

		private async Task LoadPdfPage(uint pageIndex)
		{
			try
			{
				BitmapImage pageImage = await GetPageImage(pageIndex);

				// add the pageImage to the observablecollection and increment counter
				AddPdfImage(pageImage);
			}
			catch (Exception ex)
			{
				PdfErrorMessage = string.Format("Exception in LoadPdfPage({0}):{1}", pageIndex, ex.Message);
			}
		}

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
			}

			// null is the error return
			return null;
		}

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
				}
			}
			catch (Exception ex)
			{
				PdfErrorMessage = string.Format("Exception in GetPageImage({0}):{1}", pageIndex, ex.Message);
			}

			// null is the error return
			return src;			
		}
    }
}
