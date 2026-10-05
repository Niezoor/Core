# Core (`com.niezor.core`)

Wspólna paczka Unity (6000.0+) używana przez wszystkie gry autora. Gry podpinają ją lokalnie, np.
`SpaceRoguelike/Packages/manifest.json`: `"com.niezor.core": "file:../../Core"`. Repo to sama paczka, nie projekt
Unity — kompilacja i testy odbywają się w projekcie gry, który ją referuje.

## Założenia

- Core ma zawierać wszystkie kluczowe systemy gry: zapis/wczytywanie, autoryzację, ustawienia, narzędzia itp.
- Musi być używalna w różnych grach — zero kodu specyficznego dla jednej gry; rozszerzanie przez interfejsy,
  rejestrację i eventy, nie przez edycję Core.
- Platformy docelowe: **WebGL (itch.io), Steam, Android, iOS**. Każdy nowy system musi działać na wszystkich czterech:
  - WebGL: brak wątków (tylko main thread / `Awaitable`), `persistentDataPath` to IndexedDB, domyślnie PlayerPrefs;
  - Android/iOS: aplikacja może zostać ubita po pauzie — zapisywać w `OnApplicationPause`/`OnApplicationFocus`;
  - zależności platformowe (Steamworks, Google Play Games, Game Center itd.) trzymać w osobnych assembly z
    `defineConstraints`/`versionDefines`, żeby rdzeń kompilował się bez nich.

## Struktura

Każdy moduł ma własne `.asmdef` (namespace = nazwa assembly).

| Assembly | Ścieżka | Zawartość |
|---|---|---|
| `Core.Utilities` | `Runtime/Utilities` | singletony, ustawienia `SettingsAsset` (opis niżej), eventy na SO, `UpdateManager`/`TimeCache`, rozszerzenia |
| `Core.SaveSystem` | `Runtime/SaveSystem` | **nowy** system zapisu (opis niżej) |
| `Core.Save` | `Runtime/Save` | **stary** system zapisu, `[Obsolete]` — zamrożony, bez żadnych zmian; nowy kod używa `Core.SaveSystem` |
| `Core.Bootstrap` | `Runtime/Bootstrap` | start gry: `Boot`, `BootSettings`, kroki startowe (opis niżej) |
| `Core.Pooling` | `Runtime/Pooling` | pule obiektów (+ `Editor/PoolDebugWindow`) |
| `Core.UI` | `Runtime/UI` | `UIManager`, `UICanvas`, `UIPanel`, safe area, opcje ustawień (`Settings/Options`) |
| `Core.InputSystemExtension` | `Runtime/InputSystemExtension` | `InputManager`, touch gamepad, on-screen stick/tap |
| `Core.Editor` | `Editor` | narzędzia edytora: TimeTracker, Finder, ProjectSetup, podgląd ikon, Boot, ustawienia (`Editor/Settings`) |
| `Core.SaveSystem.Tests` | `Tests/Runtime/SaveSystem` | testy PlayMode nowego zapisu |
| `Core.Utilities.Tests` | `Tests/Runtime/Utilities` | testy PlayMode `Core.Utilities` (`Notices`, `SettingsRegistry`) |
| `Core.Bootstrap.Tests` | `Tests/Runtime/Bootstrap` | testy PlayMode `Boot` |

Zależności spoza `package.json` (muszą być w projekcie gry): Input System, TextMeshPro, Odin Inspector (`Sirenix`,
używany przez stary `Core.Save`, `Core.UI` i InputSystemExtension). Nowy kod nie powinien wprowadzać zależności od Odina.

## Ustawienia (`Core.Utilities.Settings`)

`SettingsAsset<T>` — jeden asset na typ, szukany **po typie** (`AssetDatabase.FindAssets`), więc plik można dowolnie
przenosić i zmieniać mu nazwę. `T.Instance` / `T.TryGet(out t)` (to drugie nigdy nie tworzy assetu i nie rzuca).

- Domyślnie **async**: w buildzie w Addressables pod labelem `Settings` (`SettingsRegistry.AddressablesLabel`),
  ładowane przez `SettingsRegistry.LoadAllAsync()` — w Boot robi to splash task `LoadSettingsTask`. Odczyt przed
  załadowaniem rzuca `InvalidOperationException` z opisem, co zrobić.
- `[PreloadedSettings]` — w Player Settings › Preloaded Assets, w pamięci od startu (czytelne w `BeforeSceneLoad`,
  np. `BootSettings`, `InputSystemSettings`). Wszystko, do czego się odwołuje, ładuje się razem z nim.
- W edytorze każdy typ jest dostępny synchronicznie (`SettingsRegistry.EditorResolver` z `Editor/Settings`), asset
  powstaje przy pierwszym `Instance` albo otwarciu strony w Project Settings — w `Assets/Settings` lub folderze z
  `[SettingsPath]`, zawsze pod unikalną nazwą (nigdy nie nadpisuje pliku). W Play Mode edytor loguje błąd przy
  odczycie, który w buildzie by rzucił: async w initializerach Boot (`SettingsRegistry.PreloadedOnly`), w trakcie
  `LoadAllAsync` albo typ, którego `LoadAllAsync` nie załadował.
- `SettingsAssetSync` (po kompilacji, przy utworzeniu assetu, przed buildem — `BuildPlayerProcessor` przed
  Addressables — i z menu `Core/Settings/Sync Settings Assets`) trzyma asset tylko w jednym miejscu: Preloaded Assets
  albo wpis Addressables (nowy w grupie `Settings`, adres = pełna nazwa typu). Duplikat: używany jest ten już
  zarejestrowany, reszta jest wyrejestrowana z ostrzeżeniem. Przed buildem ostrzega o referencjach dających drugą
  kopię (preloaded ← treść Addressables, async ← sceny buildu / preloaded) i o ciężkich zależnościach preloaded.
- `[SettingsMenu("Game/Xyz")]` — strona w Project Settings (zamiast ręcznego `[SettingsProvider]`).
- Stare `ScriptableObjectSettings<T>` i `ScriptableObjectPreloadedSettings<T>` są `[Obsolete]` (to drugie zostaje dla
  `Core.Save`).

## Core.SaveSystem

- `Save` — statyczna fasada dla gry: `Set/TryGet/GetOrDefault/Remove`, `Flush`, `Clear`, eventy
  (`BeforeFlush`, `Flushed`, `Replaced`, `ErrorRaised`…). Ładuje się synchronicznie przy pierwszym użyciu, autosave po
  `AutoSaveDelay` (max `AutoSaveMaxDelay`), zapis przy pauzie/utracie fokusu/wyjściu. Backend zmienia się przez
  `Save.Configure(...)` przed pierwszym użyciem (`RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`).
- `SaveStore` — zwykły obiekt (testowalny bez sceny): wpisy JSON pod kluczami, format pliku v2
  (`SaveStore.FormatVersion`), `SnapshotId`/`Revision` do synchronizacji. Zasada nadrzędna: **zapisu, którego nie dało
  się czysto odczytać, nigdy nie nadpisujemy** — uszkodzony plik idzie do kwarantanny, niedostępny → tryb read-only.
- Serializacja przez `JsonUtility` — typy wpisów muszą być `[Serializable]`. Wersjonowanie typów:
  `[SaveVersion(n)]` + `ISaveMigration`.
- Backendy (`ISaveBackend`, zapis musi być atomowy z backupem): `FileSaveBackend` (domyślny; w edytorze osobny
  `save_editor.json`), `PlayerPrefsSaveBackend` (domyślny na WebGL).
- `Cloud/`: `CloudSave` (statyczna fasada, automatyczny sync z back-offem, rejestracja providera z priorytetem,
  `Status`/`StatusChanged` dla ikonki w menu: Disabled/Loading/Synced/Offline/Error/Conflict),
  `CloudSync` (logika porównania snapshotów; lokalny zapis jest źródłem prawdy, konflikt zatrzymuje sync do
  `ResolveConflictAsync`), `ICloudSaveProvider` (wszystkie wywołania na main thread, błędy rzucane jako wyjątki),
  `FakeCloudProvider` do testów. Prawdziwych providerów jeszcze nie ma.

## Core.Bootstrap

`Boot` startuje grę według `BootSettings` (Project Settings → Core → Boot; preloaded asset — bez niego bootstrap
nic nie robi). Trzy fazy, każda to lista `[SerializeReference, SubclassPicker]` — gra dodaje własne kroki, pisząc
klasę `[Serializable]`, bez zmian w Core:

1. `BootInitializer` — synchronicznie w `BeforeSceneLoad`, **w każdej scenie** (też gameplay w edytorze). Tylko rzeczy
   szybkie, lokalne i synchroniczne na każdej platformie (lokalny zapis, ustawienia); suma ponad
   `InitializerBudgetMs` → ostrzeżenie. Np. `LoadSaveInitializer` (w `Core.SaveSystem`, wrzuca `Notices` przy
   uszkodzonym / tylko-do-odczytu zapisie).
2. `BootService` — start w tle, nikt na nie nie czeka (auth, cloud, sklep, reklamy). Stan wystawiają same.
3. `SplashTask` — asynchronicznie, **tylko w scenie z komponentem `BootScene`** (startuje je jego `Awake`), po kolei; `Required` (błąd → `SplashState.Failed`
   + `Boot.Retry()` wznawia od tego kroku) albo opcjonalne (log i dalej), `Timeout` liczony co klatkę. Ostatni,
   niejawny krok wczytuje `FirstScene` bez aktywacji. Gotowe: `InitializeAddressablesTask`, `LoadSettingsTask`
   (ustawienia async — inspektor `BootSettings` ostrzega, gdy go brakuje), `PreloadAddressablesLabelTask`.
   Initializery i synchroniczny start usług widzą tylko ustawienia `[PreloadedSettings]`.

Scena startowa należy do gry (Animator, przycisk na cały ekran wołający `Boot.Continue()`, opcjonalnie
`BootProgressView` z `Core.UI`) + znacznik `BootScene`. `BootSceneSetup` (`Editor/Boot`) pilnuje, żeby była pierwsza
w Build Settings — tylko gdy projekt ma `BootSettings`: przed Play (naprawia, nie blokuje), przed buildem (przerywa
build, jeśli musiał coś zmienić — lista scen buildu jest już ustalona) i z menu `Core/Boot/Setup Boot Scene`.
Kolejność: scena 0 ze znacznikiem → inna scena ze znacznikiem na początek → scena `Boot`/`Launcher`/`Loader` bez
znacznika tylko po potwierdzeniu w okienku → kopia szablonu `Editor/Boot/BootSceneTemplate.unity` do
`Assets/Scenes/Boot.unity` (kopia, bo Unity nie tworzy sceny addytywnie przy otwartej niezapisanej scenie).
Znacznik wykrywany po GUID skryptu `BootScene` w pliku `.unity` (stały GUID w `BootScene.cs.meta` — nie zmieniać,
szablon go używa). Wyjście ze splasha: `Boot.Continue()` (od razu, gdy
gotowe; tap w trakcie ładowania jest zapamiętany) albo samo po `MinSplashDuration`, jeśli `AutoContinue`.
Diagnostyka: `Boot.Records` (każdy krok: faza, czas, wynik, postęp, błąd) i `SplashStartedAt/ReadyAt/LeftAt`;
okno `Core/Boot/Boot Monitor` pokazuje je na żywo i zostawia ostatni przebieg do następnego Play. Logi `[Boot]`:
start, koniec inicjalizacji (ms), koniec ładowania splasha (czasy kroków), wyjście ze splasha (czas od uruchomienia).
`Boot.Restart()` → event `Restarting` (sprzątanie gry) → scena startowa i splash od nowa; initializery i usługi nie startują
ponownie. Kod gry nie może zakładać, że splash się odbył.

## Powiadomienia (`Core.Utilities.Notifications`)

Globalna kolejka komunikatów dla gracza. Systemy wrzucają `Notice` przez `Notices.Post` w dowolnym momencie (także
przed pierwszą sceną); gra pobiera je `Notices.TryTake(out notice, filter)`, kiedy jest gotowa je pokazać. Core niczego
nie wyświetla.

- `NoticeSeverity` (Info/Warning/Error) — jak poważne; `NoticeResponse` — czego wymaga od gracza:
  `None` (toast, opcjonalne akcje), `Acknowledge` (okno z OK, bez akcji), `Choice` (≥ 2 akcje, nie da się zamknąć).
- `TryTake` zdejmuje `None`/`Acknowledge`; `Choice` zostaje w `Pending` jako `IsTaken`, dopóki `Notices.Run` nie
  wykona akcji albo `Notices.Release` jej nie odda — pytanie nie ginie, gdy widok zniknie bez decyzji.
- `Id` deduplikuje (nowy wpis z tym samym Id zastępuje stary), `Notices.Dismiss(id)` wycofuje nieaktualny komunikat.
- Tekst to klucz lokalizacji + `FallbackText` (format z `Args`) — tłumaczy widok gry. Tylko w pamięci, bez zapisu.

## Konwencje

- Domain reload przy wejściu w Play Mode jest **wyłączony** — każdy stan statyczny resetować w metodzie z
  `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` (wzór: `Save.ResetStatics`).
- Async przez Unity `Awaitable` + `CancellationToken` — działa na wszystkich platformach bez `#if UNITY_WEBGL`,
  pod warunkiem że:
  - nie ma wątków: żadnego `Task.Run`, `Thread`, `Awaitable.BackgroundThreadAsync()`, timerów z `System.Threading`
    ani `Task.Delay` (zamiast tego `Awaitable.WaitForSecondsAsync`);
  - nie ma blokującego czekania: żadnego `.Wait()`, `.Result`, `WaitForCompletion()` (Addressables, Localization);
  - na operacje Addressables czekamy po klatce (`IsDone`), nie przez `handle.Task`;
  - jednego `Awaitable` nie awaitujemy dwa razy (Unity je recyklinguje) — kilku czekających dostaje osobne
    `AwaitableCompletionSource`.
- Pomocnicze `MonoBehaviour` tworzone przez kod (`SaveRunner`, `CloudSaveRunner`): `internal`, `[AddComponentMenu("")]`,
  `DontDestroyOnLoad`.
- Styl: namespace blokowy, 4 spacje, pola prywatne camelCase bez prefiksu, komentarze XML po angielsku i tylko tam,
  gdzie tłumaczą „dlaczego”. Do testów wewnętrzne API przez `InternalsVisibleTo` (`Runtime/SaveSystem/AssemblyInfo.cs`).
- Nowe pliki/foldery w Unity potrzebują `.meta` — tworzy je edytor po otwarciu projektu gry; nie commitować bez nich.

## Testy

Testy PlayMode (NUnit + UnityTest, `Awaitable` przez `AwaitableTestExtensions`) uruchamia się z Test Runnera w
projekcie gry referującym paczkę; żeby były widoczne, jego `manifest.json` potrzebuje
`"testables": ["com.niezor.core"]`. Testy zapisu podmieniają backend przez `Save.ResetForTests(...)`
(np. `MemorySaveBackend`).

Bez projektu gry: tymczasowy projekt Unity (w scratchpadzie) z `com.unity.test-framework` w manifeście, do którego
kopiuje się testowane moduły z ich asmdefami, i uruchomienie
`Unity.exe -batchmode -nographics -projectPath <proj> -runTests -testPlatform PlayMode -testResults <xml>`.
`Core.Utilities` w całości wymaga Odina, więc kopiuje się tylko potrzebne pliki do asmdefu `Core.Utilities` i dodaje
stuby: `TimeCache` (`unscaledDeltaTime => Time.unscaledDeltaTime`), `Sirenix.OdinInspector.ReadOnlyAttribute`
(dla `SceneRef`) i `HideMonoScriptAttribute` (dla starego `ScriptableObjectSettings`). Manifest: test-framework,
addressables, ugui + moduły `uielements`, `imgui`, `ui`, `jsonserialize`, `assetbundle`, `unitywebrequest`,
`unitywebrequestassetbundle`.
`-projectPath` musi być pełną ścieżką (`C:\Users\Użytkownik\...`), nie krótką 8.3 (`UYTKOW~1`) — inaczej Unity nie
mapuje skryptów na klasy (`MonoScript.GetClass()` zwraca null) i np. zapisuje sceny z wbudowanym `MonoScript`.
To samo (asset z `m_Script: {fileID: 0}`, niewidoczny dla `FindAssets("t:Typ")`) dzieje się, gdy klasa
`ScriptableObject` nie leży w pliku o swojej nazwie — także w testach i skryptach sprawdzających.
Scenariusze edytorowe (Build Settings, tworzenie scen) sprawdza się skryptem przez `-executeMethod`, nie testami w
repo — testy w repo uruchamiane w projekcie gry nie mogą zmieniać jego Build Settings.
