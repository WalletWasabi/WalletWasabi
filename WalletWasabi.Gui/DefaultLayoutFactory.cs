using AvaloniaDemo.ViewModels.Views;
using Dock.Avalonia.Controls;
using Dock.Model;
using Dock.Model.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace WalletWasabi.Gui
{
	/// <inheritdoc/>
	public class DefaultLayoutFactory : DockFactory
	{
		public DocumentDock DocumentDock { get; private set; }
		public ToolDock RightDock { get; private set; }

		/// <inheritdoc/>
		public override IDock CreateLayout()
		{
			// Right Pane

			RightDock = new ToolDock
			{
				Id = "RightDock",
				Dock = "Right",
				Width = 300,
				Height = double.NaN,
				Title = "RightDock",
				CurrentView = null,
				Views = new ObservableCollection<IView>()
			};

			// Documents

			DocumentDock = new DocumentDock
			{
				Id = "DocumentsPane",
				Dock = "",
				Width = double.NaN,
				Height = double.NaN,
				Title = "DocumentsPane",
				CurrentView = null,
				Views = new ObservableCollection<IView>()
			};

			// Main

			var mainLayout = new LayoutDock
			{
				Id = "MainLayout",
				Dock = "",
				Width = double.NaN,
				Height = double.NaN,
				Title = "MainLayout",
				CurrentView = null,
				Views = new ObservableCollection<IView>
				{
					RightDock,
					DocumentDock
				}
			};

			var mainView = new MainView
			{
				Id = "Main",
				Width = double.NaN,
				Height = double.NaN,
				Title = "Main",
				CurrentView = mainLayout,
				Views = new ObservableCollection<IView>
				{
				   mainLayout
				}
			};

			// Root

			var root = new RootDock
			{
				Id = "Root",
				Width = double.NaN,
				Height = double.NaN,
				Title = "Root",
				CurrentView = mainView,
				DefaultView = mainView,
				Views = new ObservableCollection<IView>
				{
					mainView,
				}
			};

			return root;
		}

		/// <inheritdoc/>
		public override void InitLayout(IView layout, object context)
		{
			ContextLocator = new Dictionary<string, Func<object>>
			{
				// Defaults
				[nameof(IRootDock)] = () => context,
				[nameof(ILayoutDock)] = () => context,
				[nameof(IDocumentDock)] = () => context,
				[nameof(IToolDock)] = () => context,
				[nameof(ISplitterDock)] = () => context,
				[nameof(IDockWindow)] = () => context,
				// Layouts
				["MainLayout"] = () => context,
				// Views
				["Home"] = () => layout,
				["Main"] = () => context
			};

			HostLocator = new Dictionary<string, Func<IDockHost>>
			{
				[nameof(IDockWindow)] = () => new HostWindow()
			};

			ViewLocator = new Dictionary<string, Func<IView>>
			{
				[nameof(RightDock)] = () => RightDock,
				[nameof(DocumentDock)] = () => DocumentDock
			};

			Update(layout, context, null);

			if (layout is IDock layoutWindowsHost)
			{
				layoutWindowsHost.ShowWindows();
				if (layout is IDock layoutViewsHost)
				{
					layoutViewsHost.CurrentView = layoutViewsHost.DefaultView;
					if (layoutViewsHost.CurrentView is IDock currentViewWindowsHost)
					{
						currentViewWindowsHost.ShowWindows();
					}
				}
			}
		}
	}
}
