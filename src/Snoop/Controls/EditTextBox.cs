// (c) 2015 Eli Arbel
// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Snoop.Controls
{
	/// <summary>
	/// Allows committing a value when pressing Return, and requerying the value after setting it.
	/// </summary>
	public class EditTextBox : TextBox
	{
		protected override void OnInitialized(EventArgs e)
		{
			base.OnInitialized(e);

			// Take focus and select all the text by default.
			// Great for in the property editor, so the text is quick to edit,
			// but probably not useful in general.
			Focus();
			SelectAll();
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				var expression = BindingOperations.GetBindingExpressionBase(this, TextProperty);
			    expression?.UpdateSource();
			    e.Handled = true;
			}
			base.OnKeyDown(e);
		}
	}
}
