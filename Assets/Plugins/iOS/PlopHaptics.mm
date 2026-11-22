#import <TargetConditionals.h>
#import <UIKit/UIKit.h>

extern "C" __attribute__((visibility("default"))) void PlopPlayHaptic(int cue)
{
#if !TARGET_OS_SIMULATOR
    dispatch_async(dispatch_get_main_queue(), ^{
        if (UIApplication.sharedApplication.applicationState != UIApplicationStateActive) return;

        static UIImpactFeedbackGenerator *soft = nil;
        static UIImpactFeedbackGenerator *finish = nil;
        if (cue == 4) {
            if (finish == nil) {
                finish = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
            }
            [finish impactOccurredWithIntensity:0.62];
        } else {
            if (soft == nil) {
                soft = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleSoft];
            }
            CGFloat intensity = cue == 3 ? 0.46 : cue == 2 ? 0.31 : 0.23;
            [soft impactOccurredWithIntensity:intensity];
        }
    });
#endif
}
