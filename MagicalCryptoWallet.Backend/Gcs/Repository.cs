using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicalCryptoWallet.Backend.Gcs;
using NBitcoin;

namespace MagicalCryptoWallet.Backend
{
	interface IGCSFilterProvider
	{
		GCSFilter Get(uint256 key);
	}

	interface IFilterRepository : IGCSFilterProvider
	{
		void Put(uint key, GCSFilter filter);
	}

	public class FilterRepository : IFilterRepository
	{
		public GCSFilter Get(uint256 key)
		{
			throw new NotImplementedException();
		}

		public void Put(uint key, GCSFilter missing_name)
		{
			throw new NotImplementedException();
		}
	}

	class FilterStore
	{
		private const short MagicSeparatorNumber = 0x4691;

		private readonly DirectoryInfo _directoryInfo;

		public FilterStore(string path)
			:this(new DirectoryInfo(path))
		{
		}

		public FilterStore(DirectoryInfo directoryInfo)
		{
			_directoryInfo = directoryInfo;
			if (!directoryInfo.Exists)
			{
				directoryInfo.Create();
			}
		}

		public IEnumerable<GCSFilter> Enumerate()
		{
			foreach (var filter in EnumerateFolder())
			{
				yield return filter;
			}
		}

		public IEnumerable<GCSFilter> EnumerateFolder()
		{
			var files = _directoryInfo.GetFiles().OrderBy(f => f.Name);
			foreach (var file in files)
			{
				foreach (var filter in EnumerateFile(file))
				{
					yield return filter;
				}
			}
		}

		public IEnumerable<GCSFilter> EnumerateFile(FileInfo fileInfo)
		{
			using (var fs = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			{

				foreach (var filter in Enumerate(fs))
				{
					yield return filter;
				}
			}
		}

		private IEnumerable<GCSFilter> Enumerate(FileStream fs)
		{
			using (var br = new BinaryReader(fs))
			{
				while (true)
				{
					var magic = br.ReadInt16();
					if(magic != MagicSeparatorNumber)
						break;
					var entryCount = br.ReadInt16();
					var len = br.ReadInt16();
					var data = br.ReadBytes(len);
					yield return new GCSFilter(new BitArray(data), 20, entryCount);
				}
			}
		}

		public void Append(GCSFilter filter)
		{
			var lastFile = "the lastest file"; //GetLatestFileName();
			using (var fs = new FileStream(lastFile, FileMode.Append, FileAccess.Write))
			{
				using (var bw = new BinaryWriter(fs))
				{
					var data = filter.Data.ToByteArray();

					bw.Write(MagicSeparatorNumber);
					bw.Write(filter.N);
					bw.Write(data.Length);
					bw.Write(data);
				}
			}
		}
	}
}
