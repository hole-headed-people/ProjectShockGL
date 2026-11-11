# ProjectShockGL

Небольшой движок на C# и OpenGL с редактором уровней. Создаётся для системной игры от первого лица, вдохновлённой System Shock: исследование комнат, головоломки и связи между объектами уровня. Это личный инструмент; API развивается вместе с игрой.

При разработке движка активное используется Codex

## Содержание

1. [Устройство репозитория](#устройство-репозитория)
2. [Сборка и запуск](#сборка-и-запуск)
3. [Подключение своей игры и Content](#подключение-своей-игры-и-content)
4. [Первый уровень](#первый-уровень)
5. [Работа в редакторе](#работа-в-редакторе)
6. [Объекты уровня](#объекты-уровня)
7. [Игровая логика](#игровая-логика)
8. [Объекты, компоненты и жизненный цикл](#объекты-компоненты-и-жизненный-цикл)
9. [Время, таймеры и корутины](#время-таймеры-и-корутины)
10. [Физика и персонаж](#физика-и-персонаж)
11. [Отображение](#отображение)
12. [UI и текст](#ui-и-текст)
13. [Ресурсы и настройки](#ресурсы-и-настройки)
14. [Проверки и ограничения](#проверки-и-ограничения)

## Устройство репозитория

| Проект | Назначение |
| --- | --- |
| [ProjectShockGL](ProjectShockGL) | Движок: окно, мир, компоненты, физические запросы, ресурсы, рендер и UI |
| [ProjectShockGLEditor](ProjectShockGLEditor) | Редактор уровней на ImGui |
| [StarterContent](StarterContent) | Стартовые ресурсы для редактора и собственной игры |
| [tests](tests) | Консольные проверки систем |

Редактор ссылается на движок и ProjectOxygenGL — библиотеку конкретной игровой архитектуры. Для пользовательских классов игры добавьте небольшой проект запуска редактора в решение игры со ссылками на игру и ProjectShockGLEditor. Само ядро ProjectShockGL от игровых проектов не зависит.

## Сборка и запуск

Требования:

- .NET SDK 8.0; выбор SDK описан в [global.json](global.json).
- OpenGL 3.3 и подходящий драйвер видеокарты.
- Windows — основная среда разработки; другие платформы не проверены.
- Доступ к NuGet при первом восстановлении пакетов.

Из корня репозитория:

```sh
dotnet build ProjectShockGL.sln

```

Перед первым запуском редактора укажите абсолютный путь к папке StarterContent или Content своей игры в EditorSettings.ini. Затем запустите редактор командой `dotnet run --project ProjectShockGLEditor/ProjectShockGLEditor.csproj`.

В Rider или Visual Studio можно выбрать соответствующий стартовый проект. Кнопки Play внутри редактора пока нет: редактор и игра запускаются отдельно. Предупреждение NU1701 относится к целевой платформе используемого пакета ini-parser.

## Подключение своей игры и Content

### Папка ресурсов редактора

Редактору необходимо указать папку Content той игры, уровни которой он будет редактировать. Настройки находятся в [ProjectShockGLEditor/EditorSettings.ini](ProjectShockGLEditor/EditorSettings.ini):

```ini
[Content]
Path=Content
CustomPath=D:\MyGame\Content
```

Правила выбора пути в [Content.cs](ProjectShockGL/Content.cs):

1. Непустой CustomPath имеет приоритет над Path.
2. Абсолютный CustomPath используется напрямую.
3. Относительный CustomPath вычисляется от папки запущенного приложения — AppDomain.CurrentDomain.BaseDirectory, а не от корня репозитория и не от расположения исходного Settings.ini.
4. При пустом CustomPath используется Path относительно папки приложения. По умолчанию это Content рядом с exe.

В общей опубликованной папке оба приложения используют Path=Content и пустой CustomPath. Для отдельного запуска редактора из IDE укажите путь к Content в его EditorSettings.ini или обеспечьте копирование ресурсов в папку вывода.

Settings.Initialize(isEditor: true) читает EditorSettings.ini, Settings.Initialize(isEditor: false) (или вызов без аргумента) — GameSettings.ini. Оба файла ищутся рядом с запущенным приложением. Настройте CopyToOutputDirectory и CopyToPublishDirectory, чтобы INI попадали в сборку. После изменения исходного INI пересоберите приложение; уже опубликованные файлы можно редактировать непосредственно. Выбранный корень Content выводится в консоль.

Одной смены папки недостаточно для загрузки пользовательских классов: сборка с ними также должна быть доступна редактору. В JSON сущности указывается полное имя типа и сборки, например `MyGame.Placeables.Switch, MyGame`.

### Минимальная основа собственной игры

1. Создайте исполняемый проект .NET 8 со ссылкой на ProjectShockGL.
2. Добавьте GameSettings.ini и Layers.ini с копированием в папку сборки.
3. Подготовьте Content и настройте его копирование либо укажите CustomPath.
4. Создайте наследника ProjectShockGL.Core.Game и укажите его в Game/Class.
5. Подключите сборку игровых сущностей к редактору и задайте редактору этот же Content.

Точка входа:

```csharp
using ProjectShockGL;
using ProjectShockGL.Core;

Settings.Initialize(isEditor: false);
Game.Initialize();
Application.Run();
```

Пример выбора класса:

```ini
[Game]
Class=MyGame.MyGame, MyGame
PostProcessingProfile=Default
```

Базовый Game.Load загружает профиль постобработки, даже если эффекты вам не нужны. В Content должны быть необходимые стандартные шейдеры и валидный профиль Default; для отключения эффектов можно задать Enabled=false. Не начинайте с полностью пустой папки: скопируйте содержимое StarterContent в Content своей игры. Для своего жизненного цикла загрузки можно переопределить Game, учитывая обязанности базового Load.

## Первый уровень

Следующие шаги предполагают, что вы создали игру на ProjectOxygenGL и скопировали StarterContent в её Content:

1. Укажите Content игры в настройках редактора.
2. Выберите File → New или File → Open.
3. Нажмите Create BrushBox в Palette. Новый объект появляется в начале координат.
4. Соберите пол и стены, изменяя Transform. Назначьте текстуры.
5. Создайте PlayerStart из категории Placeables и поставьте над полом.
6. Добавьте свет; откройте Edit → Environment для окружения уровня.
7. Сохраните карту как Content/Levels/MyLevel.map.
8. В GameSettings.ini игрового проекта задайте `[Game] StartupLevel=MyLevel` и запустите игру.

Стартовый уровень читает [ProjectOxygenGL.Core.Game](ProjectOxygenGL/Core/Game.cs), он же создаёт игрока и персонажа после загрузки. В собственной игре эти действия определяет ваш Game.

### Пример связи уровня

1. Назовите объект `room_light` и снимите IsActive в Inspector.
2. Создайте Placeables/Trigger, разместите его область и включите CanTriggeredByPlayer.
3. Добавьте Output: Event=`OnTriggerEnter`, Target=`room_light`, Input=`SetActive`, Parameter=`true`.
4. Сохраните карту и проверьте её в игре.

Trigger и понятие игрока здесь принадлежат ProjectOxygenGL. В собственной игре условие активации определяется вашим кодом.

## Работа в редакторе

### Панели

| Панель / инструмент | Назначение |
| --- | --- |
| Scene | Список объектов, включая выключенные и скрытые |
| Groups | Группы редакторской видимости |
| Palette | Создание BrushBox, Geometry и Entity |
| Inspector | Transform, IsActive, свойства объекта и Outputs |
| Camera в верхней панели | Скорость и чувствительность камеры |
| Edit → Environment | Ambient, туман и другие параметры окружения уровня |

В Palette папки внутри Entities и Geometry становятся категориями. Поиск работает по имени и пути без учёта регистра и раскрывает найденные категории. Refresh перечитывает список после добавления файлов.

В выборе Texture у BrushBox также есть папки и поиск. Имя нового объекта короткое: `Placeables/TimerEvent` → `TimerEvent_1`. Путь ресурса сохраняется отдельно. Перенос файла ресурса требует обновления ссылок на него в картах.

### Управление

| Действие | Управление |
| --- | --- |
| Выбор | ЛКМ в сцене или строка Scene |
| Осмотр / движение камеры | ПКМ + мышь / WASD, Q/E вниз/вверх |
| Сдвиг камеры | Средняя кнопка мыши + движение |
| Фокус | F |
| Move / Rotate / Scale | G / R / T |
| Resize одного BrushBox | B или кнопка Resize |
| Изменение трансформа | Перетаскивание оси или кольца gizmo |
| Привязка | Snap или временное удержание любого Shift |
| Отмена перетаскивания | Esc |
| Дублирование / удаление | Ctrl+D / Delete |
| Undo / Redo | Ctrl+Z / Ctrl+Y |
| New / Open | Ctrl+N / Ctrl+O |
| Save / Save As | Ctrl+S / Ctrl+Shift+S |

### Resize BrushBox

Выделите один BrushBox и включите Resize в верхней панели или нажмите B над сценой. В центрах шести граней появляются ручки. Перетаскивание ручки изменяет Size вдоль локальной оси и сдвигает центр на половину фактического смещения грани; противоположная грань остаётся на месте. Поворот, неравномерный и отрицательный масштаб учитываются, Transform.Scale сохраняется.

Snap и удержание Shift привязывают смещение грани к Move step в мировых единицах. Минимальный локальный размер — 0.01; грань не проходит через противоположную сторону. При нулевом масштабе оси её ручки недоступны. Ручку, направленную точно вдоль взгляда, удобнее тянуть после небольшого поворота камеры.

Отпускание мыши сохраняет Size и позицию одним действием Undo/Redo; Esc восстанавливает исходное состояние. Коллайдер следует за Size, плотность текстуры сохраняется. Для нескольких объектов, Geometry и Entity этот режим не применяется. Resize изменяет коробку целиком и не добавляет редактирование вершин или выдавливание граней.
### Groups

1. В отдельной панели Groups введите имя и нажмите Create group.
2. Выделите объект, раскройте группу и нажмите Add selected.
3. Remove исключает объект; Delete group удаляет группу, сохраняя объекты.
4. Переключатель у имени скрывает или показывает группу.

Один объект может входить в несколько групп; скрытие любой из них скрывает его. Скрытые объекты не рисуются, не выбираются мышью в сцене и не показывают gizmo; скрытый свет не освещает предпросмотр. Через списки их можно выбрать для настройки.

Группы не имеют Transform, не меняют IsActive или коллайдеры и игнорируются игровым рендером. Состав и видимость сохраняются в карте. Дублирование копирует принадлежность, восстановление удалённого объекта возвращает её.

### История и сохранение

Общая история включает трансформы, Inspector, Outputs, Environment, создание, удаление, дублирование и групповые операции. Непрерывное редактирование одного поля объединяется в шаг. Новое действие после Undo сбрасывает ветку Redo. Открытие или создание уровня очищает историю.

Undo удаления создаёт новый экземпляр по снимку, а не оживляет старый; предыдущие редакторские действия используют EditorObjectReference для доступа к восстановленному объекту. Для расширения редактора используйте SceneObjects.Create, Duplicate и Delete, чтобы сохранять историю и состояние изменений.

### Предпросмотр

Lit/Unlit и Environment в верхней панели меняют только предпросмотр: Unlit убирает освещение, отключённый Environment убирает туман и делает Ambient предпросмотра белым. Они не изменяют настройки уровня. View → Post processing переключает постобработку редактора. Настройки самого окружения редактируются отдельно и сохраняются в map.

## Объекты уровня

| Тип | Применение |
| --- | --- |
| Geometry | Статичная модель и коллайдер из JSON-ресурса |
| BrushBox | Процедурная коробка для геометрии уровня |
| Entity | Именованный объект с Inputs, Outputs и свойствами логики |

### BrushBox

| Свойство | Значение |
| --- | --- |
| Size | Полный локальный размер коробки по XYZ |
| Texture | JPG/PNG из Textures или WhitePixel |
| Texture scale | Число повторений на мировую единицу |
| Color / Lit | Цвет и участие в освещении |
| Movable | Разрешает использовать brush как нестатичную цель движения |

Коллайдер соответствует размерам. Изменение Size или Scale сохраняет плотность текстуры; отдельный файл модели не нужен. Для стены `(4, 3, 0.25)` при Scale=1 означает ширину 4, высоту 3 и толщину 0.25.

### Активность и статичность

- SetActive(false) исключает объект из обновления, отрисовки и физических запросов. Он остаётся доступен через World.Find. Выключенный свет не освещает сцену.
- IsStatic пропускает обычный Update, защищает от обычного World.Destroy в игре, позволяет кешировать границы и исключает события столкновений между двумя статичными объектами. Выгрузка мира освобождает их.
- IsStatic не блокирует прямую запись Transform и не объединяет геометрию в батчи.
- Geometry статична. BrushBox по умолчанию статичен; Movable=true делает его нестатичным. LinearMovement отказывается двигать статичную цель.

Для управления brush по TargetName превращать его в Entity не требуется: поиск цели работает с Object. Параметр Movable сохраняется, дублируется и поддерживает Undo/Redo.

## Игровая логика

### Inputs и Outputs

| Поле Output | Назначение |
| --- | --- |
| Event | Имя события, отправляемого исходной сущностью |
| Target | Имя объекта в мире |
| Input | Имя метода с атрибутом Input |
| Parameter | Аргументы через запятую |

Имена целей должны быть уникальны: поиск возвращает первый подходящий объект. Числа записываются с точкой, bool — true/false. Outputs вызываются кодом через Entity.InvokeEvent; одно наличие строки в Inspector не создаёт событие автоматически.

`InvokeEvent("OnValueChanged", value)` передаёт одно динамическое значение. Совместимый Input с одним аргументом получает его вместо Parameter. Поддерживаются совпадающие типы, числовые преобразования и преобразование в строку. Число автоматически не становится bool. При несовместимости или нескольких аргументах используется Parameter; Input без аргументов вызывается без них.

### Своя сущность

Пример пользовательского класса в сборке MyGame:

```csharp
using ProjectShockGL.Core;

namespace MyGame.Placeables;

public class Switch : Entity
{
    [Editable] public bool Locked { get; set; }

    [Input("Use")]
    public void Use()
    {
        if (!Locked) InvokeEvent("OnUsed");
    }
}
```

Сохраните Content/Entities/Logic/Switch.json:

```json
{
  "Class": "MyGame.Placeables.Switch, MyGame",
  "Parameters": {}
}
```

После подключения сборки и обновления Palette ресурс доступен в категории Logic. Это пример логики без модели. Изображение и коллайдер создаются отдельно в Create через компоненты. Parameters — данные ресурса для Entity.Load(Dictionary<string, object>); Editable-свойства экземпляра сохраняются в карте отдельно.

## Объекты, компоненты и жизненный цикл

Application создаёт окно и вызывает события цикла. Game владеет World и sealed UIManager. World создаёт, обновляет и удаляет Object. Объект содержит Transform, компоненты, активность и маски столкновений; Entity расширяет его ресурсом и связями уровня.

1. World.Spawn сразу вызывает Create: здесь создавайте компоненты и подготавливайте объект.
2. Загрузчик назначает свойства и трансформ из карты.
3. При активном игровом обновлении вызывается начальный OnActiveChanged(true), затем Start.
4. Start вызывается один раз; у изначально выключенного объекта откладывается до первого активного обновления.
5. Активный нестатичный объект получает Update. Повторное включение не повторяет Start.
6. Уничтожение останавливает корутины и освобождает компоненты. Уничтоженный объект повторно не включается.

В переопределениях вызывайте базовый метод, чтобы сохранить обслуживание компонентов. Используйте World.Destroy для удаления, World.Clear для полной очистки. Cleanup выполняет все заданные операции освобождения, собирая ошибки; ошибка одной операции не должна оставлять остальные ресурсы без очистки.

В редакторе выполняется Create, загрузка свойств и отображение, но не игровая активация, Start, Update или корутины. Не размещайте запуск таймеров и игровой сценарий в Create. World.Find<T>() и Find(name) видят также созданные объекты, ожидающие Start.

### Уничтожение ниже уровня

World.KillY задаёт общий нижний порог по мировой координате Y (по умолчанию -1000). При Position.Y < KillY у активной Entity вызывается виртуальный Kill(), который по умолчанию вызывает Destroy(). Порог задаётся полем Kill Y в окне Environment и сохраняется в поле KillY файла уровня. Изменение поддерживает Undo/Redo. Reset to defaults возвращает -1000. При загрузке старых карт без поля используется -1000, а не значение предыдущего уровня. Из игрового кода порог доступен через World.KillY.

Проверка выполняется после обновлений и корутин мира при положительном игровом шаге, включая статичные Entity. Обычные Object и геометрия не затрагиваются. В редакторе, при simulate=false и на паузе автоматического вызова нет. Переопределите Kill() для смерти персонажа или возврата на безопасную позицию; base.Kill() уничтожит сущность. Если переопределение оставляет её ниже границы, вызов не повторяется каждый кадр: сущность должна вернуться к границе или выше, чтобы следующее падение снова вызвало Kill().

### Отладочная отрисовка

Object и Component имеют виртуальные методы DebugRender(DebugDraw draw) и DebugRenderSelected(DebugDraw draw). Первый вызывается в редакторе и в игре при Application.IsDebug (IsDebug=true в разделе Application файла настроек). Второй дополнительно вызывается только в редакторе для каждого выделенного объекта и его компонентов, включая множественное выделение. Базовый метод для обслуживания компонентов вызывать здесь не требуется: движок вызывает их отдельно.

DebugDraw находится в ProjectShockGL.Rendering. Доступны Line(from, to, color), Box(position, rotation, size, color), Sphere(position, radius, color) и Billboard(position, size, texture, color). Координаты мировые, size куба — полный размер. Куб каркасный, сфера изображается тремя окружностями. Билборд обращён к текущей камере и имеет размер в мировых единицах. Последний необязательный параметр ignoreDepth=true позволяет рисовать поверх стен. Примитивы не освещаются и рисуются после постобработки мира, до UI.

```csharp
public override void DebugRenderSelected(DebugDraw draw)
{
    draw.Box(Transform.Position, Transform.Rotation,
        new Vector3(2f, 1f, 2f), Color.White);
    draw.Line(Transform.Position, Transform.Position + Transform.Forward * 2f,
        Color.White, ignoreDepth: true);
}
```

Отладочная отрисовка не запускает Start или Update. В редакторе она доступна для неактивных объектов и отключённых компонентов; скрытые Groups и уничтоженные объекты исключаются. В игре рисуются только активные объекты и включённые компоненты. Методы предназначены для рисования, игровую логику в них размещать не следует. Существующие отладочные RenderComponent и манипуляторы редактора продолжают работать отдельно.

### Загрузка уровня

LevelManager.Load("MyLevel") читает Levels/MyLevel.map; можно передать Level. Доступны Loading/Loaded/Unloading/Unloaded, ActiveLevel, IsLoading и Unload(). В Loaded объекты и Environment уже назначены, но Start ещё не выполнен — здесь удобно создавать игрока.

Загрузка синхронная и подготавливает новый мир до замены старого. Вложенные переходы из обработчика загрузки запрещены. Подписки на события нужно снимать при Unload; пример находится в [ProjectDungeon/Core/Game.cs](ProjectDungeon/Core/Game.cs).

### Камеры и CameraComponent

Application при инициализации окна создаёт запасную Camera.Default, которая не принадлежит World и сохраняется при смене уровня. Camera.Active — текущая камера. Camera.UseDefault() явно возвращает запасной вид. В редакторе запасной камерой управляет SceneCamera.

Для камеры игрового объекта добавьте CameraComponent в Create:

```csharp
_cameraComponent = AddComponent<CameraComponent>();
_cameraComponent.LocalPosition = new System.Numerics.Vector3(0, 0.6f, 0);
```

В игровом Start настройте `_cameraComponent.Camera.Fov` (радианы), Near/Far и остальные параметры, затем вызовите `_cameraComponent.MakeActive()`. Создание компонента само по себе вид не переключает. MakeActive возвращает false для выключенного/уничтоженного владельца или компонента, освобождённой камеры и в режиме редактора.

LocalPosition задаётся в локальных координатах владельца с учётом его масштаба и вращения. LocalRotation — локальный Quaternion; мировое вращение равно Owner.Rotation * LocalRotation. Например, персонаж вращает свой объект по yaw, а наклон взгляда задаёт через LocalRotation камеры. Компонент владеет отдельным экземпляром Camera; запись в Camera.Active не требуется.

Камеры автоматически синхронизируются непосредственно перед отрисовкой в Application, после завершения обновления игровой логики и физики. В Game.Update синхронизация не выполняется. Если игровому коду нужен актуальный трансформ раньше, вызовите Synchronize(). Отключение IsEnabled, SetActive(false), удаление компонента, уничтожение владельца или выгрузка уровня возвращают активный вид к Camera.Default. Уничтожение неактивной камеры не переключает текущий вид. Повторное включение само по себе не активирует камеру: вызовите MakeActive явно. Персонажи примера и ProjectA делают это при повторном включении.

FramebufferResize обновляет пропорции запасной и активной камеры; при переключении неактивная получает актуальный AspectRatio. Нулевой framebuffer при сворачивании игнорируется. Camera.Create/ToggleTo остаются низкоуровневым API, но для камер объектов используйте компонент, чтобы жизненный цикл был связан с владельцем.
## Время, таймеры и корутины

### Источники времени

Текущий API находится в [Time.cs](ProjectShockGL/Time.cs), а не в Application:

| Член Time | Назначение |
| --- | --- |
| DeltaSeconds | Шаг кадра с учётом TimeScale |
| UnscaledDeltaSeconds | Независимый шаг кадра |
| ElapsedTime | Накопленное игровое время |
| UnscaledElapsedTime | Накопленное независимое время |
| TimeScale | 1 — обычная скорость, 0.5 — замедление, 0 — остановка игрового времени |

Application.AdvanceTime вычисляет эти значения из времени окна. Отрицательный или нечисловой TimeScale запрещён. Масштаб времени сам по себе не отключает ввод и произвольную логику.

Core.Game.Update масштабирует реальный шаг кадра один раз: `deltaSeconds * Time.TimeScale`. World, корутины и игровые таймеры получают игровой шаг; независимые задержки и UI — исходный шаг кадра. При TimeScale=0 игровой отсчёт стоит, независимый продолжается. Объекты получают Update с нулевым шагом, поэтому ввод и действия, не зависящие от времени, сами по себе не блокируются.

### Timer

```csharp
using Timer = ProjectShockGL.Core.Timer;

var timer = new Timer(useUnscaledTime: true);
timer.Finished += () => Console.WriteLine("Готово");
timer.Run(2f);
// Храните timer у владельца; при уничтожении владельца вызовите timer.Dispose().
```

В игровом объекте храните таймер в поле и вызывайте Dispose при уничтожении. Его обновляет менеджер из Game; сам Timer не является объектом мира.

- Run запускает или перезапускает без повторных подписок.
- SetRemainingSeconds меняет остаток, RemainingSeconds читает его.
- SetPaused приостанавливает; IsRunning() остаётся true, IsPaused сообщает о паузе.
- Stop отменяет без Finished; Complete завершает работающий таймер с событием.
- При завершении остаток равен 0; сначала ValueChanged(0), затем Finished. Перезапуск или отмена из ValueChanged подавляет устаревший Finished.
- Dispose освобождает подписки; повторное использование запрещено.

### Coroutine

Методы Object: StartCoroutine(IEnumerator), StopCoroutine(handle), StopAllCoroutines(). У выполнения доступны Stop(), IsCompleted и IsCancelled.

Пример метода внутри нестатичного наследника Object/Entity, с using System.Collections и System.Numerics:

```csharp
private IEnumerator Raise()
{
    yield return new WaitForSeconds(0.3f, useUnscaledTime: true);
    var start = Transform.Position;
    var target = start + new Vector3(0, 2, 0);
    var elapsed = 0f;
    while (elapsed < 1f)
    {
        yield return null;
        elapsed += ProjectShockGL.Time.UnscaledDeltaSeconds;
        Transform.Position = Vector3.Lerp(start, target, Math.Clamp(elapsed, 0f, 1f));
    }
    Transform.Position = target;
}
// Запуск из игрового метода: StartCoroutine(Raise());
```

Для игрового времени используйте WaitForSeconds(seconds) и Time.DeltaSeconds в анимации; для независимого — useUnscaledTime=true и UnscaledDeltaSeconds. Это прямое изменение трансформа, без проверки пути на столкновения.

StartCoroutine сразу выполняет код до первого ожидания. null и нулевая задержка ждут следующего обновления. Можно возвращать вложенный IEnumerator или Coroutine. Продолжение выполняется после обновления объектов мира, до TimerManager и Physics. Точность ожиданий ограничена кадром.

Неактивный владелец приостанавливает выполнение, включение продолжает. Уничтожение, Stop и выгрузка мира отменяют последовательности с освобождением вложенных итераторов и выполнением finally. Корутины статичных объектов тоже обслуживаются; в редакторе запуск запрещён. Ошибка одной корутины журналируется и завершает её. Это не потоки: тяжёлый код без yield блокирует кадр.

## Физика и персонаж

Поддерживаются BoxCollider, SphereCollider и CapsuleCollider, триггеры, Enter/Exit, слои и запросы Raycast/OverlapSphere/OverlapSphereAll/OverlapCapsule. PhysicsQueryFilter позволяет исключить IgnoreObject, IgnoreCollider и IgnoreTriggers. Для движения и запросов учитывайте маски слоёв; для событий столкновений проверяются CollidesWith обеих сторон.

CharacterController — капсула с Move(displacement), а не полноценное физическое тело. Метод принимает смещение за кадр; гравитацию и скорость определяет игра. Доступны SkinWidth, CollisionMask, StepOffset, SlopeLimit, IsGrounded, GroundNormal, CollisionFlags и LastDisplacement.

После движения объектов Physics.Update вытесняет даже стоящего персонажа из твёрдых коробок, сфер и капсул. Собственные коллайдеры, триггеры и выключенные объекты исключаются. Используется skin-допуск и ограниченное число попыток; после коррекции обновляется опора. LastDisplacement относится к последнему явному Move.

Нет масс и сил, переноса персонажа платформой или политики зажатия дверью. Объект, целиком прошедший через персонажа за кадр, может остаться незамеченным. При нулевом игровом шаге базовый Game пропускает автоматический Physics.Update; явные запросы остаются доступны.

## Отображение

### Свет и окружение

Light поддерживает Directional, Point и Spot. Inspector показывает редактируемые параметры света, включая Color, Intensity, Radius и внутренний/внешний углы конуса Spot (InnerAngle/OuterAngle); реализация в [Light.cs](ProjectShockGL/Core/Light.cs). Выбранный Point показывает радиус, Spot — конус, Directional — направление. Свет выключенного объекта не участвует в сцене.

Environment хранит AmbientColor, IsFogEnabled, FogColor, FogNear и FogFar; редактор сохраняет их в map, загрузчик применяет при смене уровня. Динамических теней и запекания света нет.

### Шейдер объекта

Назначайте программу RenderComponent; отдельная система материалов не требуется:

```csharp
mesh.Shader = Content.LoadShaderProgram("Deform", "Lit");
mesh.ShaderParameters.Set("uAmplitude", 0.15f);
```

Здесь mesh — ваш MeshComponent, а Deform.vert/Lit.frag находятся в Shaders. Параметры поддерживают float/int/bool, Vector2/3/4, Color и Matrix4x4. Remove и Clear удаляют переопределения. Имена uniform должны соответствовать GLSL; матрицы, текстура, освещение и прочие служебные значения передаются рендерером.

В ресурсах примера есть [Deform.vert](StarterContent/Shaders/Deform.vert) и [Pulse.frag](StarterContent/Shaders/Pulse.frag). Для Pulse можно использовать пару Unlit/Pulse и параметры uPulseColor, uPulseSpeed, uPulseStrength. Деформация изображения не изменяет физический коллайдер и границы объекта.

### SimplePBR: упрощённое освещение поверхности

В Content примера добавлен Shaders/SimplePBR.frag. Он совместим с Lit.vert и Deform.vert, Directional/Point/Spot Light, Ambient, туманом, цветом и текстурой объекта. Обычный Lit не заменяется. Назначение MeshComponent:

```csharp
mesh.Shader = Content.LoadShaderProgram("Lit", "SimplePBR");
mesh.ShaderParameters.Set("uRoughness", 0.6f);
mesh.ShaderParameters.Set("uMetallic", 0.0f);
```

Roughness задаёт шероховатость (0..1, для устойчивости вычислений минимум ограничен 0.08), Metallic — долю металлического поведения (0..1). Значения по умолчанию: 0.6 и 0.0. Параметры отдельных объектов не переносятся на следующие отрисовки.

Модель использует GGX-блик, Schlick Fresnel и уменьшение диффузной составляющей для металлов. Радиусы, затухание и конусы света соответствуют существующему освещению движка. Unlit-предпросмотр показывает исходный цвет без освещения. Alpha cutout и туман работают по соглашению Lit.

Это PBR-подобное прямое освещение, без карт roughness/metallic, normal maps, отражений, карт окружения, IBL и отдельного HDR/sRGB-конвейера. Ambient применяется только к неметаллической диффузной составляющей: металл без прямого света может быть чёрным. Яркость и блики отличаются от обычного Lit; начните с Metallic=0 и подберите Roughness. Нормали поступают из выбранного вершинного шейдера; SimplePBR.frag не меняет его обработку неравномерного масштаба.

Проверка компиляции и изображения в скрытом OpenGL-окне: `dotnet run --project tests/PostProcessingChecks -- . --simple-pbr`.
### Постобработка

Профили — JSON в PostProcessProfiles. Пример собственного профиля:

```json
{
  "Enabled": true,
  "VertexShader": "PostProcess",
  "FragmentShader": "PostProcess",
  "Parameters": {
    "uVignetteStrength": 0.45,
    "uVignetteRadius": 0.6,
    "uVignetteSoftness": 0.5,
    "uGrainStrength": 0.0,
    "uGrainSize": 1.0,
    "uChromaticAberrationPixels": 0.0
  }
}
```

```csharp
ProjectShockGL.Rendering.PostProcessSettings.Apply("Default");
```

Профиль копирует параметры в активное состояние с преобразованием типов по uniform шейдера. Ошибка применения не должна частично заменить активный профиль. Можно загрузить описание через Content.LoadPostProcessProfile и передать его в Apply. Ресурсы кешируются; изменение JSON на диске требует перезапуска или выгрузки Content.

Эффект применяется к миру. UI, редакторский интерфейс и overlay рисуются после него. Доступны виньетка, film grain и chromatic aberration; их сила задаётся профилем. Нулевые значения отключают соответствующие эффекты; текущий Default в примере игры может использовать ненулевые значения. Bloom отсутствует.

## UI и текст

Game создаёт один sealed UIManager. Он создаёт один Canvas по Game/UICanvasClass; без настройки используется обычный Canvas. HUD, инвентарь и терминалы можно организовать как дочерние панели одного Canvas.

Canvas.Add<T>() создаёт корневой Widget, Widget.Add<T>() — дочерний. У корневого виджета Parent=null, но Canvas задан. SetParent меняет размещение внутри Canvas. RectTransform предоставляет Position, Size, Anchor, Pivot и вычисленную AbsolutePosition; ось Y интерфейса направлена вниз.

Виджет имеет Visible, IsEnabled и HitTestVisible. Жизненный цикл UI: OnCreate, OnUpdate, OnDraw, OnDestroy. Pointer у UIManager определяет наведение и нажатия; переопределяются OnMouseEnter/Exit, OnMousePressed/Released и OnMouseCancelled с MouseButton из Silk.NET.Input. Скрытие/удаление может отменить захваченное нажатие.

Пример в OnCreate собственного Canvas:

```csharp
var label = Add<ProjectShockGL.UI.Text>();
label.Rect.Position = new System.Numerics.Vector2(20, 20);
label.Rect.Size = new System.Numerics.Vector2(300, 60);
label.Font = ProjectShockGL.Content.LoadFont("NotoSans-Regular", 24);
label.Content = "Доступ разрешён / Access granted";
```

Text поддерживает растровый TTF-атлас, английский и русский текст, перенос строк, выравнивание и ClipToRect. Используйте шрифт с нужными глифами из Fonts; имя в примере предполагает Fonts/NotoSans-Regular.ttf. Font принадлежит кешу Content, отдельный Text его не освобождает. Следите за лицензией шрифта. UI обновляется на независимом времени.

## Ресурсы и настройки

### Структура Content

| Папка | Содержимое |
| --- | --- |
| Entities | JSON с Class и Parameters; поддерживаются вложенные папки |
| Geometry | JSON с Model, Texture, ColliderType, Size и другими параметрами |
| Meshes | OBJ-модели |
| Textures | JPG/PNG |
| Fonts | TTF и лицензии |
| Shaders | GLSL .vert/.frag |
| PostProcessProfiles | JSON-профили эффектов |
| Levels | JSON-уровни с расширением .map |

Имена передаются относительно соответствующей папки, обычно без расширения: Entity.Create("Placeables/TimerEvent"), LevelManager.Load("MyLevel"), Content.LoadFont("NotoSans-Regular", 24). Текстуры используют отдельный TextureFormat. Для кешируемых ресурсов освобождение выполняет Content.UnloadAll; не удаляйте общий ресурс из одного объекта.

Уровень сохраняет размещения, трансформы, активность, Editable-свойства, Outputs, параметры brush, окружение и Groups. Текущее выполнение корутин и таймеров не является сохранением игры и в map не записывается.

### GameSettings.ini, EditorSettings.ini и окно

```ini
[Window]
Mode=Windowed
Width=1280
Height=720
CanResize=true
Icon=Content/Textures/Game.png

[Application]
IsEditor=false
IsDebug=false

[Game]
Name=MyGame
Class=MyGame.MyGame, MyGame
UICanvasClass=MyGame.GameCanvas, MyGame
PostProcessingProfile=Default

[Content]
Path=Content
```

Windowed использует Width/Height. Borderless использует размер основного монитора, скрывает рамку и не требует ручного разрешения. Это исходные настройки приложения; отдельной системы пользовательских настроек пока нет.

Icon — изображение для окна, путь относительно приложения. Иконка exe задаётся отдельно через ApplicationIcon в csproj и ICO-файл: изменение INI не пересобирает значок exe. В репозитории эти файлы уже настроены для игры и редактора.

Game/Class и UICanvasClass читает движок. StartupLevel, PlayerClass и PlayerCharacterClass читает архитектура ProjectOxygenGL: собственная игра не обязана использовать эти ключи. Layers.ini задаёт именованные слои; пример находится в [ProjectShockGLEditor/Layers.ini](ProjectShockGLEditor/Layers.ini).

## Проверки и ограничения

Примеры команд из корня:

```sh
dotnet run --project tests/LifecycleChecks
dotnet run --project tests/PhysicsChecks
dotnet run --project tests/EditorChecks -- --imgui
dotnet run --project tests/UIChecks
dotnet run --project tests/TextChecks
dotnet run --project tests/CoroutineChecks
dotnet run --project tests/InputChecks
dotnet run --project tests/MovementChecks
dotnet run --project tests/TimerChecks
dotnet run --project tests/PostProcessingChecks -- .
```

Проверки OpenGL требуют подходящего драйвера; PostProcessingChecks создаёт скрытое окно. EditorChecks с --imgui проверяет реальные события ImGui без видимого окна. После изменения API тестовые программы также могут требовать обновления; проверка актуальности примеров README не означает запуск всех тестов.

Текущие ограничения:

- Нет множественного выделения объектов, встроенного Play и сложного редактирования граней brush.
- Нет статического батчинга, теней, запекания света и Bloom.
- Нет звуковой подсистемы, частиц и скелетной анимации.
- Нет полноценной динамики твёрдых тел и переноса на движущихся платформах.
- Нет отдельной системы материалов, твинов и сохранения состояния игрового сеанса.






### Общая поставка игры и редактора

Игра и отдельный запускатель редактора могут публиковаться рядом: ProjectA.exe, ProjectAEditor.exe, их DLL/deps/runtimeconfig, GameSettings.ini, EditorSettings.ini, Layers.ini, Icons и общий Content. Оба INI используют относительный Path=Content без CustomPath. В EditorSettings.ini обязательны Application/IsEditor=true и редакторский Game/Class; GameSettings.ini выбирает Game игры. Разные имена настроек исключают перезапись конфигурации игры конфигурацией редактора.

Проект запуска редактора может ссылаться на игру для доступа к её Entity-классам. Игровой Game и редакторский Game запускаются раздельно. После изменения C# обновляйте комплект и перезапускайте приложения; изменение ресурсов на диске учитывает правила кеширования Content. Публикация с self-contained включает .NET, но все DLL и JSON запуска нужно хранить вместе с exe.

### Подключение редактора к следующей игре

В решении новой игры создайте отдельный исполняемый проект, например MyGame.Editor, со ссылками на MyGame.csproj и ProjectShockGLEditor.csproj. Файлы движка копировать не нужно. Добавьте существующие проекты движка в решение, если хотите редактировать их в Rider. ProjectOxygenGL уже является зависимостью редактора.

Точка входа MyGame.Editor:

```csharp
using ProjectShockGL;
using ProjectShockGLEditor;
using Game = ProjectShockGL.Core.Game;

Settings.Initialize(isEditor: true);
Game.Initialize();
Editor.Initialize();
Application.Run();
```

Добавьте EditorSettings.ini с копированием в вывод и публикацию. В нём задайте Application/IsEditor=true, Game/Class=ProjectShockGLEditor.Game, ProjectShockGLEditor, Game/PostProcessingProfile=Default и Content/CustomPath к исходному Content новой игры. У игры остаётся отдельный GameSettings.ini. В редакторе создаются классы из той игры, на которую ссылается её запускатель.

Чтобы ресурсы сохраняли структуру при копировании между проектами, явно задавайте у Content метаданные:

```xml
<Content Include="Content\**\*.*">
    <Link>Content\%(RecursiveDir)%(Filename)%(Extension)</Link>
    <TargetPath>Content\%(RecursiveDir)%(Filename)%(Extension)</TargetPath>
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
</Content>
```

Для разработки запускайте игру и её редактор из одного решения Rider с исходным Content. Для поставки используйте папку dist внутри проекта игры, общий вложенный Content и отдельные INI с пустым CustomPath. Публикуемые ресурсы — копии, поэтому правки из dist автоматически в исходники не возвращаются.

