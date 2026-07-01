using System.Collections.Generic;

namespace WalletWasabi.Observability;

public class ConsoleTable
{
	private readonly List<string[]> _rows = new();
	private readonly string[] _headers;

	public ConsoleTable(params string[] headers)
	{
		_headers = headers;
	}

	public void Clear()
	{
		_rows.Clear();
	}

	public void AddRow(params string[] row)
	{
		if (row.Length != _headers.Length)
		{
			throw new ArgumentException("Row must have the same number of columns as headers.");
		}

		_rows.Add(row);
	}

	public void Print()
	{
		// Calculate column widths.
		var columnWidths = new int[_headers.Length];

		for (int i = 0; i < _headers.Length; i++)
		{
			columnWidths[i] = _headers[i].Length;
		}

		foreach (var row in _rows)
		{
			for (int i = 0; i < row.Length; i++)
			{
				columnWidths[i] = Math.Max(columnWidths[i], row[i]?.Length ?? 0);
			}
		}

		// Print top border.
		PrintLine(columnWidths);

		// Print header.
		PrintRow(_headers, columnWidths);
		PrintLine(columnWidths);

		// Print rows.
		foreach (var row in _rows)
		{
			PrintRow(row, columnWidths);
		}

		// Print bottom border.
		PrintLine(columnWidths);
	}

	private void PrintLine(int[] widths)
	{
		Console.Write("+");
		foreach (var width in widths)
		{
			Console.Write(new string('-', width + 2) + "+");
		}
		Console.WriteLine();
	}

	private void PrintRow(string[] row, int[] widths)
	{
		Console.Write("|");
		for (int i = 0; i < row.Length; i++)
		{
			Console.Write($" {row[i]?.PadRight(widths[i])} |");
		}
		Console.WriteLine();
	}
}
