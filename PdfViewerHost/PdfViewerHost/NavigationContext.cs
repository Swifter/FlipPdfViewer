using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI;

namespace PdfViewerHost
{
	/// <summary>
	/// The parameters for navigating PDF viewer display pages.  Used by MainPage.xaml.cs click-event handlers.
	/// </summary>
	class NavigationContext
	{
		/// <summary>
		/// Flag to indicate if this NavigationContext represents a 
		/// StorageFile or Uri location.
		/// </summary>
		public bool IsFile { get; set; } = false;

		/// <summary>
		/// The StorageFile representing the PDF file to load.
		/// </summary>
		public StorageFile PdfFile { get; set; }

		/// <summary>
		/// The Uri where a remote PDF file may be found and loaded.
		/// </summary>
		public Uri PdfUri { get; set; }

		/// <summary>
		/// The Windows.UI.Color for the background when rendering the PDF file.
		/// </summary>
		public	Color BackgroundColor { get; set; }

	}
}
