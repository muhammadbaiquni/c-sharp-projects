using System.Windows;
using System.Windows.Controls;
using TreeView = System.Windows.Controls.TreeView;

namespace MpcplBuilder.Wpf.Behaviors;

// Bridges TreeView's read-only SelectedItem to a two-way view-model binding.
public static class TreeViewSelection
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(TreeViewSelection), new PropertyMetadata(false, OnIsEnabledChanged));
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.RegisterAttached(
        "SelectedItem", typeof(object), typeof(TreeViewSelection),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static object? GetSelectedItem(DependencyObject element) => element.GetValue(SelectedItemProperty);
    public static void SetSelectedItem(DependencyObject element, object? value) => element.SetValue(SelectedItemProperty, value);

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not TreeView tree) return;
        if ((bool)args.NewValue)
        {
            tree.SelectedItemChanged += OnTreeSelectionChanged;
            tree.Loaded += OnTreeLoaded;
        }
        else
        {
            tree.SelectedItemChanged -= OnTreeSelectionChanged;
            tree.Loaded -= OnTreeLoaded;
        }
    }

    private static void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> args) =>
        ((TreeView)sender).SetCurrentValue(SelectedItemProperty, args.NewValue);

    private static void OnSelectedItemChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is TreeView tree && !ReferenceEquals(tree.SelectedItem, args.NewValue))
        {
            UpdateSelection(tree, args.NewValue);
            QueueSelection(tree);
        }
    }

    private static void OnTreeLoaded(object sender, RoutedEventArgs args) => QueueSelection((TreeView)sender);

    private static void QueueSelection(TreeView tree)
    {
        // Refresh can restore selection before the new item containers exist.
        tree.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
            new Action(() => UpdateSelection(tree, GetSelectedItem(tree))));
    }

    private static void UpdateSelection(ItemsControl parent, object? selected)
    {
        foreach (var data in parent.Items)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(data) is not TreeViewItem item) continue;
            if (ReferenceEquals(data, selected))
            {
                item.SetCurrentValue(TreeViewItem.IsSelectedProperty, true);
                item.BringIntoView();
                return;
            }
            if (selected is null) item.SetCurrentValue(TreeViewItem.IsSelectedProperty, false);
            UpdateSelection(item, selected);
        }
    }
}
