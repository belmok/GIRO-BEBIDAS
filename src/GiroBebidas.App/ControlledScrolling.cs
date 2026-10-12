using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace GiroBebidas.App;

public static class ControlledScrolling
{
    private const double PixelsPerNotch = 22;
    private sealed class WheelState
    {
        public double RequestedOffset;
        public bool Pending, Scheduled;
    }
    private static readonly DependencyProperty WheelStateProperty = DependencyProperty.RegisterAttached(
        "WheelState", typeof(WheelState), typeof(ControlledScrolling));
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(ControlledScrolling), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not FrameworkElement area || area is not (ScrollViewer or DataGrid)) return;
        area.PreviewMouseWheel -= Area_PreviewMouseWheel;
        area.PreviewMouseDown -= ResetPending;
        area.PreviewKeyDown -= ResetPending;
        if (!(bool)e.NewValue) return;
        area.PreviewMouseWheel += Area_PreviewMouseWheel;
        area.PreviewMouseDown += ResetPending;
        area.PreviewKeyDown += ResetPending;
    }

    private static void Area_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not FrameworkElement area || e.Handled || e.Delta == 0) return;
        // A popup has a separate visual tree even when logically owned by a row.
        if (e.OriginalSource is Visual source && !ReferenceEquals(source, area) && !area.IsAncestorOf(source)) return;
        if (e.OriginalSource is Visual wheelSource)
        {
            for (DependencyObject? parent = wheelSource; parent is not null && !ReferenceEquals(parent, area); parent = VisualTreeHelper.GetParent(parent))
                if (parent is FrameworkElement nested && GetIsEnabled(nested)) return;
        }
        if (GetScrollViewer(area) is null) return;
        if (SystemParameters.WheelScrollLines != 0) ScrollArea(area, e.Delta);
        e.Handled = true;
    }

    private static void ResetPending(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement area && GetScrollViewer(area) is { } viewer && viewer.GetValue(WheelStateProperty) is WheelState state)
            state.Pending = false;
    }

    internal static ScrollViewer? GetScrollViewer(FrameworkElement area)
    {
        if (area is ScrollViewer viewer) return viewer;
        if (area is not DataGrid grid) return null;
        grid.ApplyTemplate();
        return grid.Template.FindName("DG_ScrollViewer", grid) as ScrollViewer;
    }

    internal static void ScrollArea(FrameworkElement area, int delta)
    {
        if (delta == 0 || GetScrollViewer(area) is not { } viewer || viewer.ScrollableHeight <= 0) return;
        var state = viewer.GetValue(WheelStateProperty) as WheelState;
        if (state is null)
        {
            state = new WheelState(); viewer.SetValue(WheelStateProperty, state);
            viewer.ScrollChanged += Viewer_ScrollChanged;
        }
        double start = state.Pending ? state.RequestedOffset : viewer.VerticalOffset;
        state.RequestedOffset = Math.Clamp(start - delta / 120.0 * PixelsPerNotch, 0, viewer.ScrollableHeight);
        state.Pending = true;
        viewer.ScrollToVerticalOffset(state.RequestedOffset);
        // ScrollToVerticalOffset is queued. Accumulate a burst until layout applies
        // its last request, then read the actual offset before the next gesture.
        if (state.Scheduled) return;
        state.Scheduled = true;
        viewer.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            state.RequestedOffset = viewer.VerticalOffset;
            state.Pending = state.Scheduled = false;
        }));
    }

    private static void Viewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer viewer || !ReferenceEquals(e.OriginalSource, viewer) || viewer.GetValue(WheelStateProperty) is not WheelState state) return;
        // An external scroll/drag can replace a queued wheel request before the
        // idle callback. Follow its actual offset instead of restoring an old one.
        if (!state.Pending || Math.Abs(viewer.VerticalOffset - state.RequestedOffset) > 0.5)
        {
            state.RequestedOffset = viewer.VerticalOffset;
            state.Pending = false;
        }
    }
}
