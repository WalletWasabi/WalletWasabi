using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WalletWasabi.Helpers
{
	public class LabelBuilder
	{
		public List<string> Labels { get; } = new List<string>();

		public LabelBuilder(params string[] labels)
		{
			labels = labels ?? new string[] { };
			foreach (var label in labels)
			{
				Add(label);
			}
		}

		public void Add(string label)
		{
			var parts = Guard.Correct(label).Split(',', StringSplitOptions.RemoveEmptyEntries);
			foreach (var corrected in parts.Select(Guard.Correct))
			{
				if (corrected == "")
				{
					return;
				}

				if (!Labels.Contains(label))
				{
					Labels.Add(label);
				}
			}
		}

		public override string ToString()
		{
			return string.Join(", ", Labels);
		}
	}
}
