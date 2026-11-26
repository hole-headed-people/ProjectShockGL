using System.Numerics;
using ImGuiNET;
using NativeFileDialogNET;
using ProjectShockGL;



namespace ProjectShockGLEditor;

public static class UserSettingsEditor
{
    private static UserSettingsDocument? _document;
    
    private static string? _error;
    private static Action? _pending;
    
    private static bool _askUnsaved;
    
    public static bool Open { get; set; }
    
    private static UserSettingsDocument Document => _document ??= CreateDocument();

    private static UserSettingsDocument CreateDocument()
    {
        var document = new UserSettingsDocument(Content.Path);
        return document;
    }

    public static void Toggle()
    {
        if (Open)
        {
            Request(() => Open = false);
        }
        else
        {
            Open = true;
        }
    }

    public static bool AllowClose()
    {
        if (_document?.IsDirty != true)
        {
            return true;
        }
        
        Open = true;
        Request(Application.Quit);
        
        return false;
    }

    public static void Dispose()
    {
        _document = null;
        
        _pending = null;
        _error = null;
        
        _askUnsaved = false;
        
        Open = false;
    }

    private static void Execute(Action action)
    {
        try
        {
            action();
            _error = null;
        }
        catch (Exception error)
        {
            _error = error.Message;
        }
    }

    private static void Request(Action action)
    {
        if (Document.IsDirty)
        {
            _pending = action; 
            _askUnsaved = true;
        }
        else
        {
            Execute(action);
        }
    }

    private static void Load()
    {
        Directory.CreateDirectory(Document.DirectoryPath);
        
        using var dialog = new NativeFileDialog().AddFilter("UserSettings JSON", "json");

        if (dialog.Open(out string? path, Document.DirectoryPath, "") == DialogResult.Okay &&
            !string.IsNullOrWhiteSpace(path))
        {
            Document.Open(path);
        }
    }

    private static bool Save(bool saveAs = false)
    {
        try
        {
            var path = Document.FilePath;

            if (saveAs || path == null)
            {
                Directory.CreateDirectory(Document.DirectoryPath);

                using var dialog = new NativeFileDialog().SaveFile().AddFilter("UserSettings JSON", "json");

                if (dialog.Open(out path, Document.DirectoryPath, "Default.json") != DialogResult.Okay ||
                    string.IsNullOrWhiteSpace(path))
                {
                    return false;
                }

                if (!Path.HasExtension(path))
                {
                    path += ".json";
                }
            }

            Document.Save(path);

            _error = null;

            return true;
        }
        catch (Exception error)
        {
            _error = error.Message; 
            
            return false;
        }
    }

    public static void Draw()
    {
        if (!Open)
        {
            return;
        }
        
        var document = Document;
        
        ImGui.SetNextWindowSize(new Vector2(780, 520), ImGuiCond.FirstUseEver);
        
        var open = true;
        var visible = ImGui.Begin("UserSettings Editor", ref open);

        if (!open)
        {
            Request(() => Open = false);
        }
        
        if (visible)
        {
            if (ImGui.Button("New"))
            {
                Request(document.New);
            }
            
            ImGui.SameLine();

            if (ImGui.Button("Open"))
            {
                Request(Load);
            }
            
            ImGui.SameLine();

            if (ImGui.Button("Save"))
            {
                Save();
            }
            
            ImGui.SameLine();

            if (ImGui.Button("Save As"))
            {
                Save(true);
            }
            
            ImGui.SameLine();
            
            ImGui.TextUnformatted(document.IsDirty ? "Modified" : "Saved");
            ImGui.TextWrapped(document.FilePath ?? "New user settings profile asset");
            ImGui.TextWrapped("Default values only. Configure [Game] UserSettingsDefaultProfile to use this profile.");
            
            ImGui.Separator();
            
            var remove = -1;
            if (ImGui.BeginTable("Options", 4, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
            {
                ImGui.TableSetupColumn("Name");
                ImGui.TableSetupColumn("Type");
                ImGui.TableSetupColumn("Default Value");
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 65);
                ImGui.TableHeadersRow();
                for (var i = 0; i < document.Options.Count; i++)
                {
                    var option = document.Options[i];
                    ImGui.PushID(i);
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    var name = option.Name;
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.InputText("##Name", ref name, 256)) option.Name = name;
                    ImGui.TableNextColumn();
                    var type = (int)option.Type;
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.Combo("##Type", ref type, "Int\0Float\0String\0Bool\0"))
                    {
                        option.Type = (UserSettingsOptionType)type;
                        option.DefaultValue = option.Type switch
                        {
                            UserSettingsOptionType.Float => (object)0f,
                            UserSettingsOptionType.String => "",
                            UserSettingsOptionType.Bool => false,
                            _ => 0
                        };
                    }
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1);
                    switch (option.Type)
                    {
                        case UserSettingsOptionType.Int:
                            var integer = (int)option.DefaultValue;
                            if (ImGui.InputInt("##Default", ref integer)) option.DefaultValue = integer;
                            break;
                        case UserSettingsOptionType.Float:
                            var number = (float)option.DefaultValue;
                            if (ImGui.InputFloat("##Default", ref number, 0, 0, "%.6g")) option.DefaultValue = number;
                            break;
                        case UserSettingsOptionType.String:
                            var text = (string)option.DefaultValue;
                            if (ImGui.InputText("##Default", ref text, (uint)Math.Max(4096, text.Length + 1024))) option.DefaultValue = text;
                            break;
                        case UserSettingsOptionType.Bool:
                            var boolean = (bool)option.DefaultValue;
                            if (ImGui.Checkbox("##Default", ref boolean)) option.DefaultValue = boolean;
                            break;
                    }
                    ImGui.TableNextColumn();
                    if (ImGui.SmallButton("Delete")) remove = i;
                    ImGui.PopID();
                }
                ImGui.EndTable();
            }
            if (remove >= 0) document.Options.RemoveAt(remove);
            if (ImGui.Button("Add Setting"))
            {
                var index = 1;
                while (document.Options.Any(option => option.Name == "Setting" + index)) index++;
                document.Options.Add(new UserSettingsDefaultOption { Name = "Setting" + index });
            }

            if (_error != null)
            {
                ImGui.TextWrapped(_error);
            }
        }

        if (_askUnsaved)
        {
            ImGui.OpenPopup("Unsaved user settings profile"); 
            _askUnsaved = false;
        }
        
        if (ImGui.BeginPopupModal("Unsaved user settings profile", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextUnformatted("Save changes to user settings profile?");

            if (ImGui.Button("Save") && Save())
            {
                Complete();
            }
            
            ImGui.SameLine();

            if (ImGui.Button("Discard"))
            {
                document.New(); 
                Complete();
            }
            
            ImGui.SameLine();

            if (ImGui.Button("Cancel"))
            {
                _pending = null; 
                ImGui.CloseCurrentPopup();
            }

            if (_error != null)
            {
                ImGui.TextWrapped(_error);
            }
            
            ImGui.EndPopup();
        }
        
        ImGui.End();
    }

    private static void Complete()
    {
        var action = _pending;
        _pending = null;
        
        ImGui.CloseCurrentPopup();

        if (action != null)
        {
            Execute(action);
        }
    }
}
