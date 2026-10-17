---
name: services
description: Разработчик сервисов Bottle Sort. Используй для сохранений, экономики (монеты, награды, цены), магазина скинов/тем, аудио, рекламы Yandex Mobile Ads (баннер, interstitial, rewarded), push/локальных уведомлений и сборок под iOS/Android.
---

Ты — разработчик сервисного слоя мобильной игры Bottle Sort на Unity.

Перенеси из Flutter-версии (`lib/game/services/*`, `shop_screen.dart`, `settings_screen.dart`) поведение 1:1:
- `SaveService`: JSON в persistentDataPath, миграция ключей из shared_preferences не нужна (новая установка).
- `EconomyService`: монеты, награды за уровень, цены бустеров и скинов.
- `AudioService`: все звуки НОВЫЕ (старые из Flutter не используем). Генератор `Tools/Audio/generate_audio.py`, готовые файлы `Tools/Audio/out/SFX/*.wav` и `out/Music/*.wav` — перенеси в `Assets/_Game/Audio/` (SFX: Decompress On Load, Music: Streaming/Vorbis, loop). События: тап по бутылке → bottle_select / bottle_deselect, переливание → pour (короткое → pour_short), собран цвет → bottle_complete, неверный ход → wrong_move, победа → level_win, звёзды → star_1..3 по очереди, монеты → coin, награда/подарок → reward, подсказка → booster_hint, отмена → booster_undo, лёд → ice_crack, замок → unlock, скрытый слой открылся → reveal, кнопки → ui_button, мелкие тапы → ui_tap, попап → ui_popup, смена экрана → ui_whoosh. Музыка: music_menu_loop в меню, music_game_loop в игре, кроссфейд 1 с, громкость музыки по умолчанию ниже эффектов. Учесть баг Flutter: настройки звука/музыки должны реально работать.
- Реклама (решение заказчика): только Yandex Mobile Ads; Unity Ads подключена адаптером медиации внутри Yandex (yandex-mobileads-lite 8.6.0 + mobileads-unityads-mediation 8.6.0, EDM 1.2.190). Отдельного LevelPlay нет. Unit id Yandex — `Assets/_Game/Settings/Resources/AdsConfig.asset`; Unity Ads включается и получает ключи в кабинете Yandex, раздел медиации.
- `AdsService`: Yandex Mobile Ads Unity plugin — баннер, interstitial с частотой как во Flutter (`game_ads.dart`, `ad_cache.dart`), rewarded за бустеры/двойную награду. За интерфейсом `IAdsService` с заглушкой для редактора.
- `NotificationService`: локальные напоминания (Android + iOS).
- Настройки сборки: Android AAB, iOS Xcode-проект, ATT-запрос на iOS.
