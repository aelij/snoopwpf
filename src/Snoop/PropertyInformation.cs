// (c) 2015 Eli Arbel
// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Snoop.Annotations;
using Snoop.Infrastructure;
using Snoop.Utilities;

namespace Snoop
{
    public class PropertyInformation : DependencyObject, IComparable, INotifyPropertyChanged
    {
        private readonly object _component;
        private readonly bool _isCopyable;

        private PropertyFilter _filter;
        private bool _isRunning;
        private bool _ignoreUpdate;
        private DispatcherTimer _changeTimer;
        private int _index;
        private bool _breakOnChange;
        private bool _changedRecently;

        /// <summary>
        /// Normal constructor used when constructing PropertyInformation objects for properties.
        /// </summary>
        /// <param name="target">target object being shown in the property grid</param>
        /// <param name="property">the property around which we are contructing this PropertyInformation object</param>
        /// <param name="propertyName">the property name for the property that we use in the binding in the case of a non-dependency property</param>
        /// <param name="propertyDisplayName">the display name for the property that goes in the name column</param>
        public PropertyInformation(object target, PropertyDescriptor property, string propertyName, string propertyDisplayName)
        {
            Target = target;
            Property = property;
            DisplayName = propertyDisplayName;
            CollectionIndex = -1;

            if (property != null)
            {
                // create a data binding between the actual property value on the target object
                // and the Value dependency property on this PropertyInformation object
                var dp = DependencyProperty;
                var binding = dp != null ? new Binding { Path = new PropertyPath("(0)", dp) } : new Binding(propertyName);

                binding.Source = target;
                binding.Mode = property.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay;

                try
                {
                    BindingOperations.SetBinding(this, ValueProperty, binding);
                }
                catch (Exception)
                {
                    // cplotts note:
                    // warning: i saw a problem get swallowed by this empty catch (Exception) block.
                    // in other words, this empty catch block could be hiding some potential future errors.
                }
            }

            Update();

            _isRunning = true;
        }

        /// <summary>
        /// Constructor used when constructing PropertyInformation objects for an item in a collection.
        /// In this case, we set the PropertyDescriptor for this object (in the property Property) to be null.
        /// This kind of makes since because an item in a collection really isn't a property on a class.
        /// That is, in this case, we're really hijacking the PropertyInformation class
        /// in order to expose the items in the Snoop property grid.
        /// </summary>
        /// <param name="target">the item in the collection</param>
        /// <param name="component">the collection</param>
        /// <param name="collectionIndex">Index in the collection.</param>
        /// <param name="displayName">the display name that goes in the name column, i.e. this[x]</param>
        /// <param name="isCopyable">if set to <c>true</c> [is copyable].</param>
        public PropertyInformation(object target, object component, int collectionIndex, string displayName, bool isCopyable = false)
            : this(target, null, displayName, displayName)
        {
            CollectionIndex = collectionIndex;
            _component = component;
            _isCopyable = isCopyable;
        }

        public void Teardown()
        {
            _isRunning = false;
            BindingOperations.ClearAllBindings(this);
        }

        public object Target { get; }

        public object Value
        {
            get { return GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(object), typeof(PropertyInformation),
                new PropertyMetadata(HandleValueChanged));

        private static void HandleValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PropertyInformation)d).OnValueChanged();
        }

        protected virtual void OnValueChanged()
        {
            Update();

            if (_isRunning)
            {
                if (_breakOnChange)
                {
                    if (!Debugger.IsAttached)
                        Debugger.Launch();
                    Debugger.Break();
                }

                HasChangedRecently = true;
                if (_changeTimer == null)
                {
                    _changeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                    _changeTimer.Tick += HandleChangeExpiry;
                    _changeTimer.Start();
                }
                else
                {
                    _changeTimer.Stop();
                    _changeTimer.Start();
                }
            }
        }

        private void HandleChangeExpiry(object sender, EventArgs e)
        {
            _changeTimer.Stop();
            _changeTimer = null;

            HasChangedRecently = false;
        }

        public string StringValue
        {
            get
            {
                var value = Value;
                if (value != null)
                    return value.ToString();
                return string.Empty;
            }
            set
            {
                if (Property == null)
                {
                    // if this is a PropertyInformation object constructed for an item in a collection
                    // then just return, since setting the value via a string doesn't make sense.
                    return;
                }

                var targetType = Property.PropertyType;
                if (targetType.IsAssignableFrom(typeof(string)))
                {
                    Property.SetValue(Target, value);
                }
                else
                {
                    var converter = TypeDescriptor.GetConverter(targetType);
                    try
                    {
                        // ReSharper disable once AssignNullToNotNullAttribute
                        Property.SetValue(Target, converter.ConvertFrom(value));
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
            }
        }

        public string DescriptiveValue
        {
            get
            {
                var value = Value;
                if (value == null)
                {
                    return string.Empty;
                }

                var stringValue = value.ToString();

                if (stringValue.Equals(value.GetType().ToString()))
                {
                    // Add brackets around types to distinguish them from values.
                    // Replace long type names with short type names for some specific types, for easier readability.
                    // FUTURE: This could be extended to other types.
                    if (Property != null &&
                        (Property.PropertyType == typeof(Brush) || Property.PropertyType == typeof(Style)))
                    {
                        stringValue = $"[{value.GetType().Name}]";
                    }
                    else
                    {
                        stringValue = $"[{stringValue}]";
                    }
                }

                // Display #00FFFFFF as Transparent for easier readability
                if (Property != null &&
                    Property.PropertyType == typeof(Brush) &&
                    stringValue.Equals("#00FFFFFF"))
                {
                    stringValue = "Transparent";
                }

                var dependencyObject = Target as DependencyObject;
                if (dependencyObject != null && DependencyProperty != null)
                {
                    // Cache the resource key for this item if not cached already. This could be done for more types, but would need to optimize perf.
                    string resourceKey = null;
                    if (Property != null &&
                        (Property.PropertyType == typeof(Style) || Property.PropertyType == typeof(Brush)))
                    {
                        var resourceItem = dependencyObject.GetValue(DependencyProperty);
                        resourceKey = ResourceKeyCache.GetKey(resourceItem);
                        if (string.IsNullOrEmpty(resourceKey))
                        {
                            resourceKey = ResourceDictionaryKeyHelpers.GetKeyOfResourceItem(dependencyObject, DependencyProperty);
                            ResourceKeyCache.Cache(resourceItem, resourceKey);
                        }
                        Debug.Assert(resourceKey != null);
                    }

                    // Display both the value and the resource key, if there's a key for this property.
                    if (!string.IsNullOrEmpty(resourceKey))
                    {
                        return $"{resourceKey} {stringValue}";
                    }

                    // if the value comes from a Binding, show the path in [] brackets
                    if (IsExpression && Binding is Binding)
                    {
                        stringValue = $"{stringValue} {BuildBindingDescriptiveString((Binding) Binding, true)}";
                    }

                    // if the value comes from a MultiBinding, show the binding paths separated by , in [] brackets
                    else if (IsExpression && Binding is MultiBinding)
                    {
                        stringValue = stringValue + BuildMultiBindingDescriptiveString(((MultiBinding)Binding).Bindings.OfType<Binding>().ToArray());
                    }

                    // if the value comes from a PriorityBinding, show the binding paths separated by , in [] brackets
                    else if (IsExpression && Binding is PriorityBinding)
                    {
                        stringValue = stringValue + BuildMultiBindingDescriptiveString(((PriorityBinding)Binding).Bindings.OfType<Binding>().ToArray());
                    }
                }

                return stringValue;
            }
        }

        /// <summary>
        /// Build up a string of Paths for a MultiBinding separated by ;
        /// </summary>
        private static string BuildMultiBindingDescriptiveString(IEnumerable<Binding> bindings)
        {
            var ret = " {Paths=";
            foreach (var binding in bindings)
            {
                ret += BuildBindingDescriptiveString(binding, false);
                ret += ";";
            }
            ret = ret.Substring(0, ret.Length - 1);	// remove trailing ,
            ret += "}";

            return ret;
        }

        /// <summary>
        /// Build up a string describing the Binding.  Path and ElementName (if present)
        /// </summary>
        private static string BuildBindingDescriptiveString(Binding binding, bool isSinglePath)
        {
            var sb = new StringBuilder();
            var bindingPath = binding.Path.Path;
            var elementName = binding.ElementName;

            if (isSinglePath)
            {
                sb.Append("{Path=");
            }

            sb.Append(bindingPath);
            if (!string.IsNullOrEmpty(elementName))
            {
                sb.AppendFormat(", ElementName={0}", elementName);
            }

            if (isSinglePath)
            {
                sb.Append("}");
            }

            return sb.ToString();
        }

        public Type ComponentType
        {
            get
            {
                if (Property == null)
                {
                    // if this is a PropertyInformation object constructed for an item in a collection
                    // then this.property will be null, but this.component will contain the collection.
                    // use this object to return the type of the collection for the ComponentType.
                    return _component.GetType();
                }
                return Property.ComponentType;
            }
        }

        public Type PropertyType
        {
            get
            {
                if (Property == null)
                {
                    // if this is a PropertyInformation object constructed for an item in a collection
                    // just return typeof(object) here, since an item in a collection ... really isn't a property.
                    return typeof(object);
                }
                return Property.PropertyType;
            }
        }

        public Type ValueType
        {
            get
            {
                if (Value != null)
                {
                    return Value.GetType();
                }
                return typeof(object);
            }
        }

        public string BindingError { get; private set; } = string.Empty;

        public PropertyDescriptor Property { get; }

        public string DisplayName { get; }

        public bool IsInvalidBinding { get; private set; }

        public bool IsLocallySet { get; private set; }

        public bool IsValueChangedByUser { get; set; }

        public bool CanEdit
        {
            get
            {
                if (Property == null)
                {
                    // if this is a PropertyInformation object constructed for an item in a collection
                    //return false;
                    return _isCopyable;
                }
                return !Property.IsReadOnly;
            }
        }

        public bool IsDatabound { get; private set; }

        public bool IsExpression => ValueSource.IsExpression;

        public bool IsAnimated => ValueSource.IsAnimated;

        public int Index
        {
            get { return _index; }
            set
            {
                if (_index != value)
                {
                    _index = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsOdd));
                }
            }
        }

        public bool IsOdd => _index % 2 == 1;

        public BindingBase Binding
        {
            get
            {
                var dp = DependencyProperty;
                var d = Target as DependencyObject;
                if (dp != null && d != null)
                    return BindingOperations.GetBindingBase(d, dp);
                return null;
            }
        }

        public BindingExpressionBase BindingExpression
        {
            get
            {
                var dp = DependencyProperty;
                var d = Target as DependencyObject;
                if (dp != null && d != null)
                    return BindingOperations.GetBindingExpressionBase(d, dp);
                return null;
            }
        }

        public PropertyFilter Filter
        {
            get { return _filter; }
            set
            {
                _filter = value;

                OnPropertyChanged(nameof(IsVisible));
            }
        }

        public bool BreakOnChange
        {
            get { return _breakOnChange; }
            set
            {
                _breakOnChange = value;
                OnPropertyChanged();
            }
        }

        public bool HasChangedRecently
        {
            get { return _changedRecently; }
            set
            {
                _changedRecently = value;
                OnPropertyChanged();
            }
        }

        public ValueSource ValueSource { get; private set; }

        public bool IsVisible => _filter.Show(this);

        public void Clear()
        {
            var dp = DependencyProperty;
            var d = Target as DependencyObject;
            if (dp != null && d != null)
                ((DependencyObject)Target).ClearValue(dp);
        }

        /// <summary>
        /// Returns the DependencyProperty identifier for the property that this PropertyInformation wraps.
        /// If the wrapped property is not a DependencyProperty, null is returned.
        /// </summary>
        private DependencyProperty DependencyProperty
        {
            get
            {
                if (Property != null)
                {
                    // in order to be a DependencyProperty, the object must first be a regular property,
                    // and not an item in a collection.

                    var dpd = DependencyPropertyDescriptor.FromProperty(Property);
                    return dpd?.DependencyProperty;
                }

                return null;
            }
        }

        private void Update()
        {
            if (_ignoreUpdate)
                return;

            IsLocallySet = false;
            IsInvalidBinding = false;
            IsDatabound = false;

            var dp = DependencyProperty;
            var d = Target as DependencyObject;

            if (SnoopModes.MultipleDispatcherMode && d != null && d.Dispatcher != Dispatcher)
                return;

            if (dp != null && d != null)
            {
                UpdateValue(d, dp);
            }

            OnPropertyChanged(nameof(IsLocallySet));
            OnPropertyChanged(nameof(IsInvalidBinding));
            OnPropertyChanged(nameof(StringValue));
            OnPropertyChanged(nameof(DescriptiveValue));
            OnPropertyChanged(nameof(IsDatabound));
            OnPropertyChanged(nameof(IsExpression));
            OnPropertyChanged(nameof(IsAnimated));
            OnPropertyChanged(nameof(ValueSource));
        }

        private void UpdateValue(DependencyObject d, DependencyProperty dp)
        {
            if (d.ReadLocalValue(dp) != DependencyProperty.UnsetValue)
                IsLocallySet = true;

            ValueSource = DependencyPropertyHelper.GetValueSource(d, dp);

            var expression = BindingOperations.GetBindingExpressionBase(d, dp);
            if (expression == null) return;

            IsDatabound = true;

            if (expression.HasError || expression.Status != BindingStatus.Active)
            {
                SetBindingError(d, dp, expression);
            }
            else
            {
                BindingError = string.Empty;
            }
        }

        private void SetBindingError(DependencyObject d, DependencyProperty dp, BindingExpressionBase expression)
        {
            IsInvalidBinding = true;

            var builder = new StringBuilder();
            var writer = new StringWriter(builder);
            var tracer = new TextWriterTraceListener(writer);
            PresentationTraceSources.DataBindingSource.Listeners.Add(tracer);

            // reset binding to get the error message.
            _ignoreUpdate = true;
            d.ClearValue(dp);
            BindingOperations.SetBinding(d, dp, expression.ParentBindingBase);
            _ignoreUpdate = false;

            // this needs to happen on idle so that we can actually run the binding, which may occur asynchronously.
            Dispatcher.InvokeAsync(() =>
            {
                BindingError = builder.ToString();
                OnPropertyChanged(nameof(BindingError));
                PresentationTraceSources.DataBindingSource.Listeners.Remove(tracer);
                writer.Close();
            }, DispatcherPriority.ApplicationIdle);
        }

        public static List<PropertyInformation> GetProperties(object obj)
        {
            return GetProperties(obj, descriptor => PertinentPropertyFilter.IsRelevantProperty(obj, descriptor));
        }

        public static List<PropertyInformation> GetProperties(object obj, Predicate<PropertyDescriptor> filter)
        {
            // get the properties
            var propertyDescriptors = GetAllProperties(obj, new Attribute[] { new PropertyFilterAttribute(PropertyFilterOptions.All) });

            // filter the properties
            var props = (
                from property in propertyDescriptors
                where filter(property)
                select new PropertyInformation(obj, property, property.Name, property.DisplayName)).ToList();

            //delve path. also, issue 4919
            var extendedProps = GetExtendedProperties(obj);
            props.AddRange(extendedProps);

            // if the object is a collection, add the items in the collection as properties
            var collection = obj as ICollection;
            var index = 0;
            if (collection != null)
            {
                foreach (var item in collection)
                {
                    var info = new PropertyInformation(item, collection, index, "this[" + index + "]");
                    index++;
                    info.Value = item;
                    props.Add(info);
                }
            }

            props.Sort();

            return props;
        }

        /// <summary>
        /// 4919 + Delve
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        private static IList<PropertyInformation> GetExtendedProperties(object obj)
        {
            var props = new List<PropertyInformation>();

            if (obj != null && ResourceKeyCache.Contains(obj))
            {
                var key = ResourceKeyCache.GetKey(obj);
                var prop = new PropertyInformation(key, new object(), -1, "x:Key", true) { Value = key };
                props.Add(prop);
            }

            return props;
        }

        private static List<PropertyDescriptor> GetAllProperties(object obj, Attribute[] attributes)
        {
            var propertiesToReturn = new List<PropertyDescriptor>();

            // keep looping until you don't have an AmbiguousMatchException exception
            // and you normally won't have an exception, so the loop will typically execute only once.
            var noException = false;
            while (!noException && obj != null)
            {
                try
                {
                    // try to get the properties using the GetProperties method that takes an instance
                    var properties = TypeDescriptor.GetProperties(obj, attributes);
                    noException = true;

                    MergeProperties(properties, propertiesToReturn);
                }
                catch (AmbiguousMatchException)
                {
                    // if we get an AmbiguousMatchException, the user has probably declared a property that hides a property in an ancestor
                    // see issue 6258 (http://snoopwpf.codeplex.com/workitem/6258)
                    //
                    // public class MyButton : Button
                    // {
                    //     public new double? Width
                    //     {
                    //         get { return base.Width; }
                    //         set { base.Width = value.Value; }
                    //     }
                    // }

                    var t = obj.GetType();
                    var properties = TypeDescriptor.GetProperties(t, attributes);

                    MergeProperties(properties, propertiesToReturn);

                    var nextBaseTypeWithDefaultConstructor = GetNextTypeWithDefaultConstructor(t);
                    obj = Activator.CreateInstance(nextBaseTypeWithDefaultConstructor);
                }
            }

            return propertiesToReturn;
        }

        public static bool HasDefaultConstructor(Type type)
        {
            return type.GetConstructors().Any(constructor => constructor.GetParameters().Length == 0);
        }

        public static Type GetNextTypeWithDefaultConstructor(Type type)
        {
            var t = type.BaseType;

            while (!HasDefaultConstructor(t))
            {
                // ReSharper disable once PossibleNullReferenceException
                t = t.BaseType;
            }

            return t;
        }

        private static void MergeProperties(IEnumerable newProperties, ICollection<PropertyDescriptor> allProperties)
        {
            foreach (var newProperty in newProperties)
            {
                var newPropertyDescriptor = newProperty as PropertyDescriptor;
                if (newPropertyDescriptor == null)
                    continue;

                if (!allProperties.Contains(newPropertyDescriptor))
                    allProperties.Add(newPropertyDescriptor);
            }
        }

        public int CollectionIndex { get; }

        #region IComparable Members

        public int CompareTo(object obj)
        {
            var thisIndex = CollectionIndex;
            var objIndex = ((PropertyInformation)obj).CollectionIndex;
            if (thisIndex >= 0 && objIndex >= 0)
            {
                return thisIndex.CompareTo(objIndex);
            }
            return string.CompareOrdinal(DisplayName, ((PropertyInformation)obj).DisplayName);
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
