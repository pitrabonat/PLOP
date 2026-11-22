#import <TargetConditionals.h>
#import <UIKit/UIKit.h>

extern "C" __attribute__((visibility("default"))) void PlopPlayHaptic(int completed)
{
#if !TARGET_OS_SIMULATOR
    dispatch_async(dispatch_get_main_queue(), ^{
        static UIImpactFeedbackGenerator *settle = nil;
        static UIImpactFeedbackGenerator *finish = nil;
        if (completed != 0) {
            if (finish == nil) {
                finish = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
            }
            [finish impactOccurredWithIntensity:0.65];
        } else {
            if (settle == nil) {
                settle = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleSoft];
            }
            [settle impactOccurredWithIntensity:0.48];
        }
    });
#endif
}
