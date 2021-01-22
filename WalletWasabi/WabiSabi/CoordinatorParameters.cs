using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi
{
	public class CoordinatorParameters
	{
		public CoordinatorParameters(string dataDir)
		{
			DataDir = dataDir;
		}

		/// <summary>
		/// The main data directory of the application.
		/// </summary>
		public string DataDir { get; }

		/// <summary>
		/// Configuration of coinjoins can be modified runtime.
		/// Set how often changes in the configuration file should be monitored.
		/// </summary>
		public TimeSpan ConfigChangeMonitoringPeriod { get; init; } = TimeSpan.FromSeconds(10);
	}
}
