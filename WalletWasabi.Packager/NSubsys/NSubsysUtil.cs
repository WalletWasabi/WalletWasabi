using System;
using System.IO;
using System.Runtime.InteropServices;
using static NSubsys.PeUtility;

// https://github.com/jmacato/NSubsys
namespace NSubsys
{
	public static class NSubsysUtil
	{
		/// <summary>
		/// Makes the exe not to open console.
		/// </summary>
		public static bool ProcessFile(string exeFilePath)
		{
			Console.WriteLine("NSubsys: Subsystem Changer for Windows PE files.");
			Console.WriteLine($"NSubsys: Target EXE {exeFilePath}.");

			using (var peFile = new PeUtility(exeFilePath))
			{
				SubSystemType subsysVal;
				var subsysOffset = peFile.MainHeaderOffset;
				var headerType = peFile.Is32BitHeader ? typeof(IMAGE_OPTIONAL_HEADER32) : typeof(IMAGE_OPTIONAL_HEADER64);

				if (peFile.Is32BitHeader)
				{
					subsysVal = (SubSystemType)peFile.OptionalHeader32.Subsystem;
				}
				else
				{
					subsysVal = (SubSystemType)peFile.OptionalHeader64.Subsystem;
				}

				subsysOffset += Marshal.OffsetOf(headerType, "Subsystem").ToInt32();

				switch (subsysVal)
				{
					case SubSystemType.IMAGE_SUBSYSTEM_WINDOWS_GUI:
						Console.WriteLine("NSubsys: Executable file is already a Win32 App!");
						return true;

					case SubSystemType.IMAGE_SUBSYSTEM_WINDOWS_CUI:
						Console.WriteLine("NSubsys: Console app detected...");
						Console.WriteLine("NSubsys: Converting...");

						var subsysSetting = BitConverter.GetBytes((ushort)SubSystemType.IMAGE_SUBSYSTEM_WINDOWS_GUI);

						if (!BitConverter.IsLittleEndian)
						{
							Array.Reverse(subsysSetting);
						}

						if (peFile.Stream.CanWrite)
						{
							peFile.Stream.Seek(subsysOffset, SeekOrigin.Begin);
							peFile.Stream.Write(subsysSetting, 0, subsysSetting.Length);
							Console.WriteLine("NSubsys: Conversion Complete...");
						}
						else
						{
							Console.WriteLine("NSubsys: Can't write changes!");
							Console.WriteLine("NSubsys: Conversion Failed...");
						}

						return true;

					default:
						Console.WriteLine($"NSubsys: Unsupported subsystem : {Enum.GetName(typeof(SubSystemType), subsysVal)}.");
						return false;
				}
			}
		}
	}
}
