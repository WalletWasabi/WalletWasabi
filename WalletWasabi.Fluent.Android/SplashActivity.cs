using Android.App;
using Android.Content;
using Avalonia;
using Avalonia.Android;
using Avalonia.Fonts.Inter;
using Application = Android.App.Application;

namespace WalletWasabi.Fluent.Android;

[Activity(Theme = "@style/MyTheme.Splash", MainLauncher = true, NoHistory = true)]
public class SplashActivity : AvaloniaSplashActivity<App>
{
	protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .AfterSetup(_ =>
            {
	            // TODO;
            });
    }

    protected override void OnResume()
    {
        base.OnResume();

        StartActivity(new Intent(Application.Context, typeof(MainActivity)));
        Finish();
    }
}
