using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NSubsys
{
	internal class PeUtility : IDisposable
	{
		public enum SubSystemType : UInt16
		{
			IMAGE_SUBSYSTEM_WINDOWS_GUI = 2,
			IMAGE_SUBSYSTEM_WINDOWS_CUI = 3,
		}

		[StructLayout(LayoutKind.Explicit)]
		public struct IMAGE_DOS_HEADER
		{
			[FieldOffset(60)]
			public UInt32 e_lfanew;
		}

		[StructLayout(LayoutKind.Explicit)]
		public struct IMAGE_OPTIONAL_HEADER
		{
			[FieldOffset(68)]
			public UInt16 Subsystem;
		}

		private readonly long fileHeaderOffset;
		private IMAGE_OPTIONAL_HEADER optionalHeader;
		private readonly FileStream curFileStream;

		public PeUtility(string filePath)
		{
			curFileStream = new FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite);
			
			using (var reader = new BinaryReader(curFileStream))
			{
				var dosHeader = FromBinaryReader<IMAGE_DOS_HEADER>(reader);

				// Seek the new PE Header and skip NtHeadersSignature (4 bytes) & IMAGE_FILE_HEADER struct (20bytes).
				curFileStream.Seek(dosHeader.e_lfanew + 4 + 20, SeekOrigin.Begin);

				fileHeaderOffset = curFileStream.Position;
				optionalHeader = FromBinaryReader<IMAGE_OPTIONAL_HEADER>(reader);
			}
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
			curFileStream?.Dispose();
		}

		/// <summary>
		/// Gets the optional header
		/// </summary>
		public IMAGE_OPTIONAL_HEADER OptionalHeader
		{
			get => optionalHeader;
		}

		/// <summary>
		/// Gets the PE file stream for R/W functions.
		/// </summary> 
		public FileStream Stream
		{
			get => curFileStream;
		}

		public long MainHeaderOffset
		{
			get => fileHeaderOffset;
		}
	}
}