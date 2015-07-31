// (c) 2015 Eli Arbel
// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Snoop.Annotations;
using Snoop.Controls;
using Snoop.Properties;
using Snoop.Utilities;

namespace Snoop.Views
{
    public partial class PropertyInspector : INotifyPropertyChanged
    {

        public PropertyInspector()
        {
            PropertyFilter.SelectedFilterSet = AllFilterSets[0];

            InitializeComponent();

            _inspector = PropertyGrid;
            _inspector.Filter = PropertyFilter;

            CommandBindings.Add(new CommandBinding(PropertyGrid.SnipXamlCommand, HandleSnipXaml, CanSnipXaml));
            CommandBindings.Add(new CommandBinding(PropertyGrid.PopTargetCommand, HandlePopTarget, CanPopTarget));
            CommandBindings.Add(new CommandBinding(PropertyGrid.DelveCommand, HandleDelve, CanDelve));
            CommandBindings.Add(new CommandBinding(PropertyGrid.DelveBindingCommand, HandleDelveBinding, CanDelveBinding));
            CommandBindings.Add(new CommandBinding(PropertyGrid.DelveBindingExpressionCommand, HandleDelveBindingExpression, CanDelveBindingExpression));

            // watch for mouse "back" button
            MouseDown += MouseDownHandler;
            KeyDown += PropertyInspector_KeyDown;
        }

        public bool NameValueOnly
        {
            get { return PropertyGrid.NameValueOnly; }
            set { PropertyGrid.NameValueOnly = value; }
        }

        private static void HandleSnipXaml(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                var xaml = XamlWriter.Save(((PropertyInformation)e.Parameter).Value);
                Clipboard.SetData(DataFormats.Text, xaml);
                MessageBox.Show("This brush has been copied to the clipboard. You can paste it into your project.", "Brush copied", MessageBoxButton.OK);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private static void CanSnipXaml(object sender, CanExecuteRoutedEventArgs e)
        {
            if (e.Parameter != null && ((PropertyInformation)e.Parameter).Value is Brush)
                e.CanExecute = true;
            e.Handled = true;
        }

        public object RootTarget
        {
            get { return GetValue(RootTargetProperty); }
            set { SetValue(RootTargetProperty, value); }
        }
        public static readonly DependencyProperty RootTargetProperty =
            DependencyProperty.Register
            (
                "RootTarget",
                typeof(object),
                typeof(PropertyInspector),
                new PropertyMetadata(HandleRootTargetChanged)
            );
        private static void HandleRootTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var inspector = (PropertyInspector)d;

            inspector._inspectStack.Clear();
            inspector.Target = e.NewValue;

            inspector._delvePathList.Clear();
            inspector.OnPropertyChanged(nameof(DelvePath));
        }

        public object Target
        {
            get { return GetValue(TargetProperty); }
            set { SetValue(TargetProperty, value); }
        }

        public static readonly DependencyProperty TargetProperty =
            DependencyProperty.Register
            (
                "Target",
                typeof(object),
                typeof(PropertyInspector),
                new PropertyMetadata(HandleTargetChanged)
            );

        private static void HandleTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var inspector = (PropertyInspector)d;
            inspector.OnPropertyChanged(nameof(Type));

            if (e.NewValue != null)
                inspector._inspectStack.Add(e.NewValue);
        }


        private string GetDelvePath(Type rootTargetType)
        {
            var delvePath = new StringBuilder(rootTargetType.Name);

            foreach (var propInfo in _delvePathList)
            {
                int collectionIndex;
                delvePath.Append((collectionIndex = propInfo.CollectionIndex) >= 0
                    ? $"[{collectionIndex}]"
                    : $".{propInfo.DisplayName}");
            }

            return delvePath.ToString();
        }

        private string GetCurrentTypeName(Type rootTargetType)
        {
            var type = string.Empty;
            if (_delvePathList.Count > 0)
            {
                type = _delvePathList[_delvePathList.Count - 1].Value != null
                    ? _delvePathList[_delvePathList.Count - 1].Value.GetType().ToString()
                    : _delvePathList[_delvePathList.Count - 1].PropertyType.ToString();
            }
            else if (_delvePathList.Count == 0)
            {
                type = rootTargetType.FullName;
            }

            return type;
        }

        /// <summary>
        /// Delve Path
        /// </summary>
        public string DelvePath
        {
            get
            {
                if (RootTarget == null)
                    return "object is NULL";

                var rootTargetType = RootTarget.GetType();
                var delvePath = GetDelvePath(rootTargetType);
                var type = GetCurrentTypeName(rootTargetType);

                return $"{delvePath}\n({type})";
            }
        }

        public Type Type => Target?.GetType();

        public void PushTarget(object target)
        {
            Target = target;
        }

        public void SetTarget(object target)
        {
            _inspectStack.Clear();
            Target = target;
        }

        private void HandlePopTarget(object sender, ExecutedRoutedEventArgs e)
        {
            PopTarget();
        }

        private void PopTarget()
        {
            if (_inspectStack.Count > 1)
            {
                Target = _inspectStack[_inspectStack.Count - 2];
                _inspectStack.RemoveAt(_inspectStack.Count - 2);
                _inspectStack.RemoveAt(_inspectStack.Count - 2);

                if (_delvePathList.Count > 0)
                {
                    _delvePathList.RemoveAt(_delvePathList.Count - 1);
                    OnPropertyChanged(nameof(DelvePath));
                }
            }
        }

        private void CanPopTarget(object sender, CanExecuteRoutedEventArgs e)
        {
            if (_inspectStack.Count > 1)
            {
                e.Handled = true;
                e.CanExecute = true;
            }
        }

        private void HandleDelve(object sender, ExecutedRoutedEventArgs e)
        {
            var realTarget = ((PropertyInformation)e.Parameter).Value;

            if (realTarget != Target)
            {
                // top 'if' statement is the delve path.
                // we do this because without doing this, the delve path gets out of sync with the actual delves.
                // the reason for this is because PushTarget sets the new target,
                // and if it's equal to the current (original) target, we won't raise the property-changed event,
                // and therefore, we don't add to our delveStack (the real one).

                _delvePathList.Add(((PropertyInformation)e.Parameter));
                OnPropertyChanged(nameof(DelvePath));
            }

            PushTarget(realTarget);
        }

        private void HandleDelveBinding(object sender, ExecutedRoutedEventArgs e)
        {
            PushTarget(((PropertyInformation)e.Parameter).Binding);
        }

        private void HandleDelveBindingExpression(object sender, ExecutedRoutedEventArgs e)
        {
            PushTarget(((PropertyInformation)e.Parameter).BindingExpression);
        }

        private static void CanDelve(object sender, CanExecuteRoutedEventArgs e)
        {
            if (e.Parameter != null && ((PropertyInformation)e.Parameter).Value != null)
                e.CanExecute = true;
            e.Handled = true;
        }

        private static void CanDelveBinding(object sender, CanExecuteRoutedEventArgs e)
        {
            if (e.Parameter != null && ((PropertyInformation)e.Parameter).Binding != null)
                e.CanExecute = true;
            e.Handled = true;
        }

        private static void CanDelveBindingExpression(object sender, CanExecuteRoutedEventArgs e)
        {
            if (e.Parameter != null && ((PropertyInformation)e.Parameter).BindingExpression != null)
                e.CanExecute = true;
            e.Handled = true;
        }

        public PropertyFilter PropertyFilter { get; } = new PropertyFilter(string.Empty, true);

        public string StringFilter
        {
            get { return PropertyFilter.FilterString; }
            set
            {
                PropertyFilter.FilterString = value;

                _inspector.Filter = PropertyFilter;

                OnPropertyChanged();
            }
        }

        public bool ShowDefaults
        {
            get { return PropertyFilter.ShowDefaults; }
            set
            {
                PropertyFilter.ShowDefaults = value;

                _inspector.Filter = PropertyFilter;

                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Looking for "browse back" mouse button.
        /// Pop properties context when clicked.
        /// </summary>
        private void MouseDownHandler(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.XButton1)
            {
                PopTarget();
            }
        }

        private void PropertyInspector_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.Left)
            {
                PopTarget();
            }
        }

        /// <summary>
        /// Hold the SelectedFilterSet in the PropertyFilter class, but track it here, so we know
        /// when to "refresh" the filtering with filterCall.Enqueue
        /// </summary>
        public PropertyFilterSet SelectedFilterSet
        {
            get { return PropertyFilter.SelectedFilterSet; }
            set
            {
                PropertyFilter.SelectedFilterSet = value;
                OnPropertyChanged();

                if (value == null)
                    return;

                if (value.IsEditCommand)
                {
                    var dlg = new EditUserFilters { UserFilters = CopyFilterSets(UserFilterSets) };

                    // set owning window to center over if we can find it up the tree
                    var snoopWindow = this.GetAncestor<Window>();
                    if (snoopWindow != null)
                    {
                        dlg.Owner = snoopWindow;
                        dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    }

                    var res = dlg.ShowDialog();
                    if (res.GetValueOrDefault())
                    {
                        // take the adjusted values from the dialog, setter will SAVE them to user properties
                        UserFilterSets = CleansFilterPropertyNames(dlg.ItemsSource);
                        // trigger the UI to re-bind to the collection, so user sees changes they just made
                        OnPropertyChanged(nameof(AllFilterSets));
                    }

                    // now that we're out of the dialog, set current selection back to "(default)"
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, (DispatcherOperationCallback)delegate
                    {
                        // couldnt get it working by setting SelectedFilterSet directly
                        // using the Index to get us back to the first item in the list
                        FilterSetCombo.SelectedIndex = 0;
                        //SelectedFilterSet = AllFilterSets[0];
                        return null;
                    }, null);
                }
                else
                {
                    _inspector.Filter = PropertyFilter;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Get or Set the collection of User filter sets.  These are the filters that are configurable by 
        /// the user, and serialized to/from app Settings.
        /// </summary>
        public PropertyFilterSet[] UserFilterSets
        {
            get
            {
                if (_filterSets == null)
                {
                    var ret = new List<PropertyFilterSet>();

                    try
                    {
                        var userFilters = Settings.Default.PropertyFilterSets;
                        ret.AddRange(userFilters ?? _defaultFilterSets);
                    }
                    catch (Exception ex)
                    {
                        string msg = $"Error reading user filters from settings. Using defaults.\r\n\r\n{ex.Message}";
                        MessageBox.Show(msg, "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
                        ret.Clear();
                        ret.AddRange(_defaultFilterSets);
                    }

                    _filterSets = ret.ToArray();
                }
                return _filterSets;
            }
            set
            {
                _filterSets = value;
                Settings.Default.PropertyFilterSets = _filterSets;
                Settings.Default.Save();
            }
        }

        /// <summary>
        /// Get the collection of "all" filter sets.  This is the UserFilterSets wrapped with 
        /// (Default) at the start and "Edit Filters..." at the end of the collection.
        /// This is the collection bound to in the UI 
        /// </summary>
        public PropertyFilterSet[] AllFilterSets
        {
            get
            {
                var ret = new List<PropertyFilterSet>(UserFilterSets);

                // now add the "(Default)" and "Edit Filters..." filters for the ComboBox
                ret.Insert
                (
                    0,
                    new PropertyFilterSet
                    {
                        DisplayName = "(Default)",
                        IsDefault = true,
                        IsEditCommand = false
                    }
                );
                ret.Add
                (
                    new PropertyFilterSet
                    {
                        DisplayName = "Edit Filters...",
                        IsDefault = false,
                        IsEditCommand = true
                    }
                );
                return ret.ToArray();
            }
        }

        /// <summary>
        /// Make a deep copy of the filter collection.
        /// This is used when heading into the Edit dialog, so the user is editing a copy of the
        /// filters, in case they cancel the dialog - we dont want to alter their live collection.
        /// </summary>
        public PropertyFilterSet[] CopyFilterSets(PropertyFilterSet[] source)
        {
            return source.Select(src => new PropertyFilterSet
            {
                DisplayName = src.DisplayName,
                IsDefault = src.IsDefault,
                IsEditCommand = src.IsEditCommand,
                Properties = (string[])src.Properties.Clone()
            }).ToArray();
        }

        /// <summary>
        /// Cleanse the property names in each filter in the collection.
        /// This includes removing spaces from each one, and making them all lower case
        /// </summary>
        private static PropertyFilterSet[] CleansFilterPropertyNames(ICollection<PropertyFilterSet> collection)
        {
            foreach (var filterItem in collection)
            {
                filterItem.Properties = filterItem.Properties.Select(s => s.ToLower().Trim()).ToArray();
            }
            return collection.ToArray();
        }

        private readonly List<object> _inspectStack = new List<object>();
        private PropertyFilterSet[] _filterSets;
        private readonly List<PropertyInformation> _delvePathList = new List<PropertyInformation>();

        private readonly PropertyGrid _inspector;

        private readonly PropertyFilterSet[] _defaultFilterSets = GetDefaultFilterSets();

        private static PropertyFilterSet[] GetDefaultFilterSets()
        {
            return new[]
            {
                new PropertyFilterSet
                {
                    DisplayName = "Layout",
                    IsDefault = false,
                    IsEditCommand = false,
                    Properties = new[]
                    {
                        "width", "height", "actualwidth", "actualheight", "margin", "padding", "left", "top"
                    }
                },
                new PropertyFilterSet
                {
                    DisplayName = "Grid/Dock",
                    IsDefault = false,
                    IsEditCommand = false,
                    Properties = new[]
                    {
                        "grid", "dock"
                    }
                },
                new PropertyFilterSet
                {
                    DisplayName = "Color",
                    IsDefault = false,
                    IsEditCommand = false,
                    Properties = new[]
                    {
                        "color", "background", "foreground", "borderbrush", "fill", "stroke"
                    }
                },
                new PropertyFilterSet
                {
                    DisplayName = "ItemsControl",
                    IsDefault = false,
                    IsEditCommand = false,
                    Properties = new[]
                    {
                        "items", "selected"
                    }
                }
            };
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
