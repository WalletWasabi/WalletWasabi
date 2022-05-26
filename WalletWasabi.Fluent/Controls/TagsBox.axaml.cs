using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Controls;

public class TagsBox : TemplatedControl
{
	private CompositeDisposable? _compositeDisposable;
	private AutoCompleteBox? _autoCompleteBox;
	private TextBox? _internalTextBox;
	private TextBlock? _watermark;
	private IControl? _containerControl;
	private StringComparison _stringComparison;
	private bool _isInputEnabled = true;
	private IList<string>? _suggestions;
	private IEnumerable<string>? _items;
	private IEnumerable<string>? _topItems;
	private bool _requestAdd;

	public static readonly StyledProperty<bool> IsCurrentTextValidProperty =
		AvaloniaProperty.Register<TagsBox, bool>(nameof(IsCurrentTextValid));

	public static readonly DirectProperty<TagsBox, bool> RequestAddProperty =
		AvaloniaProperty.RegisterDirect<TagsBox, bool>(nameof(RequestAdd), o => o.RequestAdd);

	public static readonly StyledProperty<string> WatermarkProperty =
		TextBox.WatermarkProperty.AddOwner<TagsBox>();

	public static readonly StyledProperty<bool> RestrictInputToSuggestionsProperty =
		AvaloniaProperty.Register<TagsBox, bool>(nameof(RestrictInputToSuggestions));

	public static readonly StyledProperty<int> ItemCountLimitProperty =
		AvaloniaProperty.Register<TagsBox, int>(nameof(ItemCountLimit));

	public static readonly StyledProperty<int> MaxTextLengthProperty =
		AvaloniaProperty.Register<TagsBox, int>(nameof(MaxTextLength));

	public static readonly StyledProperty<char> TagSeparatorProperty =
		AvaloniaProperty.Register<TagsBox, char>(nameof(TagSeparator), defaultValue: ' ');

	public static readonly StyledProperty<bool> SuggestionsAreCaseSensitiveProperty =
		AvaloniaProperty.Register<TagsBox, bool>(nameof(SuggestionsAreCaseSensitive), defaultValue: true);

	public static readonly StyledProperty<bool> AllowDuplicationProperty =
		AvaloniaProperty.Register<TagsBox, bool>(nameof(AllowDuplication));

	public static readonly DirectProperty<TagsBox, IEnumerable<string>?> ItemsProperty =
		AvaloniaProperty.RegisterDirect<TagsBox, IEnumerable<string>?>(nameof(Items),
			o => o.Items,
			(o, v) => o.Items = v,
			enableDataValidation: true);

	public static readonly DirectProperty<TagsBox, IEnumerable<string>?> TopItemsProperty =
		AvaloniaProperty.RegisterDirect<TagsBox, IEnumerable<string>?>(nameof(TopItems),
			o => o.TopItems,
			(o, v) => o.TopItems = v);

	public static readonly DirectProperty<TagsBox, IList<string>?> SuggestionsProperty =
		AvaloniaProperty.RegisterDirect<TagsBox, IList<string>?>(
			nameof(Suggestions),
			o => o.Suggestions,
			(o, v) => o.Suggestions = v);

	public static readonly StyledProperty<bool> IsReadOnlyProperty =
		AvaloniaProperty.Register<TagsBox, bool>(nameof(IsReadOnly));

	public static readonly StyledProperty<bool> EnableCounterProperty =
		AvaloniaProperty.Register<TagsBox, bool>(nameof(EnableCounter));

	public static readonly StyledProperty<bool> EnableDeleteProperty =
		AvaloniaProperty.Register<TagsBox, bool>(nameof(EnableDelete), true);

	[Content]
	public IEnumerable<string>? Items
	{
		get => _items;
		set => SetAndRaise(ItemsProperty, ref _items, value);
	}

	public bool IsCurrentTextValid
	{
		get => GetValue(IsCurrentTextValidProperty);
		private set => SetValue(IsCurrentTextValidProperty, value);
	}

	public bool RequestAdd
	{
		get => _requestAdd;
		set => SetAndRaise(RequestAddProperty, ref _requestAdd, value);
	}

	public IEnumerable<string>? TopItems
	{
		get => _topItems;
		set => SetAndRaise(TopItemsProperty, ref _topItems, value);
	}

	public string Watermark
	{
		get => GetValue(WatermarkProperty);
		set => SetValue(WatermarkProperty, value);
	}

	public bool RestrictInputToSuggestions
	{
		get => GetValue(RestrictInputToSuggestionsProperty);
		set => SetValue(RestrictInputToSuggestionsProperty, value);
	}

	public int ItemCountLimit
	{
		get => GetValue(ItemCountLimitProperty);
		set => SetValue(ItemCountLimitProperty, value);
	}

	public char TagSeparator
	{
		get => GetValue(TagSeparatorProperty);
		set => SetValue(TagSeparatorProperty, value);
	}

	public IList<string>? Suggestions
	{
		get => _suggestions;
		set => SetAndRaise(SuggestionsProperty, ref _suggestions, value);
	}

	public bool IsReadOnly
	{
		get => GetValue(IsReadOnlyProperty);
		set => SetValue(IsReadOnlyProperty, value);
	}

	public bool SuggestionsAreCaseSensitive
	{
		get => GetValue(SuggestionsAreCaseSensitiveProperty);
		set => SetValue(SuggestionsAreCaseSensitiveProperty, value);
	}

	public bool AllowDuplication
	{
		get => GetValue(AllowDuplicationProperty);
		set => SetValue(AllowDuplicationProperty, value);
	}

	public bool EnableCounter
	{
		get => GetValue(EnableCounterProperty);
		set => SetValue(EnableCounterProperty, value);
	}

	public bool EnableDelete
	{
		get => GetValue(EnableDeleteProperty);
		set => SetValue(EnableDeleteProperty, value);
	}

	public int MaxTextLength
	{
		get => GetValue(MaxTextLengthProperty);
		set => SetValue(MaxTextLengthProperty, value);
	}

	private string CurrentText => _autoCompleteBox?.Text ?? "";

	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		base.OnApplyTemplate(e);

		_compositeDisposable?.Dispose();
		_compositeDisposable = new CompositeDisposable();

		_watermark = e.NameScope.Find<TextBlock>("PART_Watermark");
		var presenter = e.NameScope.Find<ItemsPresenter>("PART_ItemsPresenter");
		presenter.ApplyTemplate();
		_containerControl = presenter.Panel;
		_autoCompleteBox = (_containerControl as ConcatenatingWrapPanel)?.ConcatenatedChildren.OfType<AutoCompleteBox>().FirstOrDefault();

		if (_autoCompleteBox is null)
		{
			return;
		}

		Observable.FromEventPattern<TemplateAppliedEventArgs>(_autoCompleteBox, nameof(TemplateApplied))
			.Subscribe(args =>
			{
				_internalTextBox = args.EventArgs.NameScope.Find<TextBox>("PART_TextBox");
				var suggestionListBox = args.EventArgs.NameScope.Find<ListBox>("PART_SelectingItemsControl");

				_internalTextBox.WhenAnyValue(x => x.IsFocused)
					.Where(isFocused => isFocused == false)
					.Subscribe(_ => RequestAdd = true)
					.DisposeWith(_compositeDisposable);

				Observable
					.FromEventPattern(suggestionListBox, nameof(PointerReleased))
					.Subscribe(_ => RequestAdd = true)
					.DisposeWith(_compositeDisposable);
			})
			.DisposeWith(_compositeDisposable);

		_autoCompleteBox
			.AddDisposableHandler(TextInputEvent, OnTextInput, RoutingStrategies.Tunnel)
			.DisposeWith(_compositeDisposable);

		_autoCompleteBox
			.AddDisposableHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel)
			.DisposeWith(_compositeDisposable);

		LayoutUpdated += OnLayoutUpdated;

		_autoCompleteBox.WhenAnyValue(x => x.Text)
			.WhereNotNull()
			.Where(text => text.Contains(TagSeparator))
			.Subscribe(_ => RequestAdd = true)
			.DisposeWith(_compositeDisposable);

		this.WhenAnyValue(x => x.RequestAdd)
			.Where(x => x)
			.Throttle(TimeSpan.FromMilliseconds(10))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(_ => CurrentText)
			.Subscribe(currentText =>
			{
				Dispatcher.UIThread.Post(() => RequestAdd = false);
				ClearInputField();

				var tags = GetFinalTags(currentText, TagSeparator);

				foreach (string tag in tags)
				{
					AddTag(tag);
				}
			});

		_autoCompleteBox.WhenAnyValue(x => x.Text)
			.Subscribe(_ =>
			{
				InvalidateWatermark();
				CheckIsCurrentTextValid();
			})
			.DisposeWith(_compositeDisposable);
	}

	private void CheckIsCurrentTextValid()
	{
		var correctedInput = CurrentText.ParseLabel();

		if (RestrictInputToSuggestions && Suggestions is { } suggestions)
		{
			IsCurrentTextValid = suggestions.Any(x => x.Equals(correctedInput, _stringComparison));
			return;
		}

		if (!RestrictInputToSuggestions)
		{
			IsCurrentTextValid = !string.IsNullOrEmpty(correctedInput);
			return;
		}

		IsCurrentTextValid = false;
	}

	private void OnKeyDown(object? sender, KeyEventArgs e)
	{
		if (!_isInputEnabled && e.Key != Key.Back)
		{
			return;
		}

		var emptyInputField = string.IsNullOrEmpty(CurrentText);

		switch (e.Key)
		{
			case Key.Back when emptyInputField:
				RemoveLastTag();
				break;

			case Key.Enter or Key.Tab when !emptyInputField:
				RequestAdd = true;
				e.Handled = true;
				break;
		}
	}

	private void ClearInputField()
	{
		_autoCompleteBox?.ClearValue(AutoCompleteBox.SelectedItemProperty);
		Dispatcher.UIThread.Post(() => _autoCompleteBox?.ClearValue(AutoCompleteBox.TextProperty));
	}

	private IEnumerable<string> GetFinalTags(string input, char tagSeparator)
	{
		var tags = input.Split(tagSeparator);

		foreach (string tag in tags)
		{
			var correctedTag = tag.ParseLabel();

			if (!string.IsNullOrEmpty(correctedTag))
			{
				yield return correctedTag;
			}
		}
	}

	private void OnLayoutUpdated(object? sender, EventArgs e)
	{
		UpdateCounters();
	}

	private void UpdateCounters()
	{
		var tagItems = _containerControl.GetVisualDescendants().OfType<TagControl>().ToArray();

		for (var i = 0; i < tagItems.Length; i++)
		{
			tagItems[i].OrdinalIndex = i + 1;
		}
	}

	protected override void OnGotFocus(GotFocusEventArgs e)
	{
		base.OnGotFocus(e);

		_internalTextBox?.Focus();
	}

	private void CheckIsInputEnabled()
	{
		if (Items is IList items && ItemCountLimit > 0)
		{
			_isInputEnabled = items.Count < ItemCountLimit;
		}
	}

	private void InvalidateWatermark()
	{
		if (_watermark is { })
		{
			_watermark.IsVisible = (Items is null || (Items is { } && !Items.Any())) && string.IsNullOrEmpty(CurrentText);
		}
	}

	private void OnTextInput(object? sender, TextInputEventArgs e)
	{
		if (sender is not AutoCompleteBox autoCompleteBox)
		{
			return;
		}

		var typedFullText = autoCompleteBox.SearchText + e.Text;

		if (!_isInputEnabled ||
			(typedFullText is { Length: 1 } && typedFullText.StartsWith(TagSeparator)) ||
			string.IsNullOrEmpty(typedFullText.ParseLabel()))
		{
			e.Handled = true;
			return;
		}

		var suggestions = Suggestions?.ToArray();

		if (RestrictInputToSuggestions &&
			suggestions is { } &&
			!suggestions.Any(x => x.StartsWith(typedFullText, _stringComparison)))
		{
			if (!typedFullText.EndsWith(TagSeparator) ||
				(typedFullText.EndsWith(TagSeparator) && !suggestions.Contains(autoCompleteBox.SearchText)))
			{
				e.Handled = true;
				return;
			}
		}

		if (e.Text is { Length: 1 } && e.Text.StartsWith(TagSeparator))
		{
			autoCompleteBox.Text = autoCompleteBox.SearchText;
			RequestAdd = true;
			e.Handled = true;
		}
	}

	protected override void UpdateDataValidation<T>(AvaloniaProperty<T> property, BindingValue<T> value)
	{
		if (property == ItemsProperty)
		{
			DataValidationErrors.SetError(this, value.Error);
		}
	}

	protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> e)
	{
		base.OnPropertyChanged(e);

		if (e.Property == IsReadOnlyProperty)
		{
			PseudoClasses.Set(":readonly", IsReadOnly);
		}
		else if (e.Property == SuggestionsAreCaseSensitiveProperty)
		{
			_stringComparison = SuggestionsAreCaseSensitive
				? StringComparison.CurrentCulture
				: StringComparison.CurrentCultureIgnoreCase;
		}
	}

	private void RemoveLastTag()
	{
		if (Items is IList { Count: > 0 } items)
		{
			RemoveAt(items.Count - 1);
		}
	}

	public void RemoveAt(int index)
	{
		if (Items is not IList items)
		{
			return;
		}

		items.RemoveAt(index);
		CheckIsInputEnabled();
		InvalidateWatermark();
	}

	public void AddTag(string tag)
	{
		var inputTag = tag;

		if (Items is not IList items)
		{
			return;
		}

		if (ItemCountLimit > 0 && items.Count + 1 > ItemCountLimit)
		{
			return;
		}

		if (!AllowDuplication && items.Contains(tag))
		{
			return;
		}

		if (Suggestions is { } suggestions)
		{
			if (RestrictInputToSuggestions &&
			    !suggestions.Any(x => x.Equals(tag, _stringComparison)))
			{
				return;
			}

			// When user tries to commit a tag,
			// check if it's already in the suggestions list
			// by comparing it case-insensitively.
			var result = suggestions.FirstOrDefault(x => x.Equals(tag, StringComparison.CurrentCultureIgnoreCase));

			if (result is not null)
			{
				inputTag = result;
			}
		}

		items.Add(inputTag);
		CheckIsInputEnabled();
		InvalidateWatermark();
	}
}
