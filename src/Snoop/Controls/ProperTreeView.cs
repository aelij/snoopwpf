// (c) 2015 Eli Arbel
// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Snoop.Utilities;
using Snoop.Views;
using Snoop.VisualTree;

namespace Snoop.Controls
{
    public class ProperTreeView : TreeView
    {
        public bool ApplyReduceDepthFilterIfNeeded(ProperTreeViewItem curNode)
        {
            if (_pendingRoot != null)
            {
                OnRootLoaded();
            }

            if (_maxDepth == 0)
            {
                return false;
            }

            var rootItem = (VisualTreeItem)_rootItem.Target;
            if (rootItem == null)
            {
                return false;
            }

            if (_snoopUi == null)
            {
                _snoopUi = this.GetAncestor<SnoopUI>();
                if (_snoopUi == null)
                {
                    return false;
                }
            }

            var item = (VisualTreeItem)curNode.DataContext;
            var selectedItem = _snoopUi.CurrentSelection;
            if (selectedItem != null && item.Depth < selectedItem.Depth)
            {
                item = selectedItem;
            }

            if ((item.Depth - rootItem.Depth) <= _maxDepth)
            {
                return false;
            }

            for (var i = 0; i < _maxDepth; ++i)
            {
                item = item.Parent;
            }

            _snoopUi.ApplyReduceDepthFilter(item);
            return true;
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            if (_pendingRoot != null)
            {
                _pendingRoot.Loaded -= OnRootLoaded;
                _pendingRoot = null;
            }
            _pendingRoot = new ProperTreeViewItem(new WeakReference(this));
            _pendingRoot.Loaded += OnRootLoaded;
            _maxDepth = 0;
            _rootItem.Target = null;
            return _pendingRoot;
        }

        private void OnRootLoaded(object sender, RoutedEventArgs e)
        {
            Debug.Assert(ReferenceEquals(_pendingRoot, sender), "_pendingRoot == sender");
            OnRootLoaded();
        }

        private void OnRootLoaded()
        {
            // The following assumptions are made:
            // 1. The visual structure of each TreeViewItem is the same regardless of its location.
            // 2. The control template of a TreeViewItem contains ItemsPresenter.
            var root = _pendingRoot;

            _pendingRoot = null;
            root.Loaded -= OnRootLoaded;

            ItemsPresenter itemsPresenter = null;
            root.EnumerateTree(null, delegate(Visual visual, object misc)
            {
                itemsPresenter = visual as ItemsPresenter;
                if (itemsPresenter != null && ReferenceEquals(itemsPresenter.TemplatedParent, root))
                {
                    return HitTestResultBehavior.Stop;
                }
                itemsPresenter = null;
                return HitTestResultBehavior.Continue;
            }, null);

            if (itemsPresenter != null)
            {
                var levelLayoutDepth = 2;
                DependencyObject tmp = itemsPresenter;
                while (!ReferenceEquals(tmp, root))
                {
                    ++levelLayoutDepth;
                    tmp = VisualTreeHelper.GetParent(tmp);
                }

                var rootLayoutDepth = 0;
                while (tmp != null)
                {
                    ++rootLayoutDepth;
                    tmp = VisualTreeHelper.GetParent(tmp);
                }

                _maxDepth = (240 - rootLayoutDepth) / levelLayoutDepth;
                _rootItem = new WeakReference((VisualTreeItem)root.DataContext);
            }
        }

        private int _maxDepth;
        private SnoopUI _snoopUi;
        private ProperTreeViewItem _pendingRoot;
        private WeakReference _rootItem = new WeakReference(null);
    }

    public class ProperTreeViewItem : TreeViewItem
    {
        static ProperTreeViewItem()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ProperTreeViewItem), new FrameworkPropertyMetadata(typeof(ProperTreeViewItem)));
        }

        public ProperTreeViewItem(WeakReference treeView)
        {
            _treeView = treeView;
        }

        public double Indent
        {
            get { return (double)GetValue(IndentProperty); }
            set { SetValue(IndentProperty, value); }
        }

        public static readonly DependencyProperty IndentProperty =
            DependencyProperty.Register
            (
                "Indent",
                typeof(double),
                typeof(ProperTreeViewItem)
            );

        protected override void OnSelected(RoutedEventArgs e)
        {
            // scroll the selection into view
            BringIntoView();

            base.OnSelected(e);
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new ProperTreeViewItem(_treeView) { Indent = Indent + 12 };
        }

        protected override Size MeasureOverride(Size constraint)
        {
            // Check whether the tree is too deep.
            try
            {
                var treeView = (ProperTreeView)_treeView.Target;
                if (treeView == null || !treeView.ApplyReduceDepthFilterIfNeeded(this))
                {
                    return base.MeasureOverride(constraint);
                }
            }
            catch
            {
                // ignored
            }
            return new Size(0, 0);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            // Check whether the tree is too deep.
            try
            {
                var treeView = (ProperTreeView)_treeView.Target;
                if (treeView == null || !treeView.ApplyReduceDepthFilterIfNeeded(this))
                {
                    return base.ArrangeOverride(arrangeBounds);
                }
            }
            catch
            {
                // ignored
            }
            return new Size(0, 0);
        }

        private readonly WeakReference _treeView;
    }

    public class IndentToMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new Thickness((double)value, 0, 0, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
