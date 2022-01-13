using Avalonia.Controls;
using Avalonia.Input;
using ReactiveUI;
using System.Reactive.Linq;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Behaviors;

public static class AutoBringIntoViewExtension
{
	private static bool Initialised { get; set; }

	public static void Initialise()
	{
		if (!Initialised)
		{
			Initialised = true;

			if (KeyboardDevice.Instance is { } device)
			{
				device.WhenAnyValue(x => x.FocusedElement)
					.Where(x => x is IControl)
					.Select(x => x as IControl)
					.Subscribe(
					x =>
					{
						static void BringParentToView(IVisual ie)
						{
							if (ie.VisualParent is IInputElement { IsKeyboardFocusWithin: true } parent)
							{
								BringParentToView(parent);
							}

							if (ie is IControl ic)
							{
								ic.BringIntoView();
							}
						}

						if (x is IInputElement ie)
						{
							BringParentToView(ie);
						}
					});
			}
		}
	}
}

