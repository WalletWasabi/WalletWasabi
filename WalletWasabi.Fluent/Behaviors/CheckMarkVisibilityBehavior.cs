using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Xaml.Interactivity;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Behaviors;

public class CheckMarkVisibilityBehavior : Behavior<PathIcon>
{
	private CompositeDisposable? _disposables;

	public static readonly StyledProperty<TextBox> OwnerTextBoxProperty =
		AvaloniaProperty.Register<CheckMarkVisibilityBehavior, TextBox>(nameof(OwnerTextBox));

	[ResolveByName]
	public TextBox OwnerTextBox
	{
		get => GetValue(OwnerTextBoxProperty);
		set => SetValue(OwnerTextBoxProperty, value);
	}

	protected override void OnAttached()
	{
		this.WhenAnyValue(x => x.OwnerTextBox)
			.Subscribe(
				x =>
				{
					_disposables?.Dispose();

					if (x is not null)
					{
						_disposables = new CompositeDisposable();

						var hasErrors = OwnerTextBox.GetObservable(DataValidationErrors.HasErrorsProperty);
						var text = OwnerTextBox.GetObservable(TextBox.TextProperty);

						hasErrors.ToSignal()
							.Merge(text.ToSignal())
							.Throttle(TimeSpan.FromMilliseconds(100))
							.ObserveOn(RxApp.MainThreadScheduler)
							.Subscribe(
								_ =>
								{
									if (AssociatedObject is { })
									{
										AssociatedObject.Opacity =
											!DataValidationErrors.GetHasErrors(OwnerTextBox) &&
											!string.IsNullOrEmpty(OwnerTextBox.Text)
												? 1
												: 0;
									}
								})
							.DisposeWith(_disposables);
					}
				});
	}
}
