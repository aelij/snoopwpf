﻿// (c) 2015 Eli Arbel
// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Navigation;
using Snoop.Infrastructure;

namespace Snoop.Views
{
	/// <summary>
	/// Interaction logic for ErrorDialog.xaml
	/// </summary>
	public partial class ErrorDialog
	{
		public ErrorDialog()
		{
			InitializeComponent();

			Loaded += ErrorDialog_Loaded;
			Closed += ErrorDialog_Closed;
		}

		public Exception Exception { get; set; }

		private void ErrorDialog_Loaded(object sender, RoutedEventArgs e)
		{
			TextBlockException.Text = GetExceptionMessage();

			SnoopPartsRegistry.AddSnoopVisualTreeRoot(this);
		}
		private void ErrorDialog_Closed(object sender, EventArgs e)
		{
			SnoopPartsRegistry.RemoveSnoopVisualTreeRoot(this);
		}

		private void _buttonCopyToClipboard_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				Clipboard.SetText(GetExceptionMessage());
			}
			catch (Exception ex)
			{
				string message =
				    $"There was an error copying to the clipboard:\nMessage = {ex.Message}\n\nPlease copy the exception from the above textbox manually!";
				MessageBox.Show(message, "Error copying to clipboard");
			}
		}
		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			try
			{
				Process.Start(e.Uri.AbsoluteUri);
			}
			catch (Exception)
			{
				string message =
				    $"There was an error starting the browser. Please visit \"{e.Uri.AbsoluteUri}\" to create the issue.";
				MessageBox.Show(message, "Error starting browser");
			}
		}

		private void CloseDoNotMarkHandled_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			if (CheckBoxRememberIsChecked())
			{
				SnoopModes.IgnoreExceptions = true;
			}
			Close();
		}
		private void CloseAndMarkHandled_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
			if (CheckBoxRememberIsChecked())
			{
				SnoopModes.SwallowExceptions = true;
			}
			Close();
		}

		private string GetExceptionMessage()
		{
			var builder = new StringBuilder();
			GetExceptionString(Exception, builder);
			return builder.ToString();
		}
		private static void GetExceptionString(Exception exception, StringBuilder builder, bool isInner = false)
		{
			if (exception == null)
				return;

			if (isInner)
				builder.AppendLine("\n\nInnerException:\n");

			builder.AppendLine($"Message: {exception.Message}");
			builder.AppendLine($"Stacktrace:\n{exception.StackTrace}");

			GetExceptionString(exception.InnerException, builder, true);
		}

		private bool CheckBoxRememberIsChecked()
		{
			return CheckBoxRemember.IsChecked.HasValue && CheckBoxRemember.IsChecked.Value;
		}
	}
}
