# SmcManager — отчёт по реализации

## Назначение

Кроссплатформенное приложение для скачивания контента из социальных сетей (сейчас **Instagram**: посты, рилсы, сторис) с сохранением метаданных аккаунта, описания и всех медиафайлов поста. Поддержка **Windows 10–11** и **Android 14+**.

---

## Архитектура решения

| Проект | Назначение |
|--------|------------|
| **SmcManager.Core** | Доменные модели, перечисления, интерфейсы сервисов — без зависимостей от UI |
| **SmcManager.Infrastructure** | SQLite (EF Core), загрузчики, HTTP с прокси, хранение файлов |
| **SmcManager.Maui** | UI (.NET MAUI), MVVM, Share intent, настройки |

Паттерн **MVVM**: View ↔ ViewModel через привязки, бизнес-логика в Core/Infrastructure.

---

## Библиотеки и зачем они используются

| Библиотека | Версия | Назначение |
|------------|--------|------------|
| **.NET MAUI** (`Microsoft.Maui.Controls`) | 10 / $(MauiVersion) | UI для Windows и Android, Shell, вкладки, Share |
| **CommunityToolkit.Mvvm** | 8.4 | `ObservableObject`, `[RelayCommand]`, `[ObservableProperty]`, `WeakReferenceMessenger` для Share-ссылок |
| **Собственные IValueConverter** | — | `InvertedBoolConverter`, `IsNotNullConverter` (CommunityToolkit.Maui пока не совместим с MAUI 10) |
| **Microsoft.EntityFrameworkCore.Sqlite** | 10.0 | Локальная БД: контент, теги, аккаунты |
| **HtmlAgilityPack** | 1.12 | Разбор HTML страницы Instagram (og:meta, JSON-LD) |
| **Microsoft.Extensions.DependencyInjection** | 10.0 | Регистрация сервисов в `MauiProgram` и `DependencyInjection` |

---

## Функциональные блоки

### 1. Скачивание (Instagram)

- **`InstagramUrlParser`** — извлекает shortcode, тип (post/reel/story).
- **`InstagramHtmlParser`** — из HTML достаёт автора, описание, URL видео/фото (в т.ч. карусель через regex + JSON-LD).
- **`InstagramDownloader`** — реализует `IContentDownloader`; при наличии подключённого аккаунта подставляет cookie `sessionid`.
- **`YouTubeDownloader`** / **`VkDownloader`** — парсинг HTML страницы (потоки `streamingData`, ссылки `userapi` / og-теги).
- **`ILinkLauncherService`** — кнопка «Открыть в соцсети» на экране просмотра (системный браузер).
- **`DownloadOrchestrator`** — выбирает загрузчик по URL и запускает сохранение.

### 2. Хранение

- **SQLite** (`AppDbContext`): `ContentItem`, `MediaFile`, `ContentTag`, `SocialAccount`.
- **Файлы** — каталог `FileSystem.AppDataDirectory/downloads/media/{id}/`.
- Стартовые теги: Здоровье, Еда, Спорт, Путешествия, Другое.

### 3. Настройки

- **Аккаунты**: платформа, username, `sessionid` (для Instagram из cookies браузера).
- **Прокси**: хост, порт, логин/пароль → `AppHttpClientFactory` настраивает `WebProxy`.

### 4. UI (вкладки Shell)

| Вкладка | Страница | ViewModel |
|---------|----------|-----------|
| Скачать | `DownloadPage` | `DownloadViewModel` — URL, тег, кнопка, последний результат |
| Контент | `LibraryPage` | `LibraryViewModel` — весь список |
| Группы | `GroupsPage` | `GroupsViewModel` — фильтр по тегам |
| Flyout «Настройки» | `SettingsPage` | `SettingsViewModel` |

Тёмная тема: фон `#0D0D12`, акценты в стиле Instagram (`#E1306C`, `#833AB4`).

### 5. Share / «Отправить»

- **Android**: `IntentFilter` `ACTION_SEND` + `text/plain` в `MainActivity` → `ShareLinkService` → `ShareUrlReceivedMessage` / сохранение в Preferences.
- При открытии вкладки «Скачать» подставляется отложенная ссылка.

---

## Ограничения и дальнейшее развитие

1. **Instagram** не имеет официального публичного API для скачивания чужих постов. Парсинг HTML может перестать работать при изменении вёрстки; для приватного контента нужен валидный **sessionid**.
2. **YouTube / VK** — базовая загрузка через HTML; при блокировках или приватном контенте может понадобиться прокси или авторизация. Для YouTube иногда отдаётся только видео без отдельной дорожки аудио (adaptive).
3. **Stories** — поддерживается разбор URL; для некоторых сторис может требоваться авторизация.
4. Рекомендуется позже: WebView-логин вместо ручного ввода `sessionid`, шифрование cookies, детальный экран поста, редактирование тегов.

---

## Сборка и запуск

```bash
cd "C:\SHILY PROJECTS\projects\SmcManager"
dotnet build SmcManager.sln
```

- Windows: `dotnet build -f net10.0-windows10.0.19041.0`
- Android: `dotnet build -f net10.0-android` (SDK 34+)

---

## Структура каталогов (основное)

```
src/SmcManager.Core/          — Models, Enums, Interfaces
src/SmcManager.Infrastructure/ — Data, Download, Services
src/SmcManager.Maui/           — Views, ViewModels, Platforms
docs/REPORT.md                 — этот файл
```

Во всём коде добавлены XML **`summary`** для классов и ключевых членов.
