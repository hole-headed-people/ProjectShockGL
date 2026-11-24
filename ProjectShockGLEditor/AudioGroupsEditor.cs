using System.Numerics;
using ImGuiNET;
using NativeFileDialogNET;
using ProjectShockGL;
using ProjectShockGL.Audio;
using AudioSystem = ProjectShockGL.Audio.Audio;

namespace ProjectShockGLEditor;

public static class AudioGroupsEditor
{
    private static AudioGroupsDocument? _document;
    
    private static string? _error;
    private static Action? _pending;
    
    private static bool _askUnsaved;
    
    public static bool Open { get; set; }
    
    private static AudioGroupsDocument Document => _document ??= CreateDocument();

    private static AudioGroupsDocument CreateDocument()
    {
        var document = new AudioGroupsDocument(Path.Combine(Content.Path, "Audio"));
        var name = Settings.GetString("Audio", "Groups", "");
        
        if (!string.IsNullOrWhiteSpace(name))
        {
            try
            {
                document.Open(Content.GetAudioGroupsPath(name));
            }
            catch (Exception error)
            {
                _error = error.Message;
            }
        }
        
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
        
        using var dialog = new NativeFileDialog().AddFilter("Audio Groups JSON", "json");

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

                using var dialog = new NativeFileDialog().SaveFile().AddFilter("Audio Groups JSON", "json");

                if (dialog.Open(out path, Document.DirectoryPath, "Mixer.json") != DialogResult.Okay ||
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

            var name = Settings.GetString("Audio", "Groups", "");

            if (!string.IsNullOrWhiteSpace(name) &&
                Path.GetFullPath(path).Equals(Content.GetAudioGroupsPath(name), StringComparison.OrdinalIgnoreCase))
            {
                AudioSystem.ApplyGroups(Document.Asset);
            }

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
        var visible = ImGui.Begin("Audio Groups Editor", ref open);

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
            ImGui.TextWrapped(document.FilePath ?? "New audio groups asset");
            ImGui.TextWrapped("Name / Parent / Volume. Configure [Audio] Groups in game and editor settings to load the same asset.");
            
            ImGui.Separator();
            
            var remove = -1;
            
            for (var i = 0; i < document.Asset.Groups.Count; i++)
            {
                var group = document.Asset.Groups[i];
                var master = group.Name.Trim().Equals("Master", StringComparison.OrdinalIgnoreCase) &&
                    i == document.Asset.Groups.FindIndex(row => row.Name.Trim().Equals("Master", StringComparison.OrdinalIgnoreCase));
                
                ImGui.PushID(i);
                
                var name = group.Name;
                
                ImGui.SetNextItemWidth(200);
                ImGui.BeginDisabled(master);
                
                if (ImGui.InputText("##Name", ref name, 256))
                {
                    foreach (var child in document.Asset.Groups)
                    {
                        if (string.Equals(child.Parent, group.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            child.Parent = name;
                        }
                    }
                    
                    group.Name = name;
                }
                
                ImGui.EndDisabled();
                
                ImGui.SameLine();
                ImGui.SetNextItemWidth(200);
                
                ImGui.BeginDisabled(master);
                
                if (ImGui.BeginCombo("##Parent", group.Parent ?? (master ? "(root)" : "Master")))
                {
                    foreach (var parent in document.Asset.Groups)
                    {
                        if (!ReferenceEquals(parent, group) &&
                            ImGui.Selectable(parent.Name, parent.Name == group.Parent))
                        {
                            group.Parent = parent.Name;
                        }
                    }
                    
                    ImGui.EndCombo();
                }
                
                ImGui.EndDisabled();
                
                ImGui.SameLine();
                
                var volume = group.Volume;
                
                ImGui.SetNextItemWidth(180);

                if (ImGui.SliderFloat("##Volume", ref volume, 0, 1, "%.3f"))
                {
                    group.Volume = volume;
                }
                
                ImGui.SameLine();
                
                ImGui.BeginDisabled(master);

                if (ImGui.SmallButton("Delete"))
                {
                    remove = i;
                }
                
                ImGui.EndDisabled();
                ImGui.PopID();
            }
            
            if (remove >= 0)
            {
                var removed = document.Asset.Groups[remove];

                foreach (var child in document.Asset.Groups)
                {
                    if (string.Equals(child.Parent, removed.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        child.Parent = removed.Parent ?? "Master";
                    }
                }
                
                document.Asset.Groups.RemoveAt(remove);
            }
            
            if (ImGui.Button("Add Group"))
            {
                var index = 1;

                while (document.Asset.Groups.Any(group =>
                           group.Name.Equals("Group" + index, StringComparison.OrdinalIgnoreCase)))
                {
                    index++;
                }
                
                document.Asset.Groups.Add(new AudioGroupData { Name = "Group" + index, Parent = "Master" });
            }

            if (_error != null)
            {
                ImGui.TextWrapped(_error);
            }
        }

        if (_askUnsaved)
        {
            ImGui.OpenPopup("Unsaved audio groups"); 
            _askUnsaved = false;
        }
        
        if (ImGui.BeginPopupModal("Unsaved audio groups", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextUnformatted("Save changes to audio groups?");

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
