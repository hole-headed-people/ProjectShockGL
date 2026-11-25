# Руководство по ProjectShockGL

ProjectShockGL — движок на C# для небольших трёхмерных игр с редактором уровней. Основной способ работы: подготовить ресурсы, определить игровые сущности в C#, затем собрать пространство и связи между сущностями в редакторе.

Руководство объясняет устройство систем, способы обращения к ним и ограничения текущей реализации. Его можно читать последовательно или использовать оглавление для конкретной задачи. Примеры относятся к условной игре `DemoGame`, не задают обязательную архитектуру и используют имена ресурсов, которые нужно подготовить в своём Content. Скриншоты пока не включены.

## Оглавление

1. [Возможности и ограничения](#limits)
2. [Подключение и запуск](#start)
3. [Устройство и игровой цикл](#architecture)
4. [Объекты, компоненты и жизненный цикл](#lifecycle)
5. [Content и настройки](#content)
6. [Уровни и редактор](#editor)
7. [Geometry, BrushBox и Details](#geometry)
8. [Сущности, Inputs и Outputs](#logic)
9. [Ассеты, GUID и префабы](#assets)
10. [Время, ввод и корутины](#time)
11. [Физика](#physics)
12. [Камеры и мониторы](#cameras)
13. [Рендер и шейдеры](#render)
14. [Освещение](#lighting)
15. [UI](#ui)
16. [Частицы](#particles)
17. [Звук](#audio)
18. [Сохранения и OxygenGL](#saves)
19. [Отладка и публикация](#release)
20. [Карта исходников](#sources)

<a id="limits"></a>
## 1. Возможности и ограничения

Движок позволяет собрать уровень из моделей и брашей, добавить физические объекты, связать события сущностей, подготовить UI, освещение, частицы и звук. API развивается; неизменность интерфейсов между будущими версиями не обещана.

Различайте отсутствие возможности, ограничение реализации и правило работы. Скелетная анимация пока отсутствует; лимит света связан с рендерером; запрет движения статичного объекта — намеренный контракт.

| Область | Что учитывать сейчас |
| --- | --- |
| Платформы | Основная проверенная среда — Windows, .NET 8, OpenGL 3.3. Остальные ОС и консоли отдельно не проверены |
| Модели | OBJ. Нет импорта FBX, glTF/GLB, скелетов и анимационных клипов |
| Иерархия | Общего Parent–Child для объектов нет. Attach передаёт позицию и вращение, без масштаба |
| Физика | Box / Sphere / Capsule; нет MeshCollider. CharacterController не является динамическим телом Bepu |
| Свет | Realtime без динамических теней; запечённый свет и один отскок GI для статичных поверхностей. Нет Light Probes и Mixed |
| Лимит света | Один основной Directional и до 16 локальных Realtime Point/Spot в стандартном рендере. Это не подбор ближайших источников для каждого меша |
| Видимость | Есть отсечение частиц по камере. Нет общего Occlusion Culling и frustum-отсечения обычных мешей, статичных батчей и Details |
| Прозрачность | Alpha-смешивание с сортировкой; пересекающиеся поверхности могут иметь ошибки порядка. Частицы сортируются внутри эмиттера |
| UI | Одна точка Anchor, нет Anchor Min/Max и полноценного автоматического layout. Растровый текст без rich text, Bold/Italic и подбора FontSize |
| Звук | WAV целиком в памяти. Нет streaming, FMOD и расчёта реальной акустики по стенам |
| Ресурсы | Кэш Content живёт до явной очистки; автоматической выгрузки неиспользуемых ресурсов между уровнями нет |
| Загрузка | Смена уровня синхронная. Игра и редактор запускаются отдельно, встроенного Play нет |
| Упаковка | Открытая папка Content, без pak |

Допустимые размеры уровня и число объектов нужно измерять на своей игре. Неизмеренный предел не является ни гарантией производительности, ни доказанной непригодностью. Отложенные возможности находятся в [TODO](../TODO.md).

<a id="start"></a>
## 2. Подключение и запуск

### 2.1. Проекты

- ProjectShockGL — библиотека движка.
- ProjectOxygenGL — необязательная игровая архитектура: игрок, персонаж, порядок запуска уровней и интеграция сохранений.
- ProjectShockGLEditor — редактор. Его проект ссылается также на OxygenGL для доступности типов; ядро от него не зависит.
- StarterContent — начальные ресурсы, tests — проверки.

Требуются .NET SDK 8, NuGet для первого восстановления и OpenGL 3.3:

```powershell
dotnet build ProjectShockGL.sln
```

### 2.2. Минимальная игра на ядре

Создайте исполняемый проект .NET 8 со ссылкой ProjectReference на ProjectShockGL.csproj. Скопируйте StarterContent в Content своей игры. Базовый Game.Load загружает стандартный профиль постобработки и шейдеры: полностью пустой Content для старта не подходит.

Точка входа Program.cs:

```csharp
using ProjectShockGL;
using EngineGame = ProjectShockGL.Core.Game;

Settings.Initialize(isEditor: false);
EngineGame.Initialize();
Application.Run();
```

Класс игры в сборке DemoGame:

```csharp
namespace DemoGame;

public class Game : ProjectShockGL.Core.Game
{
    protected override void Load()
    {
        base.Load();
        ProjectShockGL.LevelManager.Load("FirstRoom");
    }
}
```

Карту Content/Levels/FirstRoom.map предварительно создайте редактором. Ядро не создаёт персонажа автоматически: его и активную камеру определяет ваша игра или OxygenGL.

GameSettings.ini возле exe:

```ini
[Window]
Width=1280
Height=720
Mode=Windowed
CanResize=true
VSync=true
[Application]
IsDebug=true
[Game]
Name=DemoGame
Class=DemoGame.Game, DemoGame
PostProcessingProfile=Default
SaveDirectory=Saves
[Content]
Path=Content
CustomPath=
[Audio]
Groups=Mixer
```

В Class указаны полное имя типа и имя сборки. Тип должен иметь конструктор без параметров. Groups=Mixer предполагает Content/Audio/Mixer.json из StarterContent; без этой настройки остаётся Master.

Рядом нужен Layers.ini:

```ini
[Layers]
0=Default
1=Player
2=Trigger
```

Копирование файлов настраивается в .csproj:

```xml
<ItemGroup>
  <None Update="GameSettings.ini;Layers.ini">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <CopyToPublishDirectory>Always</CopyToPublishDirectory>
  </None>
</ItemGroup>
```

Content тоже копируется с сохранением структуры либо подключается из исходного каталога через CustomPath.

### 2.3. Редактор своей игры

В EditorSettings.ini задайте Class редактора и тот же Content:

```ini
[Game]
Name=DemoGame Editor
Class=ProjectShockGLEditor.Game, ProjectShockGLEditor
PostProcessingProfile=Default
[Content]
Path=Content
CustomPath=D:\Games\DemoGame\Content
[Audio]
Groups=Mixer
```

```powershell
dotnet run --project ProjectShockGLEditor/ProjectShockGLEditor.csproj
```

Путь к Content не подключает игровые C#-типы. Для своих Entity создайте отдельный проект запуска редактора со ссылками на сборку игры и ProjectShockGLEditor. Пример точки входа:

```csharp
using ProjectShockGL;
using ProjectShockGL.UI;
using ProjectShockGLEditor;
using EngineGame = ProjectShockGL.Core.Game;

Settings.Initialize(isEditor: true);
UIActionCatalog.RegisterAssembly(typeof(DemoGame.Game).Assembly);
EngineGame.Initialize();
Editor.Initialize();
Application.Run();
```

Для Global UI actions из OxygenGL зарегистрируйте и её сборку. Регистрируется сборка, не каждый класс. Пересборка игровых типов требует перезапуска редактора: hot reload сборок нет.

<a id="architecture"></a>
## 3. Устройство и игровой цикл

| Система | Ответственность |
| --- | --- |
| Application | Окно, ввод, OpenGL и события загрузки, обновления, рендера, закрытия |
| Core.Game | World, UIManager, менеджер таймеров; обслуживание физики, UI и аудио |
| World | Владение объектами, создание, обновление, удаление, подготовка команд рендера |
| Object / Component | Поведение объекта и его состав |
| Content | Загрузка и кэш ресурсов |
| Renderer | Выполнение графических команд на GPU |
| OxygenGL.Core.Game | Необязательная организация игрока, уровней и сохранённой сессии |

Settings выбирает конфигурацию. Game.Initialize создаёт заданный класс и подписывает его на Application. Application.Run подготавливает окно и контекст, затем вызывает загрузку Game.

Порядок кадра:

1. Обновляются ввод и Time.
2. Game.Update обновляет World, таймеры и физику с игровым шагом; UI и обслуживание аудио используют независимый шаг.
3. Перед рендером синхронизируются CameraComponent, слушатель и зона реверберации.
4. Выполняются захваты камер наблюдения.
5. Мир рисуется с активной камеры с ResolutionScale и постобработкой.
6. Отдельно рисуются overlay/debug, UI и интерфейс редактора.

В Object.Update время уже масштабировано. Второе умножение на TimeScale изменит скорость повторно.

Редактор использует это же ядро без запуска игровых Start/Update/OnActiveChanged мира. Create и отрисовка нужны и редактору. IsDebug не превращает игру в редактор.

Исходники: [Application](../ProjectShockGL/Application.cs), [Game](../ProjectShockGL/Core/Game.cs), [World](../ProjectShockGL/World.cs).

<a id="lifecycle"></a>
## 4. Объекты, компоненты и жизненный цикл

### 4.1. Создание и поиск

Object принадлежит World. Компонент принадлежит Object и обслуживается через него. В игровом объекте:

```csharp
var spawned = World.Spawn<ProjectShockGL.Core.Object>();
spawned.Name = "TemporaryObject";
spawned.Transform.Position = new System.Numerics.Vector3(0, 1, 0);
World.Destroy(spawned);
```

World — свойство текущего объекта после принятия миром. У просто `new Object()` владельца ещё нет, обращение к World выбросит исключение. Для игрового создания используйте Spawn.

Find<T>() находит первый живой объект совместимого типа, включая наследников. Find("name") ищет точное имя, FindAll возвращает все совпадения. Для частого доступа сохраните ссылку; после уничтожения проверяйте IsDestroyed.

### 4.2. Назначение методов

| Метод | Когда и что делать |
| --- | --- |
| Create | При создании, также в редакторе. Добавить компоненты и локальные значения по умолчанию |
| OnActiveChanged | В игре при начальной инициализации активности и реальных изменениях |
| Start | Один раз при первом активном включении в игре; начать поведение после применения параметров |
| Update | Игровой шаг активного объекта; при TimeScale=0 может быть нулевым |
| Destroy | Отписки и освобождение принадлежащих объекту ресурсов |

После Create к объекту карты ещё применяются данные ассета и размещения: Transform, имя, свойства и Outputs. Не запускайте здесь движение двери, таймер прохождения или поиск ещё не загруженных соседей. Для этого подходит Start.

При первом включении сначала вызывается OnActiveChanged(true), затем Start. При следующих включениях Start не повторяется. Выключенный объект остаётся в World: его можно найти и включить SetActive(true). Выключенный Light не освещает сцену.

В переопределениях Start, Update и Destroy вызывайте base: базовый Object обслуживает компоненты и собственную очистку. Компоненты создаются через AddComponent<T>(), находятся через GetComponent<T>(). Игровую реакцию на IsActive редактор не вызывает.

### 4.3. Уничтожение и владение

World.Destroy запрашивает удаление; мир освобождает объект и исключает его из своих коллекций в порядке обработки. Уничтоженный экземпляр повторно не используется.

В Destroy отпишитесь от внешних событий, завершите свои таймеры и самостоятельные источники звука. Не вызывайте Dispose общей текстуры из Content при уничтожении одного объекта.

Cleanup — помощник для последовательности действий очистки. Сбой одного действия не мешает попытке выполнить остальные; ошибки могут объединяться и передаваться дальше. Это не замена правильному владению ресурсами и не подавление ошибок.

### 4.4. Статичность, Attach и KillY

Статичный объект всегда активен. После размещения в игре его Transform заблокирован, выключение и обычное удаление не выполняются. В редакторе он редактируется. Entity нельзя сделать статичной; Geometry всегда статична, у BrushBox подвижность задаётся Movable.

```csharp
if (World.Find("MovingBox") is { } box)
{
    AttachTo(box);
}
// Позже:
Detach();
```

Attach сохраняет мировую позицию и вращение, затем следует за позицией и вращением цели. Масштаб не наследуется. Есть Inputs AttachTo с именем цели и Detach без параметров. Объекты должны принадлежать одному World, циклы запрещены. Это не физическое соединение Rigidbody. Связь не записывается в .map: при необходимости её создаёт логика уровня.

У Entity ниже World.KillY вызывается Kill; по умолчанию он уничтожает объект. Kill можно переопределить. KillY настраивается для уровня в Environment.

Исходники: [Object](../ProjectShockGL/Core/Object.cs), [Component](../ProjectShockGL/Core/Component.cs), [Entity](../ProjectShockGL/Core/Entity.cs).

<a id="content"></a>
## 5. Content и настройки

### 5.1. Пути и загрузка

Settings.Initialize(false) читает GameSettings.ini, true — EditorSettings.ini. Файлы находятся возле exe. Переданный флаг определяет Application.IsEditor; ключ IsEditor в INI не требуется.

Content выбирает непустой CustomPath, иначе Path. Относительные пути вычисляются от папки приложения, не от рабочего каталога IDE. В релизе обычно используются Path=Content и пустой CustomPath.

| Папка | Содержимое / пример |
| --- | --- |
| Models | OBJ: Content.LoadMesh("Props/Crate") |
| Textures | Content.LoadTexture("Props/Crate", TextureFormat.Png) |
| Atlases | PNG + JSON именованных областей для UI |
| Fonts | TTF: Content.LoadFont("NotoSans-Regular", 24) |
| Shaders | GLSL .vert / .frag: Content.LoadShaderProgram("Lit", "Lit") |
| Entities / Geometry / Details | JSON ассетов соответствующих объектов |
| Prefabs / Levels | JSON префабов / карты .map |
| UI / Particles | JSON дерева виджетов / настройки частиц |
| Audio | WAV и JSON групп |
| PostProcessProfiles / Lightmaps | Профили постобработки / результаты запекания |
| Localization | Таблицы переводов |

Имена обычно указываются от соответствующей папки без расширения. TextureFormat по умолчанию Jpeg, PNG указывайте явно. Соблюдайте регистр фактических файлов для переносимости. Часть загрузчиков возвращает null при отсутствии ресурса, часть выбрасывает исключение; ошибки записываются в Log.

### 5.2. Кэш и очистка

Повторный запрос обычно возвращает кэшированный экземпляр. Смена уровня очищает объекты World, но оставляет общие ресурсы Content. UnloadAll — полная очистка, не штатная выгрузка одного неиспользуемого ресурса.

| Ресурс | Кто завершает жизнь |
| --- | --- |
| LoadTexture / LoadMesh / LoadFont / LoadAudioClip | Content |
| Собственный RenderTexture или временный меш компонента | Создавший компонент |
| Audio.CreateSource | Вызывающий код через Dispose или общий Shutdown |
| Audio.PlayOneShot | Audio после окончания / Stop |
| Core.Timer | Создавший его код через Dispose |
| Widget / Canvas | Дерево UI через Destroy |

Правка файла не означает автоматического обновления кэша. Некоторые редакторские операции явно инвалидируют данные; для остальных ресурсов нужен перезапуск или предусмотренная перезагрузка.

### 5.3. Окно

Window/Mode: Windowed либо Borderless. Width/Height — размер окна; Borderless использует монитор. StartMaximized разворачивает обычное окно с заголовком. VSync включает синхронизацию кадров. Window/Icon — картинка иконки окна относительно папки приложения; иконка exe задаётся при сборке отдельно.

Rendering/ResolutionScale: 0.1–1. ResolutionFilter: Linear / Nearest. Меняется разрешение мира, UI остаётся полноразмерным. INI — исходная конфигурация; пользовательские изменения движок автоматически не сохраняет в отдельный файл.

Исходники: [Content](../ProjectShockGL/Content.cs), [Settings](../ProjectShockGL/Settings.cs).

<a id="editor"></a>
## 6. Уровни и редактор

### 6.1. Первый уровень

1. Укажите Content игры в EditorSettings.ini.
2. Создайте File → New, добавьте BrushBox для пола и стен, затем свет.
3. Для OxygenGL разместите PlayerStart. В собственной архитектуре появление игрока определяете вы.
4. Настройте Edit → Environment и сохраните Content/Levels/FirstRoom.map.
5. Загрузите LevelManager.Load("FirstRoom") либо задайте StartupLevel при использовании OxygenGL.

Карта — данные размещения и окружения, не сохранение прохождения. Она хранит Placeables, Details, папки, группы и привязку lightmap.

### 6.2. Окна и действия

| Область | Назначение |
| --- | --- |
| Scene | Список объектов и общие папки |
| Groups | Редакторская видимость наборов |
| Palette | Создание BrushBox, Entity, Geometry, Details и префабов, папки и поиск |
| Inspector | Transform, IsActive, свойства, слои, поверхности, Outputs |
| Верхняя панель | Gizmo, Snap, Grid, настройки камеры и Perspective / Top / Front / Side |
| Edit | Environment, Texture, редакторы UI/Entity/Particles/Audio Groups, Bake Lighting |

| Действие | Управление |
| --- | --- |
| Выбор / множественный | ЛКМ / Ctrl+ЛКМ в сцене или Scene |
| Перебор перекрытых объектов | Повторные щелчки почти в одной точке SceneView |
| Полёт камеры | ПКМ, мышь, WASD; Q/E вниз/вверх |
| Сдвиг / фокус | Средняя кнопка мыши / F |
| Move / Rotate / Scale / Resize BrushBox | G / R / T / B |
| Временный Snap / отмена перетаскивания | Shift / Esc |
| Дублирование / удаление | Ctrl+D / Delete |
| Undo / Redo | Ctrl+Z / Ctrl+Y |
| Save / Save As | Ctrl+S / Ctrl+Shift+S |

Двойной щелчок числового поля включает ввод вместо перетаскивания. Общий gizmo нескольких объектов поддерживается; множественного изменения свойств Inspector нет.

Resize одного BrushBox меняет Size и позицию, оставляя противоположную грань на месте; учитывает поворот и масштаб и отменяется одним шагом. Texture — отдельный режим выбора граней.

Ортографические Top / Front / Side меняют плоскость сетки и управление видом. При возврате Perspective восстанавливается свободная камера. Lit/Unlit и Environment влияют только на предпросмотр. Рамка выделения вращается с объектом и показывает скрытые части иначе; физический AABB — отдельные границы.

### 6.3. Организация сцены и история

Папки Scene группируют разные виды размещений, раскрываются, выделяют содержимое и принимают drag-and-drop. Нет вложенности и общего Transform.

Groups отвечают за видимость: скрытие любой группы объекта скрывает его в редакторе, но не меняет IsActive и игровую физику. Состав и видимость записываются в карту, игровой рендер редакторскую скрытость игнорирует.

Undo/Redo охватывает трансформы, Inspector, Outputs, Environment, создание, удаление и дублирование. Новая/открытая карта начинает новую историю. Undo удаления создаёт новый экземпляр с прежним GUID, а не оживляет C#-ссылку на старый.

### 6.4. Загрузка в игре

```csharp
ProjectShockGL.LevelManager.Load("NextRoom", entryIndex: 2);
```

Загрузка синхронная: подготавливается новый World, затем заменяется содержимое игрового мира. Есть Loading / Loaded и Unloading / Unloaded. К Loaded объекты и параметры созданы, но обычный игровой Start ещё не выполнен обновлением World.

Не запускайте вложенную смену уровня из её событий: действует защита повторного входа. EntryIndex передаёт данные перехода; выбор PlayerStart по нему выполняет OxygenGL, не ядро.

<a id="geometry"></a>
## 7. Geometry, BrushBox и Details

| Тип | Назначение |
| --- | --- |
| Geometry | Статичная модель окружения с необязательным примитивным коллайдером |
| BrushBox | Коробка с размерами и настройками граней, может быть подвижной |
| DetailPlacement | Статичное декоративное размещение без Object, компонентов и коллайдера |

Пример Content/Geometry/Wall.json:

```json
{
  "Model": "Architecture/Wall",
  "Texture": "Architecture/Concrete",
  "Color": [1, 1, 1, 1],
  "ColliderType": "Box",
  "Size": [4, 3, 0.5],
  "Lit": true
}
```

Нужны соответствующие OBJ и текстура. Коллайдер — примитив, не точная форма модели. В GeometryData поле Size для Box передаётся как HalfExtents, то есть половина размера коллайдера; это отличается от полного Size у BrushBox. Geometry всегда статична, её Saveable нельзя включить.

У BrushBox Size задаёт форму, Transform.Scale — дополнительный масштаб, TextureScale — повторение текстуры. Movable разрешает подвижность; SimulatePhysics создаёт Rigidbody и делает браш подвижным.

В Edit → Texture выберите грань в сцене: задаются Texture, UV scale/offset, SurfaceMode и Fit. Fit подгоняет UV под грань, затем их можно смещать. Настройки записываются в карту и префабы. Редактирования вершин, выдавливания и CSG пока нет.

| SurfaceMode | Назначение |
| --- | --- |
| Opaque | Непрозрачно, альфа не создаёт отверстий |
| Cutout | Пиксели ниже AlphaCutoff отбрасываются; решётки и листва |
| Transparent | Полупрозрачное смешивание; стекло, с ограничениями сортировки |

BackfaceCulling=false у меша показывает обратную сторону; Lit корректирует её нормаль. Это не добавляет толщину или двустороннюю физическую форму.

DetailAsset задаёт Mesh, Texture, TextureFormat, Color, Lit, BackfaceCulling. DetailPlacement хранит AssetPath, Id и трансформ; World.Details — отдельная коллекция. Загруженная игровая коллекция запечатывается для изменений. Details размещаются, трансформируются и редактируются в редакторе с Undo/Redo; кисти пока нет.

```json
{
  "Mesh": "Decoration/Grass",
  "Texture": "Decoration/Grass",
  "TextureFormat": "Png",
  "Color": [1, 1, 1, 1],
  "Lit": true,
  "BackfaceCulling": false
}
```

Это пример Content/Details/Grass.json. Совместимые размещения рисуются через instancing — один меш с набором трансформов. Статичные Geometry и BrushBox могут объединяться в батчи в игре с обычными Lit/Unlit; редактор сохраняет отдельную отрисовку. Разные текстуры, поверхности и пользовательские шейдеры ограничивают объединение. Общего пространственного отсечения этих наборов нет.

<a id="logic"></a>
## 8. Сущности, Inputs и Outputs

### 8.1. Объявление поведения

Entity — Object с параметрами и Outputs. Editable отмечает публичное свойство для Inspector и сохранения размещения. Input отмечает вызываемый метод, OutputEvent сообщает редактору имена событий, но не вызывает их.

Тип в сборке DemoGame:

```csharp
using ProjectShockGL;
using ProjectShockGL.Core;

namespace DemoGame;

[OutputEvent("Activated")]
public class Relay : Entity
{
    [Editable]
    public string Message { get; set; } = "Activated";

    [Input("Activate")]
    public void Activate()
    {
        Log.Print(Message);
        InvokeEvent("Activated");
    }
}
```

Для появления в Palette нужен JSON ассета с Class и доступная редактору сборка. Одного C#-класса недостаточно.

### 8.2. Связь уровня

Output хранит Event, TargetName, Input и Parameter. В Inspector выбираются Event с учётом базовых классов, Target с поиском и Input цели; ручной ввод остаётся.

Назовите Light `room_light`, выключите IsActive и у Relay добавьте:

| Event | Target | Input | Parameter |
| --- | --- | --- | --- |
| Activated | room_light | SetActive | true |

Activate у Relay включает свет. Если несколько живых объектов имеют одно имя, событие обращается к найденным целям с этим именем; для однозначной связи используйте уникальные имена.

Статические параметры разделяются запятыми и преобразуются с InvariantCulture: дробное число пишется через точку. Это не универсальная сериализация: Color, Vector3 и сложные структуры через Convert.ChangeType не поддерживаются; строка с запятой не является непрозрачным единым аргументом.

InvokeEvent("ValueChanged", value) может передать динамическое значение Input с одним совместимым параметром. Метод без параметров вызывается без него; при несовместимости используется обычный Parameter. Значение само по себе не превращается в ссылку на Object.

### 8.3. Ссылки по имени

```csharp
[ProjectShockGL.Core.Editable]
[ProjectShockGL.Core.ObjectName]
public string Target { get; set; } = "";
```

Такое строковое свойство получает выбор объекта из сцены и участвует в переназначении внутренних связей при копировании. Обычные строки движок не должен угадывать как ссылки.

Исходники: [Entity](../ProjectShockGL/Core/Entity.cs), [InputInvoker](../ProjectShockGL/InputInvoker.cs), [ObjectLinks](../ProjectShockGL/Core/ObjectLinks.cs).

<a id="assets"></a>
## 9. Ассеты, GUID и префабы

### 9.1. Ассет Entity

Content/Entities/Logic/Relay.json:

```json
{
  "Class": "DemoGame.Relay, DemoGame",
  "Parameters": {
    "Message": "Door control activated",
    "DebugBillboard": "Editor/Entity"
  }
}
```

Entity Asset Editor редактирует Class и параметры ключ–значение. При первом Save назначается AssetId; повторный Save сохраняет его. Save As создаёт новый ассет с новым AssetId.

Параметры ассета — начальные значения. Свойства размещения в карте могут их переопределять. Редактирование ассета не является живой связью со всеми ранее созданными экземплярами.

### 9.2. Идентификаторы

Object.Guid — идентификатор экземпляра, используется картой и сохранением прохождения. EntityData.AssetId — идентификатор файла-шаблона, позволяет найти его после переноса по папкам.

Дублирование и вставка префаба дают новые Object.Guid; Undo удаления возвращает прежний. Не копируйте идентификатор между разными объектами.

Перед переносом старых ассетов выполните Entity Asset Editor → Fix Asset References. Он назначает недостающие AssetId, обновляет ссылки в картах/префабах, делает резервные копии и отчёт. Ошибки проверки запрещают запись. После переноса проверьте открытие уровней. Копия ассета с новым назначением не должна сохранять чужой AssetId — используйте Save As.

GUID-ссылки не распространяются автоматически на все ресурсы. Перенос текстуры или OBJ с путём-именем может потребовать исправить ссылки.

### 9.3. Префабы

Префаб — снимок одного или нескольких Placeable. Сохраните выделение через Save prefab и создайте экземпляр через Palette или код внутри объекта:

```csharp
var objects = ProjectShockGL.Core.Prefab.InstantiateAll("Puzzles/DoorAssembly",
    world: World,
    position: new System.Numerics.Vector3(4, 0, 8));
```

Instantiate предназначен для одного объекта, InstantiateAll — для набора. Вставка использует общую точку, без родительского Object. Изменение файла не обновляет уже размещённые экземпляры.

При вставке/дублировании набора выдаются новые имена и переназначаются внутренние Outputs и ObjectName-поля. Внешние ссылки остаются. Одинаковые непустые имена внутри набора проверяются до создания: такие связи неоднозначны. Произвольные строки не переназначаются.

<a id="time"></a>
## 10. Время, ввод и корутины

### 10.1. Время и ввод

| Time | Значение |
| --- | --- |
| DeltaSeconds / ElapsedTime | Игровой шаг / накопленное игровое время |
| UnscaledDeltaSeconds / UnscaledElapsedTime | Независимый шаг / накопленное время |
| TimeScale | Конечный неотрицательный множитель; 0 останавливает игровой отсчёт |

Object.Update получает масштабированный шаг. Пауза времени не запрещает Input и произвольные методы: блокировку управления задаёт игра.

Input использует Key, MouseButton и CursorMode из Silk.NET.Input. IsKey — удержание; IsKeyDown / IsKeyUp — переходы кадра. Для мыши есть соответствующие методы. При переходе к показу курсор центрируется в окне. Текстового InputField и игровой консоли пока нет.

ProjectShockGL.Tools.Random.Range предоставляет общий генератор: int имеет исключительную верхнюю границу. Rendering.Color хранит нормализованные компоненты; есть FromDrawing и готовые цвета с тонами.

### 10.2. Таймер

Timer не является Object. Его обновляет TimerManager из Game, а освобождает владелец:

```csharp
var timer = new ProjectShockGL.Core.Timer(useUnscaledTime: true);
timer.Finished += () => ProjectShockGL.Log.Print("Time elapsed");
timer.Run(2);
// Сохраните timer у владельца и вызовите Dispose при его уничтожении.
```

ValueChanged передаёт остаток, RemainingSeconds читает его. SetRemainingSeconds меняет остаток, SetPaused приостанавливает, Stop останавливает. Повторный Run перезапускает отсчёт без повторных подписок. Пауза не означает завершение.

### 10.3. Корутины

Корутина — IEnumerator, продолжаемый обновлением движка; это не отдельный поток. Методы внутри игрового Object/Entity:

```csharp
private System.Collections.IEnumerator Sequence()
{
    yield return new ProjectShockGL.Core.WaitForSeconds(1);
    ProjectShockGL.Log.Print("One game second elapsed");
    yield return new ProjectShockGL.Core.WaitForSeconds(1, useUnscaledTime: true);
    ProjectShockGL.Log.Print("One unscaled second elapsed");
}

public override void Start()
{
    base.Start();
    StartCoroutine(Sequence());
}
```

Yield null продолжает в следующем кадре; поддерживаются вложенные IEnumerator и предусмотренные ожидания. Неизвестный yield не становится произвольным async-await. StartCoroutine возвращает Coroutine; есть StopCoroutine и StopAllCoroutines.

Выключение владельца приостанавливает его корутины, уничтожение завершает. Для плавного изменения значения используйте шаги с Time.DeltaSeconds; готовой tween-системы пока нет.

<a id="physics"></a>
## 11. Физика

### 11.1. Формы и тела

Collider задаёт форму для запросов/контактов. Rigidbody добавляет динамическую симуляцию Bepu. Поддерживаются BoxCollider, SphereCollider, CapsuleCollider; произвольного compound-набора форм на одном объекте нет.

В Create сущности:

```csharp
var collider = AddComponent<ProjectShockGL.Core.BoxCollider>();
collider.HalfExtents = new System.Numerics.Vector3(0.5f);
var body = AddComponent<ProjectShockGL.Core.Rigidbody>();
body.Mass = 2;
```

LinearVelocity управляет линейной скоростью, AngularVelocity — угловой в радианах в секунду. AddImpulse(impulse) прикладывает импульс к центру масс, AddImpulse(impulse, worldPoint) может добавить вращение. Одинаковый импульс сильнее меняет скорость лёгкого тела.

Изменение Transform телепортирует тело, но не обязано обнулять скорости; при необходимости присвойте обеим Vector3.Zero. Отключённый Rigidbody оставляет коллайдер твёрдой опорой. У Entity Simulate physics отображается при наличии Rigidbody; у BrushBox флаг создаёт его.

### 11.2. Слои и запросы

Layer — бит слоя, CollidesWith — маска, не индекс строки Layers.ini:

```csharp
CollidesWith = ProjectShockGL.Layers.Get("Default")
             | ProjectShockGL.Layers.Get("Player");
```

В Inspector Layer выбирается списком, CollidesWith — галочками. Данные карты применяются после Create и могут переопределить его настройки. Layers.ini находится возле exe; индексы игры и редактора должны совпадать.

Запрос внутри объекта:

```csharp
var filter = new ProjectShockGL.PhysicsQueryFilter
{
    IgnoreObject = this,
    IgnoreTriggers = true
};
if (ProjectShockGL.Physics.Raycast(Transform.Position, Transform.Forward,
        4, ProjectShockGL.Layers.Everything, out var hit, filter))
{
    ProjectShockGL.Log.Print("Hit: " + hit.Object.Name);
}
```

Есть OverlapSphere, OverlapSphereAll, OverlapCapsule. Фильтр запроса не меняет CollidesWith. Дерево Bepu отбирает контактные пары, но текущие Raycast и Overlap перебирают коллайдеры. Собственные проверки и Bepu не являются одним алгоритмом для всех задач; некоторые коробочные проверки используют осевые границы, не точный повёрнутый или треугольный меш.

### 11.3. Триггеры и персонаж

IsTrigger задаёт область уведомлений вместо обычного твёрдого препятствия. Есть Object.OnTriggerEnter / Stay / Exit и OnCollisionEnter / Exit. Stay не принимает delta: используйте Time.DeltaSeconds для непрерывного действия.

CharacterController наследует капсульный коллайдер. Move принимает перемещение за шаг, не скорость. Пример внутри Update объекта с контроллером:

```csharp
var controller = GetComponent<ProjectShockGL.Core.CharacterController>();
controller?.Move(Transform.Forward * 2 * deltaSeconds);
```

Он решает скольжение, ступени, предел наклона и вытеснение пересекающимся движущимся коллайдером. Скорость, прыжок и гравитацию задаёт персонаж. Move обновляет IsGrounded и CollisionFlags.

Это не Rigidbody: автоматического толкания тел массой персонажа нет. Перенос движущимися платформами и полноценное решение зажатия не реализованы. Добавление Rigidbody к контроллеру само по себе не согласует две системы движения.

<a id="cameras"></a>
## 12. Камеры и мониторы

Application создаёт запасную Camera.Default. Camera.Active определяет основной вид. Transform.Forward направлен по локальной -Z; учитывайте это при подготовке моделей и точек появления.

CameraComponent владеет своей Camera, LocalPosition/LocalRotation задают смещение. Создайте компонент в Create, переключите его в игровой логике:

```csharp
var camera = AddComponent<ProjectShockGL.Core.CameraComponent>();
camera.LocalPosition = new System.Numerics.Vector3(0, 1.6f, 0);
// Сохраните camera в поле; в Start вызовите camera.MakeActive().
```

Отключение или уничтожение активной компонентной камеры возвращает запасную. В редакторе MakeActive не переключает рабочий вид. Автоматическая синхронизация выполняется перед рендером. Для направления фонарика внутри игрового шага можно явно вызвать Synchronize.

В Palette есть CameraEntity с Inputs MakeActive / ReleaseActive и CameraCaptureEntity с Width / Height / Filter. Захват рисует мир в RenderTexture без смены Camera.Active и слушателя аудио. UI основной игры туда не входит. Каждая камера добавляет проход мира.

Monitor — односторонний Unlit-квад без коллайдера. CameraName — имя CameraCapture на уровне; Setup меняет подключение кодом или Input. Transform.Scale задаёт размер поверхности независимо от разрешения захвата. Лицевая сторона монитора направлена по локальной +Z. Без камеры экран чёрный; отключённый захват оставляет последний кадр.

Для собственного экрана создайте RenderTexture через Content.CreateRenderTexture, вызовите Capture с отдельной камерой и используйте Texture. Ресурс принадлежит создателю и требует Dispose; компонент захвата освобождает свой автоматически.

<a id="render"></a>
## 13. Рендер и шейдеры

C# формирует команды и буферы, OpenGL передаёт отрисовку GPU. Draw call — отдельная команда отрисовки геометрии; батчинг уменьшает их число, объединяя совместимые объекты. MeshComponent создаёт MeshCommand, RenderQueue сортирует по RenderOrder и типу поверхности. Прозрачные объекты проверяют глубину, но не записывают её.

MeshComponent хранит Mesh, Texture, Color, Lit, Shader и ShaderParameters. Отдельной системы Material-ассетов нет. OBJ содержит геометрию, UV и нормали, не поведение сущности. ObjLoader преобразует V через 1-V; не переворачивайте текстуру второй раз специально для этого загрузчика.

### 13.1. Шейдер объекта

Внутри объекта с MeshComponent:

```csharp
var mesh = GetComponent<ProjectShockGL.Core.MeshComponent>();
if (mesh != null)
{
    mesh.Shader = ProjectShockGL.Content.LoadShaderProgram("Lit", "SimplePBR");
    mesh.ShaderParameters.Set("uRoughness", 0.7f);
    mesh.ShaderParameters.Set("uMetallic", 0.2f);
}
```

Uniform должны совпадать с GLSL. SimplePBR использует текущее освещение, но не отражения окружения и не полный PBR-пайплайн. Для деформаций есть Deform.vert; изменение формы требует согласованного преобразования нормалей.

Рендерер передаёт матрицы, цвет, текстуру, свет и туман при наличии соответствующих uniform. uTime — Time.ElapsedTime, uUnscaledTime — Time.UnscaledElapsedTime. Lit не добавляется к произвольной программе автоматически.

Для собственного свечения сохраните Shaders/Highlight.frag, начиная строго с первой строки:

```glsl
// @lit
uniform vec3 uGlowColor;
uniform float uGlowStrength;
vec3 GetEmission(vec3 normal, vec3 viewDirection)
{
    float rim = pow(1.0 - max(dot(normal, viewDirection), 0.0), 3.0);
    return uGlowColor * uGlowStrength * rim;
}
```

Content.LoadShaderProgram("Lit", "Highlight") вставит функцию в маркер стандартного Lit.frag и скомпилирует единую программу. Это не два прохода и не Shader Graph. Параметры задаются через ShaderParameters, свечение не освещает соседние объекты.

### 13.2. Постобработка

PostProcessSettings — статичное активное состояние. Профиль Content/PostProcessProfiles содержит VertexShader, FragmentShader, Enabled и словарь Parameters. PostProcessSettings.Apply("Default") копирует параметры и приводит к типам uniform.

Штатный PostProcess поддерживает виньетку, film grain и chromatic aberration. Нулевой силой эффект отключается. Bloom нет. Постобработка и ResolutionScale применяются к миру; UI и ImGui рисуются отдельно в полном разрешении.

Исходники: [Renderer](../ProjectShockGL/Rendering/Renderer.cs), [RenderQueue](../ProjectShockGL/Rendering/RenderQueue.cs), [шейдеры](../StarterContent/Shaders).

<a id="lighting"></a>
## 14. Освещение

Light имеет Type Point / Spot / Directional и Mode Realtime / Baked. Point ограничен радиусом, Spot — радиусом и внутренним/внешним углом. При выделении видны радиус, конус или направление. Выключенный Light исключается из текущего освещения.

Environment задаёт AmbientColor, туман и SkyColor. При SkyColorEnabled цвет очистки берётся из окружения, иначе — у активной камеры. DynamicAmbientColorEnabled / DynamicAmbientColor позволяют отдельно поднять свет подвижных объектов без проб.

### Запекание

1. Подготовьте статичные Geometry, BrushBox и Details.
2. Нужным источникам назначьте Baked.
3. В Edit → Bake Lighting выберите качество, атлас и GI, нажмите Bake.
4. Дождитесь успешного результата и сохраните карту. Clear снимает привязку результата.

Создаются отдельные UV lightmap, обычная развёртка остаётся. Рассчитываются прямой свет, перекрытие геометрией и один диффузный отскок с цветом поверхностей. Результат записывается в Content/Lightmaps, ссылка — в .map. Отмена не заменяет предыдущий результат незавершённым.

Статичная Lit-поверхность использует lightmap вместо повторного Ambient и добавляет Realtime. Динамические объекты получают Ambient / DynamicAmbient и Realtime, не получают Baked напрямую и не используют Light Probes.

После изменения статичной геометрии, цвета поверхностей или Baked-света нужно перезапекание. Запечённый свет не исчезает от выключения лампы в игре. Если нужно менять освещение во время игры, учитывайте это при выборе режима.

Текущая развёртка и упаковка базовые, качество зависит от разрешения и выборок. Realtime-теней, Mixed и нескольких отскоков GI пока нет.

<a id="ui"></a>
## 15. UI

### 15.1. Canvas и Widget

Sealed UIManager создаёт один Canvas. Тип выбирается Game/UICanvasClass; без настройки используется обычный Canvas. Корневые виджеты принадлежат Canvas, дочерние — Widget.

```csharp
using System.Numerics;
using ProjectShockGL;
using ProjectShockGL.UI;

namespace DemoGame;

public class GameCanvas : Canvas
{
    protected override void OnCreate()
    {
        var label = Add<Text>();
        label.Name = "Status";
        label.Rect.Position = new Vector2(20, 20);
        label.Rect.Size = new Vector2(320, 60);
        label.Font = Content.LoadFont("NotoSans-Regular", 24);
        label.Content = "Доступ разрешён / Access granted";
    }
}
```

Настройка: UICanvasClass=DemoGame.GameCanvas, DemoGame. Ещё один экран — ещё один корневой Widget или UIAsset в том же Canvas, не второй UIManager.

RectTransform вычисляет положение:

```text
AbsolutePosition = ParentPosition + ParentSize × Anchor + Position − Size × Pivot
```

Начало координат — левый верхний угол, Y вниз. Anchor=(0.5,0.5) выбирает центр родителя, Pivot=(0.5,0.5) совмещает с ним центр виджета. FillCanvas заполняет весь Canvas, а не только родителя; Rect и ручки блокируются.

SortOrder задаёт визуальный порядок, при равенстве учитываются соседи. SetAsFirstSibling / SetAsLastSibling / SetSiblingIndex меняют порядок в одном родителе. Мышь обрабатывается в согласованном порядке. Иерархия виджетов не связана с Attach объектов мира.

### 15.2. Pointer и события

Pointer у UIManager определяет попадание, наведение, нажатие, отпускание и прокрутку. Widget предоставляет OnMouseEnter / Exit / Pressed / Released для переопределения. HitTestVisible задаёт участие в выборе; декоративная картинка не должна случайно закрывать кнопку.

Button.Click — UIEvent:

```csharp
var button = ProjectShockGL.Core.Game.Instance.UIManager.Canvas.Add<Button>();
button.Rect.Size = new System.Numerics.Vector2(200, 50);
button.Click.AddListener(ProjectShockGL.Application.Quit);
// Отписка тем же делегатом:
button.Click.RemoveListener(ProjectShockGL.Application.Quit);
```

Click вызывается при отпускании левой кнопки над кнопкой после её нажатия. Есть цвета состояний и центрированный Text. Отключение во время начавшегося Click не отменяет оставшиеся Actions, но уничтожение и закрытие экрана ограничивают продолжение.

UIEvent<T> также предоставляет AddListener / RemoveListener с аргументом. Для отписки от лямбды сохраните её делегат, а не создавайте новую внешне такую же лямбду.

### 15.3. Image, атлас и 9-slice

Image принимает обычную Texture или Region. Атлас — PNG и JSON с Regions: X / Y / Width / Height в пикселях от верхнего левого угла изображения. Скрипты экспорта: [Photoshop](../Tools/Photoshop/ExportUiAtlas.jsx), [Aseprite](../Tools/Aseprite/ExportUiAtlas.lua).

```csharp
var atlas = ProjectShockGL.Content.LoadTextureAtlas("UI/Panels")
    ?? throw new System.IO.FileNotFoundException("UI atlas is missing.");
var image = ProjectShockGL.Core.Game.Instance.UIManager.Canvas.Add<Image>();
image.Region = atlas.GetRegion("Panel");
image.Rect.Size = new System.Numerics.Vector2(400, 220);
image.DrawMode = ImageDrawMode.Sliced;
image.Border = new System.Numerics.Vector4(8, 8, 8, 8);
image.BorderScale = 1;
image.EdgeMode = ImageSliceMode.Tile;
image.CenterMode = ImageSliceMode.Stretch;
image.FillCenter = true;
```

Border — Left / Top / Right / Bottom в пикселях исходной области, не всего атласа и не доли 0..1. BorderScale задаёт экранную толщину. Углы сохраняются, полосы и центр растягиваются или повторяются. Filled Horizontal/Vertical/Radial нет.

### 15.4. Текст

Text использует Font из TTF и растрового атласа заданной высоты. Есть русский/английский, перенос, выравнивание, обрезка и измерение. Размер задаётся загруженным Font, не произвольным FontSize виджета. Rich text, Bold/Italic и автоматический подбор размера пока отсутствуют.

Для локализации настройте Localization/Tables и Language, затем Text.Table, Key и Localize=true. LocalizationManager.SetLanguage обновляет подписанные тексты. Предпросмотр редактора пока не переводит их автоматически. Формат таблиц: [LocalizationData](../ProjectShockGL/Localization/LocalizationData.cs).

TextScrollView содержит Body типа Text и прокрутку колесом. Это не общий ScrollView для произвольных дочерних Image/Widget; ползунка нет.

### 15.5. UI Editor и JSON

Edit → UI Editor: дерево слева, предпросмотр в центре, Inspector справа. В Widgets создаются элементы. Есть мышиное перемещение/resize, дублирование, удаление, смена порядка и родителя drag-and-drop, Undo/Redo. При смене родителя сохраняется положение на экране, циклы запрещены.

Корень UIAsset — экран: нулевые Position/Anchor/Pivot, размер следует Preview size и игровому Canvas. Его ручки заблокированы, вложенные панели редактируются. New / Open / Save сохраняют JSON отдельно от .map; закрытие изменённого экрана запрашивает решение.

```csharp
var canvas = ProjectShockGL.Core.Game.Instance.UIManager.Canvas;
var screen = ProjectShockGL.Content.LoadUI("Screens/Menu").Instantiate(canvas);
var start = screen.Find<Button>("Start");
start?.Click.AddListener(() => ProjectShockGL.LevelManager.Load("FirstRoom"));
// Когда экран больше не нужен:
screen.Destroy();
```

Кодовая сборка и ассеты совместимы. Name и Find<T> ищут дочерние виджеты. Дополнительные публичные свойства с Editable появляются в UI Inspector и JSON для поддерживаемых сериализатором типов; штатные поля не дублируются.

### 15.6. Actions

У Button.Click можно добавить Action: Target — Widget данного экрана или Global, метод с UIAction и один параметр. Поддерживаются public void без аргумента либо с одним string / int / float / bool. Global-методы static; методы Widget вызываются на выбранном виджете.

```csharp
namespace DemoGame;

public static class MenuActions
{
    [ProjectShockGL.UI.UIAction("Load level")]
    public static void LoadLevel(string name)
    {
        ProjectShockGL.LevelManager.Load(name);
    }
}
```

Сборку зарегистрируйте через UIActionCatalog.RegisterAssembly в игре и редакторе. В JSON сохраняется идентификатор метода: переименование типа/метода или изменение сигнатуры может потребовать обновить ассет. Это не скриптовый язык и не доступ к любому методу игры.

Исходники: [UI](../ProjectShockGL/UI), [UIEditor](../ProjectShockGLEditor/UIEditor.cs).

<a id="particles"></a>
## 16. Частицы

ParticleSystemComponent хранит частицы внутри себя, не как отдельные Object. ParticleEmitter — Entity, загружающая JSON из Content/Particles; Inputs Play / Stop / Clear управляют эффектом.

В Create объекта:

```csharp
var particles = AddComponent<ProjectShockGL.Core.ParticleSystemComponent>();
var asset = ProjectShockGL.Content.LoadParticle("Effects/Smoke")
    ?? throw new System.IO.FileNotFoundException("Particle asset is missing.");
particles.Apply(asset);
```

Shape Point / Box / Sphere / Cone задаёт рождение. Offset локальный; позиция и поворот владельца учитываются при рождении, масштаб не растягивает область автоматически. Родившиеся частицы движутся в мировом пространстве.

Continuous использует Rate и MaxParticles. Burst создаёт BurstCount при Play. Stop прекращает появление новых, живые продолжают существовать; Clear удаляет их. Для Burst повторный Play создаёт новый выброс.

Particle Editor предоставляет New / Open / Save / Save As, живой preview, Play / Pause / Restart. Play после Pause продолжает без нового Burst. Есть начальные диапазоны размера, времени жизни, поворота; скорость вращения, размер по кривой, цвет по градиенту, Velocity over Lifetime и Noise.

Alpha подходит для дыма с альфой, Additive — для свечения (чёрный не добавляет цвет), Multiply — для затемнения (белый нейтрален при соответствующей альфе).

Эмиттер рисуется одним Unlit-проходом с туманом. CPU симулирует и сортирует видимые частицы, GPU рисует квады. Вне поля зрения симуляция продолжается, но частицы не загружаются на GPU. Проверка учитывает текущий размер квадрата; нулевая альфа отбрасывается до расчёта тумана.

Большие перекрывающиеся прозрачные квады могут быть дорогими при одном draw call. Важны экранное покрытие, размер и число слоёв, а не только число частиц. Soft particles, столкновения частиц и GPU-симуляция пока не реализованы.

<a id="audio"></a>
## 17. Звук

### 17.1. Клип и воспроизведение

AudioClip — общие данные из Content/Audio. AudioSource — одно воспроизведение, AudioSourceComponent связывает его с Object и освобождает при уничтожении владельца.

WAV поддерживает mono/stereo PCM 8/16/24/32 и float32; старшие форматы преобразуются в PCM16. Клип целиком загружается в память, предел — 256 MiB PCM. Нет streaming, OGG/MP3 и многоканальных записей.

```csharp
using AudioSystem = ProjectShockGL.Audio.Audio;
using ProjectShockGL.Audio;

var clip = ProjectShockGL.Content.LoadAudioClip("Effects/Impact")
    ?? throw new System.IO.FileNotFoundException("Audio clip is missing.");
var shot = AudioSystem.PlayOneShot(clip,
    spatialMode: AudioSpatialMode.ThreeD,
    position: new System.Numerics.Vector3(2, 1, 0),
    volume: 0.8f,
    groupName: "Effects",
    reverbEnabled: true,
    minDistance: 1,
    maxDistance: 20);
```

PlayOneShot по умолчанию создаёт 2D без Loop. Pause сохраняет источник, Resume продолжает. Stop освобождает сразу, Stop(fadeOut) — после затухания; естественное окончание очищается на следующем Audio.Update по состоянию OpenAL. Клип остаётся в Content. Без устройства возвращается уже освобождённый источник; перед поздней командой проверяйте IsDisposed.

Для музыки используйте AudioSystem.CreateSource(clip), задайте Loop, Volume, ReverbEnabled и вызовите Play. Музыка в 2D уже работает, но длинный WAV целиком занимает память. Источник принадлежит вашему коду и требует Dispose. Чтобы он пережил уровень, не делайте его единственным владельцем уничтожаемую сущность этого уровня.

### 17.2. AudioEmitter и 3D

В Palette AudioEmitter предоставляет ClipName с выбором WAV, SpatialMode, Volume 0..1, Loop, PlayOnStart, GroupName, ReverbEnabled и расстояния. Inputs Play / Pause / Resume / Stop не принимают параметров. Без PlayOnStart источник ждёт Play, в редакторе автоматически не звучит.

TwoD не зависит от камеры. ThreeD следует мировой позиции владельца: до MinDistance громкость полная, затем линейно уходит в ноль к MaxDistance. У обычного компонента по умолчанию TwoD, у AudioEmitter — ThreeD. Для отдельного AudioSource задавайте Position самостоятельно. Требуется 0 < MinDistance < MaxDistance.

Stereo в 3D сводится в отдельный mono-буфер, исходный stereo остаётся для 2D. Для позиционных эффектов лучше исходный mono. Переключение режима сохраняет позицию воспроизведения и Pause.

Слушатель получает позицию и ориентацию Camera.Active после синхронизации, камеры наблюдения его не меняют. Fade In через Play(seconds) и Fade Out через Stop(seconds) используют unscaled-время. Отключение владельца/компонента приостанавливает его звук; вручную поставленный Pause при включении автоматически не снимается.

### 17.3. Группы громкости

В обоих INI настройте Audio/Groups=Mixer для Content/Audio/Mixer.json:

```json
{
  "Groups": [
    { "Name": "Master", "Volume": 1 },
    { "Name": "Music", "Parent": "Master", "Volume": 0.8 },
    { "Name": "Effects", "Parent": "Master", "Volume": 1 },
    { "Name": "Environment", "Parent": "Effects", "Volume": 0.6 }
  ]
}
```

Game.Load читает группы до уровня, в игре и редакторе одинаково. Без настройки остаётся Master с громкостью 1. Имена уникальны без учёта регистра, отсутствующие родители и циклы запрещены. Проверяется весь файл до применения.

Итог: Volume источника × Fade × Volume группы × Volume всех родителей. Пространственное ослабление применяется дополнительно. Пустое/неизвестное GroupName использует Master; неизвестное имя сохраняется, чтобы позже подключить зарегистрированную группу.

Edit → Audio Groups Editor редактирует JSON через New / Open / Save / Save As. Сохранение активного файла обновляет группы редактора. Другой Save As не становится активным автоматически; уже запущенная игра не следит за файлом на диске. Явная перезагрузка: AudioSystem.ApplyGroups(Content.LoadAudioGroups("Mixer")).

В игре AudioSystem.GetGroup("Music") возвращает группу для изменения Volume. Изменение не переписывает JSON, пользовательские настройки нужно сохранять отдельно. После замены конфигурации ссылки на удалённые группы получите заново. Кодовая регистрация CreateGroup также доступна.

### 17.4. Реверберация

AudioReverbZone имеет Box / Sphere, Priority, TransitionTime и Room / Hall / Cave / Custom. BoxSize — полный размер; коробка учитывает поворот и абсолютный масштаб. Сфера использует Radius и максимальную компоненту абсолютного масштаба. При выделении показывается каркас.

Preset копирует параметры. Ручное изменение Strength, DecayTime, DecayHFRatio, Density, Diffusion, GainHF переводит его в Custom. Изменение формы, трансформа и Priority не меняет пресет.

Выбор идёт по позиции слушателя в World. Побеждает наибольший Priority; при равенстве — первая созданная подходящая зона. Выключенные и уничтоженные зоны не участвуют. Переход плавный за TransitionTime на unscaled-времени; выход приглушает эффект и хвост за время последней зоны. Ноль означает мгновенную смену.

ReverbEnabled источника независим от 2D/3D, по умолчанию true. Для сухой музыки/UI выключите его. Используется один общий эффект OpenAL EFX, а не смешивание нескольких помещений. Стены и двери сами не рассчитывают акустику и не блокируют звук. Если EFX нет, обычный звук продолжает работать; доступность — Audio.IsReverbAvailable, причина — в Log.

<a id="saves"></a>
## 18. Сохранения и OxygenGL

### 18.1. Кто за что отвечает

| Часть | Ответственность |
| --- | --- |
| SaveManager | Слоты, данные в памяти, чтение/запись JSON, события Saving / Saved / Loaded |
| SaveHelper | Состояния объектов по GUID в рамках именованного уровня |
| Core.GameState | Пустая основа состояния игры |
| OxygenGL.Core.GameState | Уровень и Transform игрока |
| OxygenGL.Core.Game | Переходы, создание игрока, сбор и восстановление сессии |

SaveManager не загружает уровень. В своей архитектуре вы задаёте подписки и порядок; OxygenGL предоставляет готовую реализацию, но не обязательный контракт ядра.

### 18.2. Данные объекта

Entity по умолчанию Saveable=true, Object — false. Geometry запрещает сохранение своего игрового состояния. Player и Character OxygenGL исключены: их данные принадлежат сессии.

SaveHelper сохраняет Transform, IsActive, удаление и данные объекта через интерфейсы. Пример методов внутри класса с полем bool _opened:

```csharp
public override void OnCaptureState(ProjectShockGL.Core.IObjectStateWriter state)
{
    base.OnCaptureState(state);
    state.Set("Opened", _opened);
}

public override void OnRestoreState(ProjectShockGL.Core.IObjectStateReader state)
{
    base.OnRestoreState(state);
    _opened = state.Get("Opened", false);
}
```

Не сохраняйте живую C#-ссылку вместо идентификатора. Компоненты не сохраняют автоматически всё своё внутреннее состояние: скорости Rigidbody, прогресс механизма и другие необходимые данные нужно включить явно.

GUID объектов карты должны быть записаны в .map; старый уровень перед использованием сохранений пересохраните редактором. Динамические Saveable-объекты пересоздаются по происхождению: ассет, член префаба либо класс World.Spawn. Тип должен быть доступен и создаваться без параметров. Произвольные изменения после спавна без записи состояния не восстановятся автоматически.

### 18.3. Слоты

```csharp
var slotId = ProjectShockGL.SaveManager.CreateSlot();
ProjectShockGL.SaveManager.Save();
var slots = ProjectShockGL.SaveManager.GetSlots();
// Существующий слот:
ProjectShockGL.SaveManager.SelectSlot(slotId);
ProjectShockGL.SaveManager.Load();
```

CreateSlot выбирает идентификатор, файл появляется после успешного Save. GetSlots возвращает .save со временем записи. Save/Load не принимают путь: Game/SaveDirectory задаёт каталог. Относительный — от LocalAppData/<Game Name>, абсолютный используется напрямую. Смена Game/Name меняет путь относительных сохранений.

Saving приходит перед записью; Saved передаёт SaveResult, Loaded — LoadResult. Проверяйте результат. Сбор уровня в память не равен записи на диск.

### 18.4. Порядок OxygenGL

OxygenGL.Core.Game читает StartupLevel. После LevelManager.Loaded она восстанавливает SaveHelper, создаёт Player и Character, применяет Transform сохранённой сессии и вызывает BeginPlay. EntryIndex выбирает PlayerStart перехода; сохранение возвращает записанную позицию игрока. Start объектов выполняется после восстановления при обычном обновлении мира.

На Unloading и перед Save через Saving состояние уровня собирается в память; на диск — только по Save. EndPlay связан с уходом с уровня. Есть события и виртуальные OnBeginPlay / OnEndPlay для расширения Game.

Для использования OxygenGL унаследуйте свою Game от ProjectOxygenGL.Core.Game, а не от Core.Game ядра, и задайте StartupLevel. Game/PlayerClass и PlayerCharacterClass должны обозначать наследников OxygenGL.Core.Player и OxygenGL.Core.Character; без указанных классов они не создаются автоматически. GameStateClass обозначает наследника OxygenGL.Core.GameState, по умолчанию используется базовый тип OxygenGL. GameState можно наследовать, расширив OnCaptureState / OnRestoreState у Game. HUD и управление игроком — также соглашения OxygenGL, не обязательная часть Canvas.

Save Level State в Environment исключает уровень из сбора и восстановления — например, меню. Новую игру организует игровая архитектура: GetData().Levels.Clear() и GetData().SetGameState(null) очищают разные данные. Учитывайте, что последующее Unloading сохраняемой старой карты может снова собрать её состояние: порядок перехода и исключение меню нужно задать осознанно.

Исходники: [SaveManager](../ProjectShockGL/SaveManager.cs), [SaveHelper](../ProjectShockGL/SaveHelper.cs), [OxygenGL.Game](../ProjectOxygenGL/Core/Game.cs).

<a id="release"></a>
## 19. Отладка и публикация

### 19.1. Log и DebugRender

Log.Print / PrintWarning / PrintError пишут в консоль и LocalAppData/<Game Name>/Logs/Game.log либо Editor.log. Файл текущего запуска перезаписывается при инициализации. Ошибка записи файла не должна лишать вывода в консоль.

DebugRender объектов и компонентов работает в standalone при Application/IsDebug=true. DebugRenderSelected вызывается редактором для выделенного. DebugDraw предоставляет Line, Box, Sphere, Billboard. В расширении базового поведения вызывайте base-метод. Это отрисовка ядра, не зависимость от ImGui.

IsDebug=false исключает отладочный проход; режим редактора определяется отдельно. Обязательное изображение игры нельзя строить только на DebugRender.

### 19.2. Показатели

Игровой debug-overlay показывает FPS, время кадра, CPU Update/Render, draw calls мира/постобработки/UI-debug, треугольники, размеры мира и managed-память. Последняя не включает всю нативную и GPU-память.

Audio CPU включает обслуживание источников и Spatial-синхронизацию. Эти затраты уже входят в Update/Render; фоновое микширование OpenAL не измеряется. CPU Render — не полный замер GPU: драйвер может выполнять команды асинхронно.

Сравнивайте одинаковое положение камеры и состояние сцены без VSync. 300 FPS — около 3.33 мс кадра, 200 — 5 мс. Само количество draw calls ещё не описывает стоимость больших прозрачных частиц или дополнительных камер.

### 19.3. Проверки

Из корня репозитория:

```powershell
dotnet run --project tests/LifecycleChecks
dotnet run --project tests/PhysicsChecks
dotnet run --project tests/UIChecks
dotnet run --project tests/EditorChecks -- --imgui
dotnet run --project tests/AudioChecks
dotnet run --project tests/TextChecks -- . --render
```

Есть также проверки времени, корутин, камер, движения, ввода, постобработки и окна. Запускайте подходящие к изменению. GPU-проверкам нужен OpenGL; AudioChecks использует null-вывод и не воспроизводит сигналы через колонки. Они не заменяют тест игры, прослушивание и запуск релизной сборки на другом компьютере.

### 19.4. Выпуск

Публикация готовит запуск без IDE. Пример для собственного игрового проекта:

```powershell
dotnet publish .\DemoGame\DemoGame.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish-game
```

Редактор публикуется своим проектом/точкой входа в отдельную временную папку. Для общей поставки затем объедините приложения и один Content. В репозитории движка универсального Publish.ps1 нет; автоматизация комплекта относится к вашему игровому проекту.

SingleFile упаковывает подходящие зависимости; нативные библиотеки имеют отдельные правила доставки/извлечения. Контент не обязан быть внутри exe. INI, Layers.ini и Content должны находиться в согласованной структуре. Сохраняйте сведения о лицензиях библиотек, включая OpenAL Soft.

Проверьте комплект без исходных каталогов и SDK: CustomPath пуст, Content внутри своей папки, оба exe работают, уровни и сохранения загружаются после перезапуска. OutputType=WinExe убирает консоль игры; редактор может оставаться Exe с консолью. Ошибки игры без консоли ищите в Log.

<a id="sources"></a>
## 20. Карта исходников

| Система | Где читать дальше |
| --- | --- |
| Запуск и цикл | [Application](../ProjectShockGL/Application.cs), [Game](../ProjectShockGL/Core/Game.cs), [Time](../ProjectShockGL/Time.cs) |
| Объекты | [World](../ProjectShockGL/World.cs), [Object](../ProjectShockGL/Core/Object.cs), [Component](../ProjectShockGL/Core/Component.cs) |
| Ресурсы | [Content](../ProjectShockGL/Content.cs), [Settings](../ProjectShockGL/Settings.cs) |
| Редактор | [Editor](../ProjectShockGLEditor/Editor.cs), [Inspector](../ProjectShockGLEditor/Inspector.cs) |
| Логика | [Entity](../ProjectShockGL/Core/Entity.cs), [ObjectLinks](../ProjectShockGL/Core/ObjectLinks.cs) |
| Физика | [Physics](../ProjectShockGL/Physics.cs), [Rigidbody](../ProjectShockGL/Core/Rigidbody.cs), [CharacterController](../ProjectShockGL/Core/CharacterController.cs) |
| Рендер | [Renderer](../ProjectShockGL/Rendering/Renderer.cs), [RenderQueue](../ProjectShockGL/Rendering/RenderQueue.cs) |
| UI | [UIManager](../ProjectShockGL/UI/UIManager.cs), [Widget](../ProjectShockGL/UI/Widget.cs), [UIAsset](../ProjectShockGL/UI/UIAsset.cs) |
| Звук | [Audio](../ProjectShockGL/Audio/Audio.cs), [AudioSource](../ProjectShockGL/Audio/AudioSource.cs), [AudioReverbZone](../ProjectShockGL/Placeables/AudioReverbZone.cs) |
| Частицы | [ParticleSystemComponent](../ProjectShockGL/Core/ParticleSystemComponent.cs), [ParticleAsset](../ProjectShockGL/Core/ParticleAsset.cs) |
| Сохранения | [SaveManager](../ProjectShockGL/SaveManager.cs), [SaveHelper](../ProjectShockGL/SaveHelper.cs) |
| Игровая архитектура | [OxygenGL/Core](../ProjectOxygenGL/Core) |
