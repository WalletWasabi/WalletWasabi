using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Xaml.Interactions.Custom;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.Behaviors;

public class FlyoutSuggestionBehavior : AttachedToVisualTreeBehavior<Control>
{
	public static readonly StyledProperty<string> ContentProperty = AvaloniaProperty.Register<FlyoutSuggestionBehavior, string>(nameof(Content));

	public static readonly StyledProperty<IDataTemplate> HintTemplateProperty = AvaloniaProperty.Register<FlyoutSuggestionBehavior, IDataTemplate>(nameof(HintTemplate));

	public static readonly StyledProperty<TextBox?> TargetProperty = AvaloniaProperty.Register<FlyoutSuggestionBehavior, TextBox?>(nameof(Target));

	public static readonly StyledProperty<PlacementMode> PlacementModeProperty = AvaloniaProperty.Register<FlyoutSuggestionBehavior, PlacementMode>(nameof(PlacementMode));

	private readonly Flyout _flyout;

	public FlyoutSuggestionBehavior()
	{
		_flyout = new Flyout { ShowMode = FlyoutShowMode.Transient };
	}

	public PlacementMode PlacementMode
	{
		get => GetValue(PlacementModeProperty);
		set => SetValue(PlacementModeProperty, value);
	}

	public string Content
	{
		get => GetValue(ContentProperty);
		set => SetValue(ContentProperty, value);
	}

	public IDataTemplate HintTemplate
	{
		get => GetValue(HintTemplateProperty);
		set => SetValue(HintTemplateProperty, value);
	}

	public TextBox? Target
	{
		get => GetValue(TargetProperty);
		set => SetValue(TargetProperty, value);
	}

	public StringComparer EqualityComparer { get; set; } = StringComparer.InvariantCulture;

	protected override IDisposable OnAttachedToVisualTreeOverride()
	{
		var targets = this
			.WhenAnyValue(x => x.Target)
			.WhereNotNull();

		var contents = this
			.GetObservable(ContentProperty);

		var focusChanges = targets
			.Select(target => target.GetObservable(InputElement.IsFocusedProperty))
			.Switch();

		var hideOnLostFocus = focusChanges
			.Where(focus => !focus);

		var hideOnTextChange = targets
			.Select(target => target.GetObservable(TextBox.TextProperty))
			.Switch()
			.WithLatestFrom(contents)
			.Select(_ => false);

		var showOnGotFocus = focusChanges
			.Where(focus => focus)
			.Delay(TimeSpan.FromSeconds(0.2), RxApp.MainThreadScheduler)
			.WithLatestFrom(targets, (_, target) => target)
			.WithLatestFrom(contents, (tb, newText) => new { TextBox = tb, NewText = newText, CurrentText = tb.Text })
			.Where(arg => !string.IsNullOrWhiteSpace(arg.NewText))
			.Where(x => !EqualityComparer.Equals(x.CurrentText, x.NewText))
			.Do(x => _flyout.Content = CreateSuggestion(x.TextBox, x.NewText))
			.Select(_ => true);

		var disposable = new CompositeDisposable();

		targets
			.Subscribe(target => FlyoutHelpers.ShowFlyout(target, _flyout, showOnGotFocus.Merge(hideOnLostFocus).Merge(hideOnTextChange), disposable))
			.DisposeWith(disposable);

		Target ??= AssociatedObject as TextBox;

		this.WhenAnyValue(x => x.PlacementMode)
			.Do(x => _flyout.Placement = x)
			.Subscribe()
			.DisposeWith(disposable);

		return disposable;
	}

	private Suggestion CreateSuggestion(TextBox? textBox, string content)
	{
		return new Suggestion(
			content,
			() =>
			{
				if (textBox != null)
				{
					textBox.SetCurrentValue(TextBox.TextProperty, content);
				}

				_flyout.Hide();
			});
	}
}
