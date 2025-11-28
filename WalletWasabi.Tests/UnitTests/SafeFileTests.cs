using Nito.AsyncEx;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;
using WalletWasabi.Io;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

/// <summary>
/// Tests for <see cref="SafeFile"/>
/// </summary>
public class SafeFileTests
{
	[Fact]
	public async Task IoManagerTestsAsync()
	{
		var file = Path.Combine(Common.GetWorkDir(), "file1.dat");
		var oldFilePath = file + ".old";
		var newFilePath = file + ".new";

		List<string> lines = new();
		for (int i = 0; i < 1000; i++)
		{
			string line = RandomString.AlphaNumeric(100);

			lines.Add(line);
		}

		// Single thread file operations.

		// Delete the file if Exist.
		DeleteMe(file);
		Assert.False(Exists(file));

		// Write the data to the file.
		await WriteAllLinesAsync(file, lines);
		Assert.True(Exists(file));

		// Read back the content and check.
		static bool IsStringArraysEqual(string[] lines1, string[] lines2)
		{
			if (lines1.Length != lines2.Length)
			{
				return false;
			}

			for (int i = 0; i < lines1.Length; i++)
			{
				string line = lines2[i];
				var readLine = lines1[i];

				if (!line.Equals(readLine))
				{
					return false;
				}
			}
			return true;
		}

		var readLines = await ReadAllLinesAsync(file);

		Assert.True(IsStringArraysEqual(readLines, lines.ToArray()));

		// Write the same content, file should be rewritten.
		var currentDate = File.GetLastWriteTimeUtc(file);
		await Task.Delay(500);
		await WriteAllLinesAsync(file, lines);
		var noChangeDate = File.GetLastWriteTimeUtc(file);
		Assert.NotEqual(currentDate, noChangeDate);

		// Simulate file write error and recovery logic.
		// We have only *.new and *.old files.
		File.Copy(file, oldFilePath);
		File.Move(file, newFilePath);

		// At this point there is now OriginalFile.
		var newFile = await ReadAllLinesAsync(file);

		Assert.True(IsStringArraysEqual(newFile, lines.ToArray()));

		// Add one more line to have different data.
		lines.Add("Lorem ipsum dolor sit amet, consectetur adipiscing elit.");

		await WriteAllLinesAsync(file, lines);

		// Check recovery mechanism.
		Assert.True(
			File.Exists(file) &&
			!File.Exists(oldFilePath) &&
			!File.Exists(newFilePath));

		DeleteMe(file);

		Assert.False(Exists(file));

		// Check if directory is empty.
		var fileCount = Directory.EnumerateFiles(Path.GetDirectoryName(file)!).Count();
		Assert.Equal(0, fileCount);

		// TryReplace test.
		var dummyFilePath = $"{file}dummy";
		var dummyContent = new string[]
		{
			"banana",
			"peach"
		};
		await File.WriteAllLinesAsync(dummyFilePath, dummyContent);

		await WriteAllLinesAsync(file, lines);

		TryReplaceMeWith(file, dummyFilePath);

		var fruits = await ReadAllLinesAsync(file);

		Assert.True(IsStringArraysEqual(dummyContent, fruits));

		Assert.False(File.Exists(dummyFilePath));

		DeleteMe(file);
	}

	[Fact]
	public async Task IoTestsAsync()
	{
		var file = Path.Combine(Common.GetWorkDir(), "file.dat");

		AsyncLock asyncLock = new();
		DeleteMe(file);
		await WriteAllLinesAsync(file, []);
		Assert.False(File.Exists(file));
		IoHelpers.EnsureContainingDirectoryExists(file);
		File.Create(file).Dispose();

		static string RandomString()
		{
			StringBuilder builder = new();
			char ch;
			for (int i = 0; i < Random.Shared.Next(10, 100); i++)
			{
				ch = Convert.ToChar(Convert.ToInt32(Math.Floor((26 * Random.Shared.NextDouble()) + 65)));
				builder.Append(ch);
			}
			return builder.ToString();
		}

		var list = new List<string>();
		async Task WriteNextLineAsync()
		{
			var next = RandomString();
			lock (list)
			{
				list.Add(next);
			}

			using (await asyncLock.LockAsync())
			{
				var lines = (await ReadAllLinesAsync(file)).ToList();
				lines.Add(next);
				await WriteAllLinesAsync(file, lines);
			}
		}

		const int Iterations = 200;

		var t1 = new Thread(() =>
		{
			for (var i = 0; i < Iterations; i++)
			{
				/* We have to block the Thread.
				 * If we use async/await pattern then Join() function at the end will indicate that the Thread is finished -
				 * which is not true because the WriteNextLineAsync() is not yet finished. The reason is that await will return execution
				 * the to the calling thread it is detected as the thread is done. t1 and t2 and t3 will still run in parallel!
				 */
				WriteNextLineAsync().Wait();
			}
		});
		var t2 = new Thread(() =>
		{
			for (var i = 0; i < Iterations; i++)
			{
				WriteNextLineAsync().Wait();
			}
		});
		var t3 = new Thread(() =>
		{
			for (var i = 0; i < Iterations; i++)
			{
				WriteNextLineAsync().Wait();
			}
		});

		t1.Start();
		t2.Start();
		t3.Start();
		await Task.Delay(100);
		t1.Join();
		t2.Join();
		t3.Join();
		Assert.False(t1.IsAlive);
		Assert.False(t2.IsAlive);
		Assert.False(t3.IsAlive);

		var allLines = File.ReadAllLines(file);
		Assert.NotEmpty(allLines);

		/* Lines were added to the list and to the file parallel so the two data should be equal.
		 * If we "substract" them from each other we should get empty array.
		 */

		var diff = allLines.Except(list);
		Assert.Empty(diff);
	}

	private static void DeleteMe(string filePath)
	{
		if (File.Exists(filePath))
		{
			File.Delete(filePath);
		}

		if (File.Exists(filePath + ".new"))
		{
			File.Delete(filePath + ".new");
		}

		if (File.Exists(filePath + ".old"))
		{
			File.Delete(filePath + ".old");
		}
	}

	private static bool Exists(string file)
	{
		try
		{
			SafeFile.ReadAllText(file, Encoding.UTF8);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static Task<string[]> ReadAllLinesAsync(string file, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(SafeFile.ReadAllText(file, Encoding.Default).Split('\n', StringSplitOptions.RemoveEmptyEntries));
	}

	private static async Task WriteAllLinesAsync(string file, IEnumerable<string> lines, CancellationToken cancellationToken = default)
	{
		if (!lines.Any())
		{
			return;
		}
		var oldFilePath = file + ".old";
		var newFilePath = file + ".new";

		IoHelpers.EnsureContainingDirectoryExists(newFilePath);

		await File.WriteAllLinesAsync(newFilePath, lines, cancellationToken).ConfigureAwait(false);
		if (File.Exists(file))
		{
			if (File.Exists(oldFilePath))
			{
				File.Delete(oldFilePath);
			}

			File.Move(file, oldFilePath);
		}

		File.Move(newFilePath, file);

		if (File.Exists(oldFilePath))
		{
			File.Delete(oldFilePath);
		}
	}

	private static bool TryReplaceMeWith(string file, string sourcePath)
	{
		var oldFilePath = file + ".old";
		if (File.Exists(sourcePath))
		{
			if (File.Exists(file))
			{
				if (File.Exists(oldFilePath))
				{
					File.Delete(oldFilePath);
				}

				File.Move(file, oldFilePath);
			}

			File.Move(sourcePath, file);

			if (File.Exists(oldFilePath))
			{
				File.Delete(oldFilePath);
			}
			return true;
		}

		return false;
	}
}
