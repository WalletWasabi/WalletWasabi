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
/// Tests for <see cref="SafeIoManager"/>
/// </summary>
public class SafeIoManagerTests
{
	[Fact]
	public async Task IoManagerTestsAsync()
	{
		var file = Path.Combine(Common.GetWorkDir(nameof(IoManagerTestsAsync)), "file1.dat");

		List<string> lines = new();
		for (int i = 0; i < 1000; i++)
		{
			string line = RandomString.AlphaNumeric(100);

			lines.Add(line);
		}

		// Single thread file operations.
		SafeIoManager ioman1 = new(file);

		// Delete the file if Exist.
		ioman1.DeleteMe();
		Assert.False(ioman1.Exists());

		// Write the data to the file.
		await ioman1.WriteAllLinesAsync(lines);
		Assert.True(ioman1.Exists());

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

		var readLines = await ioman1.ReadAllLinesAsync();

		Assert.True(IsStringArraysEqual(readLines, lines.ToArray()));

		// Write the same content, file should be rewritten.
		var currentDate = File.GetLastWriteTimeUtc(ioman1.FilePath);
		await Task.Delay(500);
		await ioman1.WriteAllLinesAsync(lines);
		var noChangeDate = File.GetLastWriteTimeUtc(ioman1.FilePath);
		Assert.NotEqual(currentDate, noChangeDate);

		// Simulate file write error and recovery logic.
		// We have only *.new and *.old files.
		File.Copy(ioman1.FilePath, ioman1.OldFilePath);
		File.Move(ioman1.FilePath, ioman1.NewFilePath);

		// At this point there is now OriginalFile.
		var newFile = await ioman1.ReadAllLinesAsync();

		Assert.True(IsStringArraysEqual(newFile, lines.ToArray()));

		// Add one more line to have different data.
		lines.Add("Lorem ipsum dolor sit amet, consectetur adipiscing elit.");

		await ioman1.WriteAllLinesAsync(lines);

		// Check recovery mechanism.
		Assert.True(
			File.Exists(ioman1.FilePath) &&
			!File.Exists(ioman1.OldFilePath) &&
			!File.Exists(ioman1.NewFilePath));

		ioman1.DeleteMe();

		Assert.False(ioman1.Exists());

		// Check if directory is empty.
		var fileCount = Directory.EnumerateFiles(Path.GetDirectoryName(ioman1.FilePath)!).Count();
		Assert.Equal(0, fileCount);

		// TryReplace test.
		var dummyFilePath = $"{ioman1.FilePath}dummy";
		var dummyContent = new string[]
		{
			"banana",
			"peach"
		};
		await File.WriteAllLinesAsync(dummyFilePath, dummyContent);

		await ioman1.WriteAllLinesAsync(lines);

		ioman1.TryReplaceMeWith(dummyFilePath);

		var fruits = await ioman1.ReadAllLinesAsync();

		Assert.True(IsStringArraysEqual(dummyContent, fruits));

		Assert.False(File.Exists(dummyFilePath));

		ioman1.DeleteMe();
	}

	[Fact]
	public async Task IoTestsAsync()
	{
		var file = Path.Combine(Common.GetWorkDir(nameof(IoTestsAsync)), "file.dat");

		AsyncLock asyncLock = new();
		SafeIoManager ioman = new(file);
		ioman.DeleteMe();
		await ioman.WriteAllLinesAsync(Array.Empty<string>());
		Assert.False(File.Exists(ioman.FilePath));
		IoHelpers.EnsureContainingDirectoryExists(ioman.FilePath);
		File.Create(ioman.FilePath).Dispose();

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
				var lines = (await ioman.ReadAllLinesAsync()).ToList();
				lines.Add(next);
				await ioman.WriteAllLinesAsync(lines);
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
}
