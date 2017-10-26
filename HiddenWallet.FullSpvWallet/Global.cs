using HiddenWallet.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace HiddenWallet.FullSpvWallet
{
	public static class Global
	{
		private static string _dataDir = null;
		public static string DataDir
		{
			get
			{
				if (_dataDir != null) return _dataDir;

				_dataDir = EnvironmentHelpers.GetDataDir("HiddenWallet");

				return _dataDir;
			}
		}
	}
}
