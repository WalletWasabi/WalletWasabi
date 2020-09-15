using Microsoft.VisualBasic.CompilerServices;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Gui.Helpers;
using Xunit;
using Utils = WalletWasabi.Gui.Helpers.Utils;

namespace WalletWasabi.Tests.UnitTests.Clients
{
	public class PreventSleepTests
	{
		[Fact]
		public void PreventSleep()
		{
			Utils.KeepSystemAwake();
		}
	}
}
