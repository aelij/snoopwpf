// (c) 2015 Eli Arbel
// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Snoop.Infrastructure;
using Snoop.Utilities;

namespace Snoop.Views
{
    public partial class EventsView : INotifyPropertyChanged
    {
        public static readonly RoutedCommand ClearCommand = new RoutedCommand();


        public EventsView()
        {
            _interestingEvents = new ObservableCollection<TrackedEvent>();
            InitializeComponent();

            var sorter = new List<EventTracker>();

            foreach (var routedEvent in EventManager.GetRoutedEvents())
            {
                var tracker = new EventTracker(typeof(UIElement), routedEvent);
                tracker.EventHandled += HandleEventHandled;
                sorter.Add(tracker);

                if (_defaultEvents.Contains(routedEvent))
                {
                    tracker.IsEnabled = true;
                }
            }

            sorter.Sort();

            foreach (var tracker in sorter)
            {
                _trackers.Add(tracker);
            }

            CommandBindings.Add(new CommandBinding(ClearCommand, HandleClear));
        }

        public IEnumerable InterestingEvents => _interestingEvents;

        private readonly ObservableCollection<TrackedEvent> _interestingEvents;

        public object AvailableEvents
        {
            get
            {
                var pgd = new PropertyGroupDescription
                {
                    PropertyName = "Category",
                    StringComparison = StringComparison.OrdinalIgnoreCase
                };

                var cvs = new CollectionViewSource();
                cvs.SortDescriptions.Add(new SortDescription("Category", ListSortDirection.Ascending));
                cvs.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
                cvs.GroupDescriptions.Add(pgd);

                cvs.Source = _trackers;

                cvs.View.Refresh();
                return cvs.View;
            }
        }


        private void HandleEventHandled(TrackedEvent trackedEvent)
        {
            var visual = trackedEvent.Originator.Handler as Visual;
            if (visual != null && !visual.IsPartOfSnoopVisualTree())
            {
                Action action =
                    () =>
                    {
                        _interestingEvents.Add(trackedEvent);

                        while (_interestingEvents.Count > 100)
                            _interestingEvents.RemoveAt(0);

                        var tvi = (TreeViewItem)EventTree.ItemContainerGenerator.ContainerFromItem(trackedEvent);
                        tvi?.BringIntoView();
                    };

                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.BeginInvoke(action);
                }
                else
                {
                    action.Invoke();
                }
            }
        }
        private void HandleClear(object sender, ExecutedRoutedEventArgs e)
        {
            _interestingEvents.Clear();
        }

        private void EventTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue == null) return;

            var entry = e.NewValue as EventEntry;
            if (entry != null)
            {
                SnoopUI.InspectCommand.Execute(entry.Handler, this);
            }
            else
            {
                var @event = e.NewValue as TrackedEvent;
                if (@event != null)
                {
                    SnoopUI.InspectCommand.Execute(@event.EventArgs, this);
                }
            }
        }


        private readonly ObservableCollection<EventTracker> _trackers = new ObservableCollection<EventTracker>();


        private static readonly IReadOnlyList<RoutedEvent> _defaultEvents =
            Array.AsReadOnly(new[]
            {
                    Keyboard.KeyDownEvent,
                    Keyboard.KeyUpEvent,
                    TextCompositionManager.TextInputEvent,
                    Mouse.MouseDownEvent,
                    Mouse.PreviewMouseDownEvent,
                    Mouse.MouseUpEvent,
                    CommandManager.ExecutedEvent
            });


        #region INotifyPropertyChanged Members
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            Debug.Assert(GetType().GetProperty(propertyName) != null);
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }

    public class InterestingEvent
    {
        public InterestingEvent(object handledBy, RoutedEventArgs eventArgs)
        {
            HandledBy = handledBy;
            TriggeredOn = null;
            EventArgs = eventArgs;
        }


        public RoutedEventArgs EventArgs { get; }


        public object HandledBy { get; }


        public object TriggeredOn { get; }


        public bool Handled => HandledBy != null;
    }
}
