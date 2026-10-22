#import <Foundation/Foundation.h>
#import <AppTrackingTransparency/AppTrackingTransparency.h>

// Запрос App Tracking Transparency для TrackingConsent.cs. Ответ C# узнаёт опросом статуса,
// так проще, чем гонять колбэк через границу Objective-C и IL2CPP.
extern "C" {
    // Как ATTrackingManagerAuthorizationStatus: 0 — ещё не спрашивали, 1 — запрещено системой,
    // 2 — отказ, 3 — разрешено. До iOS 14 спрашивать не нужно — считаем, что разрешено.
    int BSTrackingStatus() {
        if (@available(iOS 14, *)) {
            return (int)[ATTrackingManager trackingAuthorizationStatus];
        }
        return 3;
    }

    void BSRequestTracking() {
        if (@available(iOS 14, *)) {
            [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) {}];
        }
    }
}
