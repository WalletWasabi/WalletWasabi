using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
			Console.WriteLine($"{nameof(NSubsys)}: Subsystem Changer for Windows PE files.");
			Console.WriteLine($"{nameof(NSubsys)}: Target EXE {exeFilePath}.");
			using (var peFile = new PeUtility(exeFilePath))
			{
				SubSystemType subsysVal;
				var subsysOffset = peFile.MainHeaderOffset;

				subsysVal = (SubSystemType)peFile.OptionalHeader.Subsystem;
				subsysOffset += Marshal.OffsetOf<ImageOptionalHeader>(nameof(ImageOptionalHeader.Subsystem)).ToInt32();

				switch (subsysVal)
				{
					case PeUtility.SubSystemType.ImageSubSystemWindowsGui:
						Console.WriteLine($"{nameof(NSubsys)}: Executable file is already a Win32 App!");
						return true;

					case PeUtility.SubSystemType.ImageSubSystemWindowsCui:
						Console.WriteLine($"{nameof(NSubsys)}: Console app detected...");
						Console.WriteLine($"{nameof(NSubsys)}: Converting...");

						var subsysSetting = BitConverter.GetBytes((ushort)SubSystemType.ImageSubSystemWindowsGui);

						if (!BitConverter.IsLittleEndian)
						{
							Array.Reverse(subsysSetting);
						}

						if (peFile.Stream.CanWrite)
						{
							peFile.Stream.Seek(subsysOffset, SeekOrigin.Begin);
							peFile.Stream.Write(subsysSetting, 0, subsysSetting.Length);
							Console.WriteLine($"{nameof(NSubsys)}: Conversion Complete...");
						}
						else
						{
							Console.WriteLine($"{nameof(NSubsys)}: Can't write changes!");
							Console.WriteLine($"{nameof(NSubsys)}: Conversion Failed...");
						}

						return true;

					default:
						Console.WriteLine($"{nameof(NSubsys)}: Unsupported subsystem number: {subsysVal}.");
						return false;
				}
			}
		}
	}
}
