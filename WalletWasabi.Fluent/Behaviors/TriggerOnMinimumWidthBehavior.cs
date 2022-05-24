using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace WalletWasabi.Fluent.Behaviors;

internal class TriggerOnMinimumWidthBehavior : DisposingBehavior<Control>
{
	public static readonly StyledProperty<double> MinimumWidthProperty = AvaloniaProperty.Register<TriggerOnMinimumWidthBehavior, double>(
		"MinimumWidth");

	public double MinimumWidth
	{
		get => GetValue(MinimumWidthProperty);
		set => SetValue(MinimumWidthProperty, value);
	}

	private string _className = "minWidth";

	public static readonly DirectProperty<TriggerOnMinimumWidthBehavior, string> ClassNameProperty = AvaloniaProperty.RegisterDirect<TriggerOnMinimumWidthBehavior, string>(
		"ClassName", o => o.ClassName, (o, v) => o.ClassName = v);

	public string ClassName
	{
		get => _className;
		set => SetAndRaise(ClassNameProperty, ref _className, value);
	}

	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is not null)
		{
			disposables.Add(AssociatedObject.GetObservable(Visual.BoundsProperty)
				.Subscribe(bounds =>
				{
					if (bounds.Width < MinimumWidth)
					{
						AssociatedObject.Classes.Add(ClassName);
					}
					else
					{
						AssociatedObject.Classes.Remove(ClassName);
					}
				}));
		}

	}
}