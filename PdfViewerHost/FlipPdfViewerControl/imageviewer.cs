using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace FlipPdfViewerControl
{

    /// <summary>
    /// This object replaces the Scrollviewer/Image combination and presents the PDF
    /// image at its actual aspect ratio, while allowing zoom.  http://www.mobilemotion.eu/?p=2212
    /// </summary>
    public class ImageViewer : Grid
    {
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        "Source", typeof(ImageSource), typeof(ImageViewer),
        new PropertyMetadata(default(BitmapImage), SourceChangedCallback));

        private ScrollViewer scroll;
        private double imgActualWidth;
        private double imgActualHeight;

        private static int pixelWidth;
        private static int pixelHeight;

        private static float zoomChangeFactor = 0.2f;

        private static void SourceChangedCallback(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            var control = sender as ImageViewer;
            var source = args.NewValue as BitmapImage;

            pixelWidth = source.PixelWidth;
            pixelHeight = source.PixelHeight;

            control.scroll = new ScrollViewer();
            control.scroll.ZoomMode = ZoomMode.Enabled;
            control.scroll.Name = "flipScrollViewer";
            control.scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            control.scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            var img = new Image
            {
                Source = source,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            img.Loaded += (o, eventArgs) =>
            {
                var ratioWidth = control.scroll.ViewportWidth / pixelWidth;
                var ratioHeight = control.scroll.ViewportHeight / pixelHeight;

                var zoomFactor = (ratioWidth >= 1 && ratioHeight >= 1)
                    ? 1F
                    : (float)(Math.Min(ratioWidth, ratioHeight));

                control.imgActualWidth = img.ActualWidth;
                control.imgActualHeight = img.ActualHeight;

                control.scroll.ChangeView(null, null, zoomFactor);
            };

            control.scroll.Content = img;
            control.Children.Clear();
            control.Children.Add(control.scroll);
        }

        public ImageSource Source
        {
            get { return (ImageSource)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public void ZoomIn()
        {
            double horzOffset = 0;
            double vertOffset = 0;

            float newZoom = scroll.ZoomFactor + zoomChangeFactor;

            // the pixel size of the content in the scrollviewer now
            float scaledContentW = (float)imgActualWidth * newZoom;
            float scaledContentH = (float)imgActualHeight * newZoom;

            // if our content, scaled by the new zoom, is bigger than the viewport, adjust the scroll offset
            if (scaledContentW < scroll.ViewportWidth)
            {
                horzOffset = 0;
            }
            else
            {
                horzOffset = (scaledContentW - scroll.ViewportWidth) / 2;
            }

            if (scaledContentH < scroll.ViewportHeight)
            {
                vertOffset = 0;
            }
            else
            {
                vertOffset = (scaledContentH - scroll.ViewportHeight) / 2;
            }

            scroll.ChangeView(horzOffset, vertOffset, newZoom);
        }

        public void ZoomOut()
        {
            double horzOffset = 0;
            double vertOffset = 0;

            float newZoom = scroll.ZoomFactor - zoomChangeFactor;

            // the pixel size of the content in the scrollviewer now
            float scaledContentW = (float)imgActualWidth * newZoom;
            float scaledContentH = (float)imgActualHeight * newZoom;

            // if our content, scaled by the new zoom, is bigger than the viewport, adjust the scroll offset
            if (scaledContentW > scroll.ViewportWidth)
            {
                horzOffset = (scaledContentW - scroll.ViewportWidth) / 2;
            }
            else
            {
                horzOffset = 0;
            }

            if (scaledContentH > scroll.ViewportHeight)
            {
                vertOffset = (scaledContentH - scroll.ViewportHeight) / 2;
            }
            else
            {
                vertOffset = 0;
            }

            scroll.ChangeView(horzOffset, vertOffset, newZoom);
        }

        public void ZoomReset()
        {
            ImageReset();
        }

        private void ImageReset()
        {
            var ratioWidth = scroll.ViewportWidth / imgActualWidth;
            var ratioHeight = scroll.ViewportHeight / imgActualHeight;

            var zoomFactor = (ratioWidth >= 1 && ratioHeight >= 1)
                ? 1F
                : (float)(Math.Min(ratioWidth, ratioHeight));

            scroll.ChangeView(null, null, zoomFactor);
        }
    }
}

