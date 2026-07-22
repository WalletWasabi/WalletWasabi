#import <UIKit/UIKit.h>

#ifdef __cplusplus
extern "C" {
#endif
	int xamarin_main (int argc, char **argv, int launch_mode);
#ifdef __cplusplus
}
#endif

int main (int argc, char **argv)
{
	@autoreleasepool {
		return xamarin_main (argc, argv, 0);
	}
}
