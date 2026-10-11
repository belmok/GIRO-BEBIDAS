using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace GiroBebidas.App;

public partial class MainWindow
{
    private void MenuNavigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox navigation || e.AddedItems.Count == 0) return;
        object? selected = e.AddedItems[0];
        if (selected is null) return;
        navigation.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (!Equals(navigation.SelectedItem, selected)) return;
            if (navigation.ItemContainerGenerator.ContainerFromItem(selected) is FrameworkElement item)
                EnsureMenuItemVisible(MenuScroll, item);
        }));
    }

    internal static void EnsureMenuItemVisible(ScrollViewer viewer, FrameworkElement item)
    {
        if (viewer.ViewportHeight <= 0 || item.ActualHeight <= 0) return;
        // Use the outer viewport directly: the ListBox has its own disabled viewer.
        var bounds = item.TransformToAncestor(viewer).TransformBounds(new Rect(item.RenderSize));
        double adjustment = bounds.Top < 0 ? bounds.Top - 4
            : bounds.Bottom > viewer.ViewportHeight ? bounds.Bottom - viewer.ViewportHeight + 4 : 0;
        if (adjustment != 0)
            viewer.ScrollToVerticalOffset(Math.Clamp(viewer.VerticalOffset + adjustment, 0, viewer.ScrollableHeight));
    }

    private void MenuScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer viewer && e.Delta != 0)
        {
            // Handle on the outer viewer before the nested ListBox consumes wheel input.
            ScrollMenu(viewer, e.Delta, SystemParameters.WheelScrollLines);
            e.Handled = true;
        }
    }

    internal static void ScrollMenu(ScrollViewer viewer, int delta, int wheelLines)
    {
        if (delta == 0 || wheelLines == 0 || viewer.ScrollableHeight <= 0) return;
        // Fractional offsets preserve high-resolution touchpad deltas. A full wheel
        // notch follows the user's configured line count (or page-scroll setting).
        double distance = wheelLines < 0 ? viewer.ViewportHeight : wheelLines * 16.0;
        double offset = viewer.VerticalOffset - delta / 120.0 * distance;
        viewer.ScrollToVerticalOffset(Math.Clamp(offset, 0, viewer.ScrollableHeight));
    }
}
