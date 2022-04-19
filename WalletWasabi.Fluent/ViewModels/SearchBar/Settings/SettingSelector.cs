using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.Settings;

public class SettingSelector : IDataTemplate
{
	// ReSharper disable once CollectionNeverUpdated.Global
	public List<IDataTemplate> DataTemplates { get; set; } = new();

	public IControl Build(object param)
	{
		var prop = param.GetType().GetProperty("Value");
		var template = DataTemplates.FirstOrDefault(d => d.Match(prop.GetValue(param)));
		if (template is not null)
		{
			return template.Build(param);
		}

		return new TextBlock {Text = "Not found"};
	}

	public bool Match(object data)
	{
		return data.GetType().Name.Contains("Setting");
	}
}