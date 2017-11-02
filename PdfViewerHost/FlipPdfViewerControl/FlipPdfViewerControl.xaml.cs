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
        public event PropertyChangedEventHandler PropertyChanged;

        public Uri Source
        {
            get { return (Uri)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(Uri), typeof(FlipPdfViewerControl),
                new PropertyMetadata(null, OnSourceChanged));

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

        internal ZoomMode ZoomMode
        {
            get { return IsZoomEnabled ? ZoomMode.Enabled : ZoomMode.Disabled; }
        }

        public void ClearPdfImages()
        {
            // clear it out
            PdfPages.Clear();
            PageCount = 0;
            Source = null;
        }

        public void AddPdfImage(BitmapImage img)
        {
            if(null != img)
            {
                PdfPages.Add(img);
                PageCount++;
            }      
        }

        public void IncrementPage()
        {
            if(flipView.SelectedIndex >= 0 && flipView.SelectedIndex < PageCount - 1)
            {
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
            FlipViewItem test = (FlipViewItem)flipView.ContainerFromIndex(flipView.SelectedIndex);
            var viewer = (ImageViewer)test.ContentTemplateRoot;

            viewer.ZoomReset();
        }

        public bool AutoLoad { get; set; }

        internal ObservableCollection<BitmapImage> PdfPages
        {
            get;
            set;
        } = new ObservableCollection<BitmapImage>();

        // Metrolog logger
        private ILogger log;

        public FlipPdfViewerControl()
        {
            this.Background = new SolidColorBrush(Colors.DarkGray);

            Window.Current.CoreWindow.CharacterReceived += CoreWindow_CharacterReceived;

            // Loaded is where we hook up component control event handlers
            Loaded += FlipPdfViewerControl_Loaded;

            log = LogManagerFactory.DefaultLogManager.GetLogger<FlipPdfViewerControl>();

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

        private void OnIsZoomEnabledChanged()
        {
            OnPropertyChanged(nameof(ZoomMode));
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

        public async Task LoadAsync()
        {
            log.Trace("In LoadAsync()");

            PdfPages.Clear();

            if(Source.IsFile || !Source.IsWebUri())
            {
                log.Trace("Source.IsFile and not IsWebUri, about to LoadFromLocalAsync.");

                await LoadFromLocalAsync();
            }
            else if(Source.IsWebUri())
            {
                log.Trace("Source.IsWebUri and not IsFile, about to LoadFromRemoteAsync.");

                await LoadFromRemoteAsync();
            }
            else
            {
                throw new ArgumentException($"Source '{Source.ToString()}' could not be recognized!");
            }            
        }

        private async Task LoadFromRemoteAsync()
        {
            HttpClient client = new HttpClient();

            var stream = await client.GetStreamAsync(Source);

            var memStream = new MemoryStream();

            await stream.CopyToAsync(memStream);

            memStream.Position = 0;

            PdfDocument doc = await PdfDocument.LoadFromStreamAsync(memStream.AsRandomAccessStream());

            log.Trace("In LoadFromLocalAsync(), about to call Load()");

            Load(doc);
        }

        private async Task LoadFromLocalAsync()
        {
            StorageFile f = await StorageFile.GetFileFromApplicationUriAsync(Source);

            PdfDocument doc = await PdfDocument.LoadFromFileAsync(f);

            log.Trace("In LoadFromLocalAsync(), about to call Load()");

            Load(doc);
        }

        private async void Load(PdfDocument pdfDoc)
        {
            PdfPages.Clear();
            
            // clear out the Source property
            Source = null;

            PageCount = (int)pdfDoc.PageCount;

            CurrentPageNumber = 1;

            log.Trace(string.Format("In Load(), about to load PdfDocument of {0} pages.", pdfDoc.PageCount));

            for (uint i = 0; i < pdfDoc.PageCount; i++)
            {
                BitmapImage image = new BitmapImage();
                image.CreateOptions = BitmapCreateOptions.None;

                var page = pdfDoc.GetPage(i);

                using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
                {
                    await page.RenderToStreamAsync(stream);
                    await image.SetSourceAsync(stream);
                }

                log.Trace(string.Format("About to add image number {0} to PdfPages collection.", i));

                PdfPages.Add(image);
            }

            log.Trace("Leaving Load(), about to set AutoLoad to true");

            // we've finished loading a document, so set AutoLoad to true to 
            // enable another load, because the UWP framework calls OnSourceChanged twice
            // through the DependencyProperty system and it will double load the document
            // if you don't do this.
            AutoLoad = true;

            // this prevents the control from loading again unless called again
            // with a new source uri.
            Source = null;

        }

        public void OnPropertyChanged([CallerMemberName]string property = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }
}
