using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors
{
	public class TriggerStyleClassBehavior : DisposingBehavior<Control>
	{
		public static readonly StyledProperty<bool> StyleTriggerProperty =
			AvaloniaProperty.Register<FocusBehavior, bool>(nameof(StyleTrigger), defaultBindingMode: BindingMode.TwoWay);

		public static readonly StyledProperty<string> ClassesProperty =
			AvaloniaProperty.Register<FocusBehavior, string>(nameof(Classes));

		public bool StyleTrigger
		{
			get => GetValue(StyleTriggerProperty);
			set => SetValue(StyleTriggerProperty, value);
		}

		public string Classes
		{
			get => GetValue(ClassesProperty);
			set => SetValue(ClassesProperty, value);
		}

		protected override void OnAttached(CompositeDisposable disposables)
		{
			this.WhenAnyValue(x => x.StyleTrigger)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(styleTrigger =>
				{
					if (AssociatedObject is null || styleTrigger == false)
					{
						return;
					}

					foreach (string c in Classes.Split(" "))
					{
						AssociatedObject.Classes.Remove(c);
						AssociatedObject.Classes.Add(c);
					}
				})
				.DisposeWith(disposables);
		}
	}
}