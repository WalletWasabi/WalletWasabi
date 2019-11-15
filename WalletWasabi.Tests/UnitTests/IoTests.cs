using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Io;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class IoTests
	{
		[Fact]
		public async Task IoManagerTestsAsync()
		{
			var file1 = Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.GetMethodName(), $"file1.dat");
			var file2 = Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.GetMethodName(), $"file2.dat");

			Random random = new Random();
			List<string> lines = new List<string>();
			for (int i = 0; i < 1000; i++)
			{
				string line = new string(Enumerable.Repeat(Constants.Chars, 100)
					.Select(s => s[random.Next(s.Length)]).ToArray());

				lines.Add(line);
			}

			// Single thread file operations.

			DigestableSafeMutexIoManager ioman1 = new DigestableSafeMutexIoManager(file1);

			// Delete the file if Exist.

			ioman1.DeleteMe();
			Assert.False(ioman1.Exists());

			Assert.False(File.Exists(ioman1.DigestFilePath));

			// Write the data to the file.

			await ioman1.WriteAllLinesAsync(lines);
			Assert.True(ioman1.Exists());

			// Check if the digest file is created.

			Assert.True(File.Exists(ioman1.DigestFilePath));

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

			// Check digest file, and write only differ logic.

			// Write the same content, file should not be written.
			var currentDate = File.GetLastWriteTimeUtc(ioman1.FilePath);
			await Task.Delay(500);
			await ioman1.WriteAllLinesAsync(lines);
			var noChangeDate = File.GetLastWriteTimeUtc(ioman1.FilePath);
			Assert.Equal(currentDate, noChangeDate);

			// Write different content, file should be written.
			currentDate = File.GetLastWriteTimeUtc(ioman1.FilePath);
			await Task.Delay(500);
			lines.Add("Lorem ipsum dolor sit amet, consectetur adipiscing elit.");
			await ioman1.WriteAllLinesAsync(lines);
			var newContentDate = File.GetLastWriteTimeUtc(ioman1.FilePath);
			Assert.NotEqual(currentDate, newContentDate);

			/* The next test is commented out because on mac and on linux File.Open does not lock the file
			 * it can be still written by the ioman1.WriteAllLinesAsync(). Tried with FileShare.None FileShare.Delete
			 * FileStream.Lock none of them are working or caused not supported on this platform exception.
			 * So there is no OP system way to garantee that the file won't be written during another write operation.
			 * For example git is using lock files to solve this problem. We are using system wide mutexes.
			 * For now there is no other way to do this. Some useful links :
			 * https://stackoverflow.com/questions/2751734/how-do-filesystems-handle-concurrent-read-write
			 * https://github.com/dotnet/corefx/issues/5964
			 */

			//using (File.OpenWrite(ioman1.FilePath))
			//{
			//	// Should be OK because the same data is written.
			//	await ioman1.WriteAllLinesAsync(lines);
			//}
			//using (File.OpenWrite(ioman1.FilePath))
			//{
			//	// Should fail because different data is written.
			//	await Assert.ThrowsAsync<IOException>(async () => await ioman1.WriteAllLinesAsync(lines));
			//}

			await ioman1.WriteAllLinesAsync(lines);

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

			var fileCount = Directory.EnumerateFiles(Path.GetDirectoryName(ioman1.FilePath)).Count();
			Assert.Equal(0, fileCount);

			// Check Mutex usage on simultaneous file writes.

			DigestableSafeMutexIoManager ioman2 = new DigestableSafeMutexIoManager(file2);

			await Task.Run(async () =>
			{
				using (await ioman1.Mutex.LockAsync())
				{
					// Should not be a problem because they use different Mutexes.
					using (await ioman2.Mutex.LockAsync())
					{
						await ioman1.WriteAllLinesAsync(lines);
						await ioman2.WriteAllLinesAsync(lines);
						ioman1.DeleteMe();
						ioman2.DeleteMe();
					}
				}
			});

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
			var file = Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.GetMethodName(), $"file.dat");

			DigestableSafeMutexIoManager ioman = new DigestableSafeMutexIoManager(file);
			ioman.DeleteMe();
			await ioman.WriteAllLinesAsync(Array.Empty<string>());
			Assert.False(File.Exists(ioman.FilePath));
			IoHelpers.EnsureContainingDirectoryExists(ioman.FilePath);
			File.Create(ioman.FilePath).Dispose();

			static string RandomString()
			{
				StringBuilder builder = new StringBuilder();
				var rnd = new Random();
				char ch;
				for (int i = 0; i < rnd.Next(10, 100); i++)
				{
					ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * rnd.NextDouble() + 65)));
					builder.Append(ch);
				}
				return builder.ToString();
			};

			var list = new List<string>();
			async Task WriteNextLineAsync()
			{
				var next = RandomString();
				lock (list)
				{
					list.Add(next);
				}
				using (await ioman.Mutex.LockAsync())
				{
					var lines = (await ioman.ReadAllLinesAsync()).ToList();
					lines.Add(next);
					await ioman.WriteAllLinesAsync(lines);
				}
			};

			const int iterations = 200;

			var t1 = new Thread(() =>
			{
				for (var i = 0; i < iterations; i++)
				{
					/* We have to block the Thread.
					 * If we use async/await pattern then Join() function at the end will indicate that the Thread is finished -
					 * which is not true bacause the WriteNextLineAsync() is not yet finished. The reason is that await will return execution
					 * the to the calling thread it is detected as the thread is done. t1 and t2 and t3 will still run in parallel!
					 */
					WriteNextLineAsync().Wait();
				}
			});
			var t2 = new Thread(() =>
			{
				for (var i = 0; i < iterations; i++)
				{
					WriteNextLineAsync().Wait();
				}
			});
			var t3 = new Thread(() =>
			{
				for (var i = 0; i < iterations; i++)
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
}
