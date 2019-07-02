using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Gui.Tabs.WalletManager;

namespace WalletWasabi.Gui.Behaviors
{
	internal class SuggestionBehavior : Behavior<TextBox>
	{
		private CompositeDisposable Disposables { get; set; }

		private static readonly AvaloniaProperty<IEnumerable<SuggestionViewModel>> SuggestionItemsProperty =
			AvaloniaProperty.Register<SuggestionBehavior, IEnumerable<SuggestionViewModel>>(nameof(SuggestionItems), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

		public IEnumerable<SuggestionViewModel> SuggestionItems
		{
			get => GetValue(SuggestionItemsProperty);
			set => SetValue(SuggestionItemsProperty, value);
		}

		protected override void OnAttached()
		{
			Disposables = new CompositeDisposable();

			base.OnAttached();

			Disposables.Add(AssociatedObject.AddHandler(TextBox.KeyDownEvent, (sender, e) =>
			{
				if (e.Key == Avalonia.Input.Key.Tab)
				{
					HandleAutoUpdate();
					e.Handled = true;
				}
				else if (e.Key == Avalonia.Input.Key.Down)
				{
					if (SuggestionItems != null)
					{
						if (SuggestionItems.All(x => !x.IsHighLighted))
						{
							var item = SuggestionItems.FirstOrDefault();
							if (item != null)
							{
								item.IsHighLighted = true;
							}
						}
						else
						{
							var index = SuggestionItems.Select((v, i) => new { sugg = v, index = i })?.FirstOrDefault(x => x.sugg.IsHighLighted)?.index;
							if (index != null)
							{
								var suggItemsArray = SuggestionItems.ToArray();
								suggItemsArray[index.Value].IsHighLighted = false;
								index++;
								if (suggItemsArray.Length <= index.Value)
								{
									index = 0;
								}
								suggItemsArray[index.Value].IsHighLighted = true;
							}
						}

						e.Handled = true;
					}
				}
				else if (e.Key == Avalonia.Input.Key.Up)
				{
					if (SuggestionItems != null)
					{
						foreach (var item in SuggestionItems)
						{
							item.IsHighLighted = false;
						}
						e.Handled = true;
					}
				}
				else if (e.Key == Avalonia.Input.Key.Enter)
				{
					if (SuggestionItems != null)
					{
						foreach (var item in SuggestionItems)
						{
							if (item.IsHighLighted)
							{
								item.OnSelected();
								break;
							}
						}
						e.Handled = true;
					}
				}
			}));
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			Disposables?.Dispose();
		}

		private void HandleAutoUpdate()
		{
			SuggestionItems?.FirstOrDefault()?.OnSelected();
		}
	}
}
