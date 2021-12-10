using Avalonia.Controls;
using Avalonia.Input;
using ReactiveUI;
using System.Reactive.Linq;

namespace WalletWasabi.Fluent.Behaviors
{
	public static class AutoBringIntoViewExtension
	{
		private static bool Initialised { get; set; }

		public static void Initialise()
		{
			if (!Initialised)
			{
				Initialised = true;

				KeyboardDevice.Instance.WhenAnyValue(x => x.FocusedElement)
				.OfType<IControl>()
				.Subscribe(
					x =>
					{
						static void BringParentToView(IInputElement ie)
						{
							if (ie.VisualParent is IInputElement parent && parent.IsKeyboardFocusWithin)
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
