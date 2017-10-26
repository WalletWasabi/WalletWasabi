using HiddenWallet.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler
{
	public static class Global
	{
		public static TumblerStateMachine StateMachine;

		private static string _dataDir = null;
		public static string DataDir
		{
			get
			{
				if (_dataDir != null) return _dataDir;

				_dataDir = EnvironmentHelpers.GetDataDir("ChaumianTumbler");

				return _dataDir;
			}
		}
	}
}
