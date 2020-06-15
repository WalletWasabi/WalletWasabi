using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Legal;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class LegalTests
	{
		[Fact]
		public void CanCreateLegalDocuments()
		{
			var version = new Version(1, 1);
			var legal = new LegalDocuments($"{version}.txt");
			Assert.Equal("1.1.txt", legal.FilePath);
			Assert.Equal(version, legal.Version);
		}

		[Fact]
		public void LegalDocumentsCreationFixesPath()
		{
			var legal = new LegalDocuments($"  1.1.txt  ");
			Assert.Equal("1.1.txt", legal.FilePath);
		}

		[Fact]
		public void LegalDocumentsCreationThrows()
		{
			Assert.Throws<ArgumentException>(() => new LegalDocuments("foo"));
			Assert.Throws<ArgumentException>(() => new LegalDocuments("foo.txt"));
			Assert.Throws<ArgumentException>(() => new LegalDocuments("0.txt"));
			Assert.Throws<ArgumentNullException>(() => new LegalDocuments(null));
			Assert.Throws<ArgumentException>(() => new LegalDocuments(""));
			Assert.Throws<ArgumentException>(() => new LegalDocuments(" "));
		}

		[Fact]
		public void CantLoadNotAgreed()
		{
			var dir = Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.GetCallerFileName());
			if (Directory.Exists(dir))
			{
				Directory.Delete(dir, true);
			}

			var res = LegalDocuments.TryLoadAgreed(dir);
			Assert.Null(res);
			Assert.True(Directory.Exists(dir));
			Assert.Empty(Directory.GetFiles(dir));
			var legalDir = Assert.Single(Directory.GetDirectories(dir));
			Assert.Empty(Directory.GetFiles(legalDir));
			Assert.Empty(Directory.GetDirectories(legalDir));
		}

		[Fact]
		public async Task LeavesTrashAloneAsync()
		{
			var dir = Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.GetCallerFileName());
			if (Directory.Exists(dir))
			{
				Directory.Delete(dir, true);
			}

			var res = LegalDocuments.TryLoadAgreed(dir);
			Assert.Null(res);
			var legalDir = Assert.Single(Directory.GetDirectories(dir));

			// Leaves one trash alone.
			var trash1 = File.Create(Path.Combine(legalDir, "foo"));
			await trash1.DisposeAsync();
			res = LegalDocuments.TryLoadAgreed(dir);
			Assert.Null(res);
			Assert.Single(Directory.GetFiles(legalDir));
			Assert.Empty(Directory.GetDirectories(legalDir));

			// Leaves 3 trash alone.
			var trash2 = File.Create(Path.Combine(legalDir, "foo.txt"));
			var trash3 = File.Create(Path.Combine(legalDir, "foo2"));
			await trash2.DisposeAsync();
			await trash3.DisposeAsync();
			res = LegalDocuments.TryLoadAgreed(dir);
			Assert.Null(res);
			Assert.Equal(3, Directory.GetFiles(legalDir).Length);
			Assert.Empty(Directory.GetDirectories(legalDir));
		}

		[Fact]
		public async Task ResolvesConflictsAsync()
		{
			var dir = Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.GetCallerFileName());
			if (Directory.Exists(dir))
			{
				Directory.Delete(dir, true);
			}

			var res = LegalDocuments.TryLoadAgreed(dir);
			Assert.Null(res);
			var legalDir = Assert.Single(Directory.GetDirectories(dir));

			// Deletes them if multiple legal docs found.
			var candidate1 = File.Create(Path.Combine(legalDir, "1.1.txt"));
			var candidate2 = File.Create(Path.Combine(legalDir, "1.2.txt"));
			await candidate1.DisposeAsync();
			await candidate2.DisposeAsync();
			res = LegalDocuments.TryLoadAgreed(dir);
			Assert.Null(res);
			Assert.Empty(Directory.GetFiles(legalDir));
			Assert.Empty(Directory.GetDirectories(legalDir));

			// Only the candidates are deleted.
			var trash = File.Create(Path.Combine(legalDir, "1.txt"));
			candidate1 = File.Create(Path.Combine(legalDir, "1.1.txt"));
			candidate2 = File.Create(Path.Combine(legalDir, "1.2.txt"));
			await trash.DisposeAsync();
			await candidate1.DisposeAsync();
			await candidate2.DisposeAsync();
			res = LegalDocuments.TryLoadAgreed(dir);
			Assert.Null(res);
			Assert.Single(Directory.GetFiles(legalDir));
			Assert.Empty(Directory.GetDirectories(legalDir));
		}

		[Fact]
		public async Task CanLoadLegalDocsAsync()
		{
			var dir = Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.GetCallerFileName());
			if (Directory.Exists(dir))
			{
				Directory.Delete(dir, true);
			}

			var res = LegalDocuments.TryLoadAgreed(dir);
			Assert.Null(res);
			var legalDir = Assert.Single(Directory.GetDirectories(dir));

			var version = new Version(1, 1);

			// Deletes them if multiple legal docs found.
			var candidate = File.Create(Path.Combine(legalDir, $"{version}.txt"));
			await candidate.DisposeAsync();
			res = LegalDocuments.TryLoadAgreed(dir);
			Assert.NotNull(res);
			Assert.Single(Directory.GetFiles(legalDir));
			Assert.Empty(Directory.GetDirectories(legalDir));
			Assert.Equal(version, res.Version);
			Assert.Equal(Path.Combine(legalDir, $"{version}.txt"), res.FilePath);
		}

		[Fact]
		public async Task CanSerializeFileAsync()
		{
			var dir = Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.GetCallerFileName());
			if (Directory.Exists(dir))
			{
				Directory.Delete(dir, true);
			}

			string filePath = Path.Combine(dir, "1.1.txt");
			var legal = new LegalDocuments(filePath);

			Assert.False(File.Exists(filePath));
			await legal.ToFileAsync("bar");
			Assert.True(File.Exists(filePath));

			string filePath2 = Path.Combine(dir, "1.2.txt");
			var legal2 = new LegalDocuments(filePath2);

			Assert.True(File.Exists(filePath));
			Assert.False(File.Exists(filePath2));
			await legal2.ToFileAsync("bar");
			Assert.False(File.Exists(filePath));
			Assert.True(File.Exists(filePath2));
		}
	}
}
