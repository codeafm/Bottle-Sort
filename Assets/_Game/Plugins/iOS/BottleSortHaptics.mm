#import <UIKit/UIKit.h>

// Стиль совпадает с HapticStrength в Haptics.cs: 0 — лёгкий, 1 — средний, 2 — сильный.
extern "C" void BottleSortImpact(int style)
{
    UIImpactFeedbackStyle feedbackStyle = UIImpactFeedbackStyleLight;
    if (style == 1)
        feedbackStyle = UIImpactFeedbackStyleMedium;
    else if (style >= 2)
        feedbackStyle = UIImpactFeedbackStyleHeavy;

    UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:feedbackStyle];
    [generator prepare];
    [generator impactOccurred];
}

// 0 — успех, 1 — предупреждение, 2 — ошибка.
extern "C" void BottleSortNotify(int type)
{
    UINotificationFeedbackType feedbackType = UINotificationFeedbackTypeSuccess;
    if (type == 1)
        feedbackType = UINotificationFeedbackTypeWarning;
    else if (type >= 2)
        feedbackType = UINotificationFeedbackTypeError;

    UINotificationFeedbackGenerator *generator = [[UINotificationFeedbackGenerator alloc] init];
    [generator prepare];
    [generator notificationOccurred:feedbackType];
}
