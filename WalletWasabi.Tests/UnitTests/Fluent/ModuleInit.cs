using System.Runtime.CompilerServices;
using VerifyTests;

namespace WalletWasabi.Tests.UnitTests.Fluent;

public static class ModuleInit
{
    [ModuleInitializer]
    public static void InitOther()
    {
        VerifierSettings.InitializePlugins();
        VerifyImageMagick.RegisterComparers(0.04);
        VerifierSettings.UniqueForOSPlatform();
    }
}
