using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NSubsys
{
	internal class PeUtility : IDisposable
	{
		public enum SubSystemType : ushort
		{
			ImageSubSystemWindowsGui = 2,
			ImageSubSystemWindowsCui = 3
		}

		[StructLayout(LayoutKind.Explicit)]
		public struct ImageDosHeader
		{
			[FieldOffset(60)]
			private uint _fileAddressNew;

			public uint FileAddressNew => _fileAddressNew;
		}

		[StructLayout(LayoutKind.Explicit)]
		public struct ImageOptionalHeader
		{
			[FieldOffset(68)]
			private ushort _subsystem;

			public ushort Subsystem => _subsystem;
		}

		/// <summary>
		/// Gets the optional header
		/// </summary>
		public ImageOptionalHeader OptionalHeader { get; }

		/// <summary>
		/// Gets the PE file stream for R/W functions.
		/// </summary>
		public FileStream Stream { get; }

		public long MainHeaderOffset { get; }

		private readonly IDisposable InternalBinReader;

		public PeUtility(string filePath)
		{
			Stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite);

			var reader = new BinaryReader(Stream);

			var dosHeader = FromBinaryReader<ImageDosHeader>(reader);

			// Seek the new PE Header and skip NtHeadersSignature (4 bytes) & IMAGE_FILE_HEADER struct (20bytes).
			Stream.Seek(dosHeader.FileAddressNew + 4 + 20, SeekOrigin.Begin);

			MainHeaderOffset = Stream.Position;

			OptionalHeader = FromBinaryReader<ImageOptionalHeader>(reader);

			InternalBinReader = reader;
		}

		/// <summary>
		/// Reads in a block from a file and converts it to the struct
		/// type specified by the template parameter
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="reader"></param>
		/// <returns></returns>
		public static T FromBinaryReader<T>(BinaryReader reader)
		{
			// Read in a byte array
			var bytes = reader.ReadBytes(Marshal.SizeOf<T>());

			// Pin the managed memory while, copy it out the data, then unpin it
			var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
			var theStructure = Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
			handle.Free();

			return theStructure;
		}

		public void Dispose()
		{
			Stream?.Dispose();
			InternalBinReader?.Dispose();
		}
	}
}
