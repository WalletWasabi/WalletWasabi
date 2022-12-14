using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Xaml.Interactivity;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class FlyoutSuggestionBehavior : Behavior<Control>
{
	public static readonly StyledProperty<string> ContentProperty = AvaloniaProperty.Register<FlyoutSuggestionBehavior, string>(nameof(Content));

	public static readonly StyledProperty<IDataTemplate> HintTemplateProperty = AvaloniaProperty.Register<FlyoutSuggestionBehavior, IDataTemplate>(nameof(HintTemplate));

	public static readonly StyledProperty<Control?> TargetProperty = AvaloniaProperty.Register<FlyoutSuggestionBehavior, Control?>(nameof(Target));

	public static readonly StyledProperty<FlyoutPlacementMode> FlyoutPlacementProperty = AvaloniaProperty.Register<FlyoutSuggestionBehavior, FlyoutPlacementMode>(nameof(PlacementMode));

	private readonly CompositeDisposable _disposables = new();

	private readonly Flyout _flyout;

	public FlyoutSuggestionBehavior()
	{
		_flyout = new Flyout { ShowMode = FlyoutShowMode.Transient };

		this.WhenAnyValue(x => x.PlacementMode)
			.Do(x => _flyout.Placement = x)
			.Subscribe()
			.DisposeWith(_disposables);
	}

	public FlyoutPlacementMode PlacementMode
	{
		get => GetValue(FlyoutPlacementProperty);
		set => SetValue(FlyoutPlacementProperty, value);
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

	public Control? Target
	{
		get => GetValue(TargetProperty);
		set => SetValue(TargetProperty, value);
	}

	protected override void OnAttachedToVisualTree()
	{
		var targets = this
			.WhenAnyValue(x => x.Target)
			.WhereNotNull();

		Displayer(targets).DisposeWith(_disposables);
		Hider(targets).DisposeWith(_disposables);

		Target ??= AssociatedObject as TextBox;

		base.OnAttachedToVisualTree();
	}

	private IDisposable Hider(IObservable<Control> targets)
	{
		return targets
			.Select(x => Observable.FromEventPattern(x, nameof(x.LostFocus)))
			.Switch()
			.Select(x => (TextBox?) x.Sender)
			.Do(_ => _flyout.Hide())
			.Subscribe();
	}

	private IDisposable Displayer(IObservable<Control> targets)
	{
		return targets
			.Select(x => Observable.FromEventPattern(x, nameof(x.GotFocus)))
			.Switch()
			.Select(x => (TextBox?) x.Sender)
			.WithLatestFrom(this.WhenAnyValue(x => x.Content))
			.Where(tuple => !string.IsNullOrWhiteSpace(tuple.Second) && tuple.First?.Text != tuple.Second)
			.Select(tuple => CreateSuggestion(tuple.First, tuple.Second))
			.Do(ShowHint)
			.Subscribe();
	}

	private Suggestion CreateSuggestion(TextBox? textBox, string content)
	{
		return new Suggestion(
			content,
			() =>
			{
				if (textBox != null)
				{
					textBox.Text = content;
				}

				_flyout.Hide();
			});
	}

	private void ShowHint(Suggestion suggestion)
	{
		_flyout.Content = new ContentControl { ContentTemplate = HintTemplate, Content = suggestion };
		if (Target != null)
		{
			_flyout.ShowAt(Target);
		}
	}
}

public class Suggestion : ReactiveObject
{
	public Suggestion(string text, Action onAccept)
	{
		Text = text;
		AcceptHintCommand = ReactiveCommand.Create(onAccept);
	}

	public string Text { get; }

	public ReactiveCommand<Unit, Unit> AcceptHintCommand { get; set; }
}
