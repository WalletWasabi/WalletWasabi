#import <UIKit/UIKit.h>

#ifdef __cplusplus
extern "C" {
#endif
	void xamarin_setup (void);
	int xamarin_main (int argc, char **argv, int launch_mode);
#ifdef __cplusplus
}
#endif

int main (int argc, char **argv)
{
	@autoreleasepool {
		xamarin_setup ();
		return xamarin_main (argc, argv, 0);
	}
}
