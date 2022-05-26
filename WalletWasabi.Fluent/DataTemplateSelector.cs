using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;

namespace WalletWasabi.Fluent;

public class DataTemplateSelector : IDataTemplate
{
	public IControl Build(object param)
	{
		return Templates.FirstOrDefault()?.Build(param) ?? new TextBlock {Text = "Not found "};
	}

	public bool Match(object data)
	{
		return true;
	}

	[Content] public IEnumerable<IDataTemplate> Templates { get; set; } = new List<IDataTemplate>();
}