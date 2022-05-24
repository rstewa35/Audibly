using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Audibly.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class CompactPage : Page
    {
        public CompactPage()
        {
            this.InitializeComponent();

            AudioBookCover_ViewBox.PointerEntered += AudioBookCover_ViewBox_PointerEntered;
            AudioBookCover_ViewBox.PointerExited += AudioBookCover_ViewBox_PointerExited;
        }

        private void AudioBookCover_ViewBox_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (BlackOverlay_Canvas.Visibility == Visibility.Visible) BlackOverlay_Canvas.Visibility = Visibility.Collapsed;
        }

        private void AudioBookCover_ViewBox_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (BlackOverlay_Canvas.Visibility == Visibility.Collapsed) BlackOverlay_Canvas.Visibility = Visibility.Visible;
        }

        private void SkipBack10Button_Click(object sender, RoutedEventArgs e)
        {
            ;
        }
        
        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            ;
        }
        
        private void SkipForward30Button_Click(object sender, RoutedEventArgs e)
        {
            ;
        }
    }
}
