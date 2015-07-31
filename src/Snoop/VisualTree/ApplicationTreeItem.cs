﻿// (c) 2015 Eli Arbel
// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Snoop.VisualTree
{
	public class ApplicationTreeItem : ResourceContainerItem
	{
		public ApplicationTreeItem(Application application, VisualTreeItem parent)
			: base(application, parent)
		{
			_application = application;
		}

		public override Visual MainVisual => _application.MainWindow;

	    protected override ResourceDictionary ResourceDictionary => _application.Resources;

	    protected override void Reload(List<VisualTreeItem> toBeRemoved)
		{
			// having the call to base.Reload here ... puts the application resources at the very top of the tree view
			base.Reload(toBeRemoved);

			// what happens in the case where the application's main window is invisible?
			// in this case, the application will only have one visual item underneath it: the collapsed/hidden window.
			// however, you are still able to ctrl-shift mouse over the visuals in the visible window.
			// when you do this, snoop reloads the visual tree with the visible window as the root (versus the application).

			if (_application.MainWindow != null)
			{
				var foundMainWindow = false;
				foreach (var item in toBeRemoved)
				{
					if (item.Target == _application.MainWindow)
					{
						toBeRemoved.Remove(item);
						item.Reload();
						foundMainWindow = true;
						break;
					}
				}

				if (!foundMainWindow)
					Children.Add(Construct(_application.MainWindow, this));
			}
		}


		private readonly Application _application;
	}
}
