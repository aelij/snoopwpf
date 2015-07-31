﻿// (c) 2015 Eli Arbel
// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Snoop.Views
{
	public partial class EditUserFilters : INotifyPropertyChanged
	{
		public EditUserFilters()
		{
			InitializeComponent();
			DataContext = this;
		}


		public IEnumerable<PropertyFilterSet> UserFilters
		{
			[DebuggerStepThrough]
			get { return _userFilters; }
			set
			{
				if (!Equals(value, _userFilters))
				{
					_userFilters = value;
					NotifyPropertyChanged("UserFilters");
					ItemsSource = new ObservableCollection<PropertyFilterSet>(UserFilters);
				}
			}
		}
		private IEnumerable<PropertyFilterSet> _userFilters;

		public ObservableCollection<PropertyFilterSet> ItemsSource
		{
			[DebuggerStepThrough]
			get { return _itemsSource; }
			private set
			{
				if (value != _itemsSource)
				{
					_itemsSource = value;
					NotifyPropertyChanged("ItemsSource");
				}
			}
		}
		private ObservableCollection<PropertyFilterSet> _itemsSource;


		private void OkHandler(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
			Close();
		}
		private void CancelHandler(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}

		private void AddHandler(object sender, RoutedEventArgs e)
		{
			var newSet =
				new PropertyFilterSet
				{
					DisplayName = "New Filter",
					IsDefault = false,
					IsEditCommand = false,
					Properties = new[] { "prop1,prop2" }
				};
			ItemsSource.Add(newSet);

			// select this new item
			var index = ItemsSource.IndexOf(newSet);
			if (index >= 0)
			{
				FilterSetList.SelectedIndex = index;
			}
		}
		private void DeleteHandler(object sender, RoutedEventArgs e)
		{
			var selected = FilterSetList.SelectedItem as PropertyFilterSet;
			if (selected != null)
			{
				ItemsSource.Remove(selected);
			}
		}

		private void UpHandler(object sender, RoutedEventArgs e)
		{
			var index = FilterSetList.SelectedIndex;
			if (index <= 0)
				return;

			var item = ItemsSource[index];
			ItemsSource.RemoveAt(index);
			ItemsSource.Insert(index - 1, item);

			// select the moved item
			FilterSetList.SelectedIndex = index - 1;

		}
		private void DownHandler(object sender, RoutedEventArgs e)
		{
			var index = FilterSetList.SelectedIndex;
			if (index >= ItemsSource.Count - 1)
				return;

			var item = ItemsSource[index];
			ItemsSource.RemoveAt(index);
			ItemsSource.Insert(index + 1, item);

			// select the moved item
			FilterSetList.SelectedIndex = index + 1;
		}

		private void SelectionChangedHandler(object sender, SelectionChangedEventArgs e)
		{
			SetButtonStates();
		}


		private void SetButtonStates()
		{
			MoveUp.IsEnabled = false;
			MoveDown.IsEnabled = false;
			DeleteItem.IsEnabled = false;

			var index = FilterSetList.SelectedIndex;
			if (index >= 0)
			{
				MoveDown.IsEnabled = true;
				DeleteItem.IsEnabled = true;
			}

			if (index > 0)
				MoveUp.IsEnabled = true;

			if (index == FilterSetList.Items.Count - 1)
				MoveDown.IsEnabled = false;
		}


		public event PropertyChangedEventHandler PropertyChanged;
		protected void NotifyPropertyChanged(string propertyName)
		{
		    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
