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
			IMAGE_SUBSYSTEM_WINDOWS_GUI = 2,
			IMAGE_SUBSYSTEM_WINDOWS_CUI = 3,
		}

		[StructLayout(LayoutKind.Explicit)]
		public struct IMAGE_DOS_HEADER
		{
			[FieldOffset(60)]
			public uint e_lfanew;
		}

		[StructLayout(LayoutKind.Explicit)]
		public struct IMAGE_OPTIONAL_HEADER
		{
			[FieldOffset(68)]
			public ushort Subsystem;
		}


		/// <summary>
		/// Gets the optional header
		/// </summary>
		public IMAGE_OPTIONAL_HEADER OptionalHeader { get;  }

		/// <summary>
		/// Gets the PE file stream for R/W functions.
		/// </summary> 
		public FileStream Stream { get; }

		public long MainHeaderOffset { get; }

		private readonly IDisposable _internalBinReader;

		public PeUtility(string filePath)
		{
			Stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite);

			var reader = new BinaryReader(Stream);

			var dosHeader = FromBinaryReader<IMAGE_DOS_HEADER>(reader);

			// Seek the new PE Header and skip NtHeadersSignature (4 bytes) & IMAGE_FILE_HEADER struct (20bytes).
			Stream.Seek(dosHeader.e_lfanew + 4 + 20, SeekOrigin.Begin);

			MainHeaderOffset = Stream.Position;

			OptionalHeader = FromBinaryReader<IMAGE_OPTIONAL_HEADER>(reader);

			_internalBinReader = reader;
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
			_internalBinReader?.Dispose();
		}
	}
}