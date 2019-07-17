using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static NSubsys.PeUtility;

// https://github.com/jmacato/NSubsys
namespace NSubsys
{
	public static class NSubSysUtil
	{
		/// <summary>
		/// Makes the exe not to open console.
		/// </summary>
		public static bool ProcessFile(string exeFilePath)
		{
			Console.WriteLine("NSubSys: Subsystem Changer for Windows PE files.");
			Console.WriteLine($"NSubSys: Target EXE {exeFilePath}.");
			using (var peFile = new PeUtility(exeFilePath))
			{
				SubSystemType subSysVal;
				var subSysOffset = peFile.MainHeaderOffset;

				subSysVal = (SubSystemType)peFile.OptionalHeader.SubSystem;
				subSysOffset += Marshal.OffsetOf<IMAGE_OPTIONAL_HEADER>(nameof(IMAGE_OPTIONAL_HEADER.SubSystem))
									   .ToInt32();

				switch (subSysVal)
				{
					case PeUtility.SubSystemType.IMAGE_SUBSYSTEM_WINDOWS_GUI:
						Console.WriteLine("NSubSys: Executable file is already a Win32 App!");
						return true;
					case PeUtility.SubSystemType.IMAGE_SUBSYSTEM_WINDOWS_CUI:
						Console.WriteLine("NSubSys: Console app detected...");
						Console.WriteLine("NSubSys: Converting...");

						var subSysSetting = BitConverter.GetBytes((ushort)SubSystemType.IMAGE_SUBSYSTEM_WINDOWS_GUI);

						if (!BitConverter.IsLittleEndian)
							Array.Reverse(subSysSetting);

						if (peFile.Stream.CanWrite)
						{
							peFile.Stream.Seek(subSysOffset, SeekOrigin.Begin);
							peFile.Stream.Write(subSysSetting, 0, subSysSetting.Length);
							Console.WriteLine("NSubSys: Conversion Complete...");
						}
						else
						{
							Console.WriteLine("NSubSys: Can't write changes!");
							Console.WriteLine("NSubSys: Conversion Failed...");
						}

						return true;
					default:
						Console.WriteLine($"NSubSys: Unsupported subsystem number: {subSysVal}.");
						return false;
				}
			}
		}
	}
}