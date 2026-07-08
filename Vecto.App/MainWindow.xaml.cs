using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Vecto.App;

public partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();
    bool _syncingScroll;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.FitRequested += () =>
            Dispatcher.BeginInvoke(FitZoom, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void TryLoad(string path)
    {
        try
        {
            ViewModel.LoadFile(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Vecto", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void OnOpen(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*" };
        if (dlg.ShowDialog(this) == true) TryLoad(dlg.FileName);
    }

    void OnPaste(object sender, RoutedEventArgs e)
    {
        if (!Clipboard.ContainsImage())
        {
            ViewModel.Status = "Clipboard has no image.";
            return;
        }
        var img = Clipboard.GetImage();
        if (img != null) ViewModel.LoadBitmap(BitmapFrame.Create(img));
    }

    void OnExport(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasResult) return;
        var dlg = new SaveFileDialog { Filter = "SVG|*.svg", FileName = ViewModel.SuggestedName };
        if (dlg.ShowDialog(this) != true) return;
        File.WriteAllText(dlg.FileName, ViewModel.BuildSvg());
        ViewModel.Status = "Exported " + dlg.FileName;
    }

    void OnCopySvg(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasResult) return;
        Clipboard.SetText(ViewModel.BuildSvg());
        ViewModel.Status = "SVG copied to clipboard.";
    }

    void OnZoomIn(object sender, RoutedEventArgs e) => ViewModel.Zoom *= 1.25;
    void OnZoomOut(object sender, RoutedEventArgs e) => ViewModel.Zoom /= 1.25;
    void OnZoomActual(object sender, RoutedEventArgs e) => ViewModel.Zoom = 1;
    void OnZoomFit(object sender, RoutedEventArgs e) => FitZoom();

    void FitZoom()
    {
        var bmp = ViewModel.SourceBitmap;
        if (bmp == null) return;
        double vw = LeftScroll.ViewportWidth, vh = LeftScroll.ViewportHeight;
        if (vw < 1 || vh < 1)
        {
            vw = LeftScroll.ActualWidth;
            vh = LeftScroll.ActualHeight;
        }
        if (vw < 1 || vh < 1) return;
        double fit = Math.Min((vw - 24) / bmp.PixelWidth, (vh - 24) / bmp.PixelHeight);
        ViewModel.Zoom = Math.Min(8, fit);
    }

    void OnPreviewWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        e.Handled = true;
        ViewModel.Zoom *= e.Delta > 0 ? 1.25 : 0.8;
    }

    void OnLeftScrollChanged(object sender, ScrollChangedEventArgs e) => SyncScroll(LeftScroll, RightScroll, e);
    void OnRightScrollChanged(object sender, ScrollChangedEventArgs e) => SyncScroll(RightScroll, LeftScroll, e);

    void SyncScroll(ScrollViewer from, ScrollViewer to, ScrollChangedEventArgs e)
    {
        if (_syncingScroll) return;
        if (e.HorizontalChange == 0 && e.VerticalChange == 0) return;
        _syncingScroll = true;
        to.ScrollToHorizontalOffset(from.HorizontalOffset);
        to.ScrollToVerticalOffset(from.VerticalOffset);
        _syncingScroll = false;
    }

    void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0) TryLoad(files[0]);
    }

    void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        switch (e.Key)
        {
            case Key.O:
                OnOpen(sender, e);
                e.Handled = true;
                break;
            case Key.V:
                OnPaste(sender, e);
                e.Handled = true;
                break;
            case Key.S:
                OnExport(sender, e);
                e.Handled = true;
                break;
        }
    }
}
