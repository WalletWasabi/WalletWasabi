using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Hwi2.Models
{
	public class HwiOption
	{
		public HwiOption(HwiOptions type, string argument = null)
		{
			Type = type;
			Arguments = argument;
		}

		public HwiOptions Type { get; }
		public string Arguments { get; }
	}
}
