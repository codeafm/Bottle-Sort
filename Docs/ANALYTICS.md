# Аналитика Bottle Sort (Firebase Analytics)

Игра отправляет события в Firebase-проект **bottle-sort-23297**.
Смотреть: https://console.firebase.google.com/project/bottle-sort-23297/analytics

Язык консоли меняется в настройках Google-аккаунта: https://myaccount.google.com/language → «Русский».
Названия событий в Firebase могут быть только латиницей. Зато все значения внутри (экраны, причины, кнопки,
источники монет) отправляются по-русски, и в отчётах вы видите «Главное меню», «Тупик», «Подарок ×2».

Первые данные появляются в разделе **DebugView** сразу, а в обычных отчётах — через несколько часов (до суток).

## Что смотреть каждый день

| Вопрос | Где в консоли |
|---|---|
| Сколько игроков сегодня, за неделю | Analytics → Dashboard (Пользователи, DAU/WAU/MAU) |
| Сколько игроков возвращается | Analytics → Retention (удержание по дням) |
| Сколько заработала реклама | Analytics → Dashboard, карточка «Revenue» / событие `ad_impression` |
| На каком уровне игроки бросают игру | Analytics → Events → `level_start` и `level_complete`, параметр `level` |
| Какие уровни слишком сложные | Events → `level_fail` (параметры `level`, `reason`) и `level_restart` |
| Какие кнопки «за рекламу» нажимают | Events → `rewarded_ad`, параметр `placement` |
| Сколько межстраничной рекламы упустили | Events → `interstitial_ad`, параметр `result` = «Не загрузилась» |
| Откуда приходят и куда уходят монеты | Events → `earn_virtual_currency` / `spend_virtual_currency` |

## Один раз настроить в консоли (5 минут)

Чтобы параметры событий показывались в отчётах, их нужно зарегистрировать:
**Analytics → Custom definitions → Create custom dimension** (область — *Event*).

Зарегистрируйте текстовые параметры: `level_type`, `reason`, `placement`, `result`, `after`, `booster`,
`source`, `item_name`, `screen_name`.
И числовые (**Create custom metric**): `level`, `attempt`, `stars`, `moves`, `seconds`, `reward`.

Свойства игрока (**Custom definitions → область User**): `max_level`, `no_ads`, `game_language`.

## Все события

### Уровни

| Событие | Когда | Параметры |
|---|---|---|
| `tutorial_begin` | Первый запуск первого уровня | — |
| `tutorial_complete` | Пройден последний обучающий уровень (3-й) | — |
| `level_start` | Начало уровня и каждое «Заново» | `level` — номер, `level_type` — Обычный / Особый / Большая колба / Спасение / Босс / Босс главы, `attempt` — какая попытка (1 — первая) |
| `level_complete` | Уровень пройден | `level`, `level_type`, `stars` — звёзды, `moves` — ходы, `seconds` — сколько секунд играл, `reward` — монеты за уровень |
| `level_fail` | Проигрыш | `level`, `level_type`, `reason` — «Кончились ходы» или «Тупик», `moves`, `seconds` |
| `level_restart` | Игрок нажал «Заново» | `level`, `level_type` |
| `booster_use` | Использован бустер | `booster` — «Отмена хода» / «Подсказка» / «+Бутылка», `level` |

### Реклама

| Событие | Когда | Параметры |
|---|---|---|
| `ad_impression` | Каждый показ любой рекламы (баннер, межстраничная, за награду) | `ad_format` — banner / interstitial / rewarded, `ad_source` — сеть (Yandex, VK Ads, Unity Ads), `value` — доход в долларах, `currency` = USD, `precision` — точность оценки дохода |
| `rewarded_ad` | Игрок нажал кнопку «за рекламу» | `placement` — какая кнопка (см. ниже), `result` — «Досмотрел» / «Закрыл раньше» / «Не загрузилась», `level` |
| `interstitial_ad` | Пора показать межстраничную рекламу | `after` — «Победа» / «Проигрыш», `result` — «Показана» / «Не загрузилась», `level` |

Кнопки рекламы за награду (`placement`): «Награда за победу ×2», «Подарок ×2», «Календарь ×2»,
«Монеты за рекламу», «Колесо: попытка за рекламу», «Отмена хода за рекламу», «Подсказка за рекламу»,
«+Бутылка за рекламу», «+5 ходов после проигрыша», «+бутылка в тупике».

Доход в `ad_impression` — оценка самого Яндекса для каждого показа. Точные деньги к выплате — в кабинете РСЯ.

### Монеты

| Событие | Когда | Параметры |
|---|---|---|
| `earn_virtual_currency` | Игрок получил монеты или бустеры | `value` — монеты, `source` — откуда (Победа на уровне, Награда за победу ×2, Ежедневный подарок, Календарь, Колесо удачи, Монеты за рекламу, Сундук, Подарок за обучение, Покупка бустера), `undo` / `hints` / `bottles` / `spins` — сколько бустеров и попыток колеса |
| `spend_virtual_currency` | Игрок потратил монеты | `value` — сколько, `item_name` — на что («Бутылка: long», «Фон: ocean», «Бустер: Подсказка») |

### Экраны

`screen_view` — каждый переход между экранами, `screen_name` по-русски: Заставка, Главное меню,
Карта уровней, Игра, Победа, Магазин, Настройки, Подарок, Награда, Календарь, Колесо удачи.

### Свойства игрока

| Свойство | Что значит |
|---|---|
| `max_level` | Самый дальний открытый уровень |
| `no_ads` | Куплено ли «Без рекламы» (да / нет) |
| `game_language` | Язык игры |

По ним можно делить любые отчёты, например «удержание игроков, дошедших до 100-го уровня».

## Как проверить, что события идут

1. Android: подключите телефон и выполните
   `adb shell setprop debug.firebase.analytics.app com.codeafm.bottlesort`
2. Запустите игру — события появятся в консоли в разделе **Analytics → DebugView** через несколько секунд.
3. Выключить отладку: `adb shell setprop debug.firebase.analytics.app .none.`

В редакторе Unity события не уходят в Firebase, а пишутся в Console с пометкой `[Analytics]`.

## Что нужно для iOS

В проекте пока нет **GoogleService-Info.plist** — без него Firebase на iOS не запустится
(ни аналитика, ни push). Скачать: Firebase → Project settings → Your apps → iOS-приложение
`com.codeafm.bottlesort` (если его нет — Add app → iOS) → **GoogleService-Info.plist**.
Положить в `Assets/GoogleService-Info.plist`. Ничего больше менять не нужно: код сам заработает.

## Где это в коде

- `Assets/_Game/Scripts/Services/Analytics/Analytics.cs` — все события и русские названия.
- `Assets/_Game/Integrations/Firebase/FirebaseAnalyticsBackend.cs` — отправка в Firebase.
- Firebase Analytics Unity SDK 14.0.0 — `Assets/Firebase` (та же версия, что у App и Messaging).
