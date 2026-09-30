using System.Collections;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MpcplBuilder.Wpf.ViewModels;
using TreeView = System.Windows.Controls.TreeView;

namespace MpcplBuilder.Wpf.Behaviors;

// Bridges TreeView's read-only SelectedItem to a two-way view-model binding.
public static class TreeViewSelection
{
    private static readonly ConditionalWeakTable<TreeView, SelectionState> States = new();
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(TreeViewSelection), new PropertyMetadata(false, OnIsEnabledChanged));
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.RegisterAttached(
        "SelectedItem", typeof(object), typeof(TreeViewSelection),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));
    public static readonly DependencyProperty SelectedItemsProperty = DependencyProperty.RegisterAttached(
        "SelectedItems", typeof(IList), typeof(TreeViewSelection), new PropertyMetadata(null));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static object? GetSelectedItem(DependencyObject element) => element.GetValue(SelectedItemProperty);
    public static void SetSelectedItem(DependencyObject element, object? value) => element.SetValue(SelectedItemProperty, value);
    public static IList? GetSelectedItems(DependencyObject element) => (IList?)element.GetValue(SelectedItemsProperty);
    public static void SetSelectedItems(DependencyObject element, IList? value) => element.SetValue(SelectedItemsProperty, value);

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not TreeView tree) return;
        if ((bool)args.NewValue)
        {
            tree.SelectedItemChanged += OnTreeSelectionChanged;
            tree.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            tree.Loaded += OnTreeLoaded;
        }
        else
        {
            tree.SelectedItemChanged -= OnTreeSelectionChanged;
            tree.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            tree.Loaded -= OnTreeLoaded;
            States.Remove(tree);
        }
    }

    private static void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> args)
    {
        var tree = (TreeView)sender;
        var state = States.GetOrCreateValue(tree);
        if (state.SuppressNativeSelection) return;
        tree.SetCurrentValue(SelectedItemProperty, args.NewValue);
        var selectedItems = GetSelectedItems(tree);
        if (selectedItems is null) return;
        if (args.NewValue is FolderNodeViewModel { CanGenerate: true } folder)
        {
            if (!(selectedItems.Count > 1 && selectedItems.Contains(folder)))
                ApplySelection(selectedItems, [folder], folder, folder, control: false, shift: false);
            state.Anchor = folder;
        }
        else
        {
            selectedItems.Clear();
            state.Anchor = null;
        }
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs args)
    {
        var tree = (TreeView)sender;
        if (args.OriginalSource is not DependencyObject source) return;
        if (FindAncestor<ToggleButton>(source) is not null) return;
        var item = FindAncestor<TreeViewItem>(source);
        if (item is null) return;
        if (item.DataContext is not FolderNodeViewModel { CanGenerate: true }) return;
        var modifiers = Keyboard.Modifiers;
        var control = modifiers.HasFlag(ModifierKeys.Control);
        var shift = modifiers.HasFlag(ModifierKeys.Shift);
        ApplySelectionToTree(tree, item, control, shift);
        args.Handled = true;
    }

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

    internal static void ApplySelectionToTree(TreeView tree, TreeViewItem item, bool control, bool shift)
    {
        if (item.DataContext is not FolderNodeViewModel { CanGenerate: true } clicked) return;
        var selectedItems = GetSelectedItems(tree);
        if (selectedItems is null) return;
        var state = States.GetOrCreateValue(tree);
        var visibleFolders = VisibleFolders(tree).ToArray();
        state.Anchor = ApplySelection(selectedItems, visibleFolders, clicked, state.Anchor, control, shift);

        state.SuppressNativeSelection = true;
        try
        {
            if (selectedItems.Contains(clicked))
            {
                item.SetCurrentValue(TreeViewItem.IsSelectedProperty, true);
                tree.SetCurrentValue(SelectedItemProperty, clicked);
            }
            else
            {
                item.SetCurrentValue(TreeViewItem.IsSelectedProperty, false);
                var fallback = selectedItems.Cast<object>().OfType<FolderNodeViewModel>().LastOrDefault();
                tree.SetCurrentValue(SelectedItemProperty, fallback);
                if (fallback is not null) UpdateSelection(tree, fallback);
            }
        }
        finally
        {
            state.SuppressNativeSelection = false;
        }
        item.Focus();
        item.BringIntoView();
    }

    private static IEnumerable<FolderNodeViewModel> VisibleFolders(ItemsControl parent)
    {
        foreach (var data in parent.Items)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(data) is not TreeViewItem item) continue;
            if (data is FolderNodeViewModel { CanGenerate: true } folder) yield return folder;
            if (!item.IsExpanded) continue;
            foreach (var descendant in VisibleFolders(item)) yield return descendant;
        }
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    internal static FolderNodeViewModel? ApplySelection(IList selectedItems,
        IReadOnlyList<FolderNodeViewModel> visibleFolders, FolderNodeViewModel clicked,
        FolderNodeViewModel? anchor, bool control, bool shift)
    {
        if (shift && anchor is not null)
        {
            var anchorIndex = IndexOf(visibleFolders, anchor);
            var clickedIndex = IndexOf(visibleFolders, clicked);
            if (anchorIndex >= 0 && clickedIndex >= 0)
            {
                if (!control) selectedItems.Clear();
                var start = Math.Min(anchorIndex, clickedIndex);
                var end = Math.Max(anchorIndex, clickedIndex);
                for (var index = start; index <= end; index++)
                    if (!selectedItems.Contains(visibleFolders[index])) selectedItems.Add(visibleFolders[index]);
                return anchor;
            }
        }

        if (control)
        {
            if (selectedItems.Contains(clicked)) selectedItems.Remove(clicked);
            else selectedItems.Add(clicked);
            return clicked;
        }

        selectedItems.Clear();
        selectedItems.Add(clicked);
        return clicked;
    }

    private static int IndexOf(IReadOnlyList<FolderNodeViewModel> folders, FolderNodeViewModel target)
    {
        for (var index = 0; index < folders.Count; index++)
            if (ReferenceEquals(folders[index], target)) return index;
        return -1;
    }

    private sealed class SelectionState
    {
        public FolderNodeViewModel? Anchor { get; set; }
        public bool SuppressNativeSelection { get; set; }
    }
}
