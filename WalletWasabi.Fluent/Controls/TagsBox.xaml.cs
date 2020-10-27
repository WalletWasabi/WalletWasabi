using System.Collections;
using System.Linq;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Controls
{
	/// <summary>
    /// </summary>
    public class TagsBox : ItemsControl
    {
	    private IEnumerable _suggestionsEnumerable;
	    private AutoCompleteBox _autoCompleteBox;

	    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	    {
		    base.OnApplyTemplate(e);

		    Presenter.ApplyTemplate();

		    _autoCompleteBox =(Presenter.Panel as ConcatenatingWrapPanel).ConcatenatedChildren.OfType<AutoCompleteBox>().FirstOrDefault();
	    }

	    public static readonly DirectProperty<TagsBox, IEnumerable> SuggestionsProperty =
		    AvaloniaProperty.RegisterDirect<TagsBox, IEnumerable>(
			    nameof(Suggestions),
			    o => o.Suggestions,
			    (o, v) => o.Suggestions = v);

	    public IEnumerable Suggestions
	    {
		    get => _suggestionsEnumerable;
		    set => SetAndRaise(SuggestionsProperty, ref _suggestionsEnumerable, value);
	    }
    }
}