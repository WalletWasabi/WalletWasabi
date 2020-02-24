using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Models.TextResourcing
{
	public class TextResource
	{
		public Uri AvaloniaTarget { get; set; } = null;
		public string FilePath { get; set; } = null;

		public string Content { get; set; } = null;

		public bool HasAvaloniaTarget => AvaloniaTarget is { };

		public bool HasFilePath => !string.IsNullOrWhiteSpace(FilePath);
		public bool HasContent => !string.IsNullOrWhiteSpace(Content);
	}
}
