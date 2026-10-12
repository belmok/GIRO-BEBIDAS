using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GiroBebidas.App;

public static class ComboBoxScrolling
{
    private const double PixelsPerNotch = 22;
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(ComboBoxScrolling), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not ScrollViewer viewer) return;
        viewer.PreviewMouseWheel -= Popup_PreviewMouseWheel;
        if ((bool)e.NewValue) viewer.PreviewMouseWheel += Popup_PreviewMouseWheel;
    }

    private static void Popup_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer viewer || e.Handled || e.Delta == 0) return;
        if (SystemParameters.WheelScrollLines != 0) ScrollPopup(viewer, e.Delta);
        // Consume input at the ends too, so the popup never scrolls its owner page.
        e.Handled = true;
    }

    internal static void ScrollPopup(ScrollViewer viewer, int delta)
    {
        if (delta == 0 || viewer.ScrollableHeight <= 0) return;
        // Physical offsets retain small touchpad deltas instead of rounding to items.
        double offset = viewer.VerticalOffset - delta / 120.0 * PixelsPerNotch;
        viewer.ScrollToVerticalOffset(Math.Clamp(offset, 0, viewer.ScrollableHeight));
    }
}
