using ProjectShockGL;
using ProjectShockGLEditor;
using Game = ProjectShockGL.Core.Game;

Settings.Initialize(isEditor: true);

Game.Initialize();
Editor.Initialize();
Application.Run();
