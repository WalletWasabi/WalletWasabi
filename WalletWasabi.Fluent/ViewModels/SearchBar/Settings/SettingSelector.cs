using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.Settings;

public class SettingSelector : IDataTemplate
{
	public List<IDataTemplate> DataTemplates { get; set; } = new();

	public Control Build(object? param)
	{
		if (param is null)
		{ 
			return CreateNotFoundControl();
		}

		var template = DataTemplates.FirstOrDefault(d =>
		{
			var prop = param.GetType().GetProperty("Value");
			var value = prop?.GetValue(param);

			if (value is null)
			{
				return false;
			}

			return d.Match(value);
		});

		if (template is not null)
		{
			return template.Build(param) ?? CreateNotFoundControl();
		}

		return CreateNotFoundControl();
	}

	public bool Match(object? data) =>
		data is not null && data.GetType().Name.Contains("Setting");

	private static TextBlock CreateNotFoundControl() =>
		new() { Text = "Not found" };
}
