using Microsoft.Extensions.Hosting;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Observability;

/// <summary>
/// Service to periodically display a snapshot of tracked metrics.
/// </summary>
public class MetricDisplayService(MetricManager metricManager) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (MetricManager.EnableMetricConsoleSnapshots)
		{
			var table = new ConsoleTable("Metric", "Type", "Value");

			while (!stoppingToken.IsCancellationRequested)
			{
				await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);

				table.Clear();
				var snapshot = metricManager.GetMetricsSnapshot();

				foreach ((var name, var value) in snapshot)
				{
					var type = name.EndsWith(".histogram", StringComparison.Ordinal) || name.EndsWith(".gauge", StringComparison.Ordinal)
						? "Last" : "Count";
					var unit = Metrics.GetUnit(name);
					var space = unit != "" ? " " : "";
					var valueStr = string.Create(CultureInfo.InvariantCulture, $"{value}{space}{unit}");

					table.AddRow(name, type, valueStr);
				}

				Console.WriteLine();
				table.Print();
				Console.WriteLine();
			}
		}
	}
}
