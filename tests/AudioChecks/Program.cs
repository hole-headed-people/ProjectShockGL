using System.Buffers.Binary;
using ProjectShockGL;
using ProjectShockGL.Audio;
using ProjectShockGL.Core;
using Silk.NET.OpenAL;
using AudioSystem=ProjectShockGL.Audio.Audio;

// Exercise the actual native backend without sending test tones to the user's speakers.
var driver=args.Contains("--unavailable")?"missing-audio-driver":"null";
if(System.Environment.GetEnvironmentVariable("ALSOFT_DRIVERS")!=driver)
{
    // OpenAL's native runtime can read its environment before Main; set it before starting the test process.
    var start=new System.Diagnostics.ProcessStartInfo(System.Environment.ProcessPath!) {UseShellExecute=false};
    if(Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet",StringComparison.OrdinalIgnoreCase))
        start.ArgumentList.Add(System.Reflection.Assembly.GetExecutingAssembly().Location);
    foreach(var argument in args) start.ArgumentList.Add(argument);
    start.Environment["ALSOFT_DRIVERS"]=driver;
    using var child=System.Diagnostics.Process.Start(start)!;
    child.WaitForExit();System.Environment.Exit(child.ExitCode);return;
}
var root=Path.Combine(AppContext.BaseDirectory,"AudioCheckData",Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(root,"Audio","UI"));
File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"GameSettings.ini"),"[Content]\nCustomPath="+root);
Settings.Initialize();Content.Initialize(null!);

byte[] Wave(int channels=1,int bits=16,ushort encoding=1,byte[]? pcm=null,bool dataFirst=false,bool junk=false)
{
    pcm??=new byte[8000*channels*(bits/8)*2];
    using var body=new MemoryStream();using var writer=new BinaryWriter(body);
    void Chunk(string tag,byte[] data)
    {
        writer.Write(System.Text.Encoding.ASCII.GetBytes(tag));writer.Write(data.Length);writer.Write(data);
        if((data.Length&1)!=0) writer.Write((byte)0);
    }
    using var fmt=new MemoryStream();using(var fw=new BinaryWriter(fmt,System.Text.Encoding.ASCII,leaveOpen:true))
    {fw.Write(encoding);fw.Write((ushort)channels);fw.Write(8000);fw.Write(8000*channels*(bits/8));fw.Write((ushort)(channels*(bits/8)));fw.Write((ushort)bits);}
    if(junk) Chunk("JUNK",new byte[]{1,2,3});
    if(dataFirst) Chunk("data",pcm);
    Chunk("fmt ",fmt.ToArray());
    if(!dataFirst) Chunk("data",pcm);
    using var output=new MemoryStream();using var header=new BinaryWriter(output);
    header.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));header.Write((uint)body.Length+4);header.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));header.Write(body.ToArray());
    return output.ToArray();
}
AudioClip Read(byte[] bytes)=>AudioClip.ReadWave(new MemoryStream(bytes));
void Check(bool value){if(!value) throw new Exception("Audio check failed.");}
void Near(float a,float b){Check(Math.Abs(a-b)<.001f);}
void Throws<T>(Action action) where T:Exception {try{action();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name);}
void Run(string name,Action action){action();Console.WriteLine("PASS: "+name);}

Run("WAV supports mono/stereo, PCM 8/16/24/32 and float32 with chunk ordering and odd padding",()=>
{
    foreach(var channels in new[]{1,2}) foreach(var bits in new[]{8,16,24,32})
    {
        using var clip=Read(Wave(channels,bits,dataFirst:true,junk:true));
        Check(clip.Channels==channels && clip.SampleRate==8000 && clip.FrameCount==16000 && clip.Duration==2);
        Check(clip.BitsPerSample==(bits==8?8:16));
    }
    using var integer=Read(Wave(bits:24,pcm:new byte[]{0,0,128,255,255,127}));
    Check(BinaryPrimitives.ReadInt16LittleEndian(integer.Samples.Span)==short.MinValue);
    Check(BinaryPrimitives.ReadInt16LittleEndian(integer.Samples.Span[2..])==short.MaxValue);
    using var floating=Read(Wave(bits:32,encoding:3,pcm:BitConverter.GetBytes(-1f).Concat(BitConverter.GetBytes(2f)).ToArray()));
    Check(BinaryPrimitives.ReadInt16LittleEndian(floating.Samples.Span)==short.MinValue);
    Check(BinaryPrimitives.ReadInt16LittleEndian(floating.Samples.Span[2..])==short.MaxValue);
});
Run("Extensible PCM/float WAV retains channels and validates subformat and valid bits",()=>
{
    byte[] Extensible(ushort encoding)
    {
        var original=Wave(channels:2,bits:32,encoding:encoding);
        using var output=new MemoryStream();using var writer=new BinaryWriter(output);
        writer.Write(original[..16]);writer.Write(40);writer.Write(original[20..36]);
        writer.Write((ushort)22);writer.Write((ushort)32);writer.Write((uint)3);
        writer.Write((uint)encoding);writer.Write((ushort)0);writer.Write((ushort)16);
        writer.Write(new byte[]{128,0,0,170,0,56,155,113});writer.Write(original[36..]);
        var result=output.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4),(uint)result.Length-8);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(20),0xfffe);
        return result;
    }
    using var pcm=Read(Extensible(1));using var floating=Read(Extensible(3));
    Check(pcm.Channels==2 && pcm.BitsPerSample==16 && floating.FrameCount==16000);
    var bad=Extensible(1);bad[59]=0;
    Throws<InvalidDataException>(()=>Read(bad));
    bad=Extensible(1);BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(38),24);
    Throws<InvalidDataException>(()=>Read(bad));
});
Run("Invalid or truncated WAV data fails without creating audio resources",()=>
{
    Throws<InvalidDataException>(()=>Read(new byte[12]));
    var truncated=Wave()[..^1];Throws<InvalidDataException>(()=>Read(truncated));
    Throws<InvalidDataException>(()=>Read(Wave(channels:6)));
    Throws<InvalidDataException>(()=>Read(Wave(encoding:17)));
    Throws<InvalidDataException>(()=>Read(Wave(pcm:new byte[]{1})));
    Throws<InvalidDataException>(()=>Read(Wave(pcm:Array.Empty<byte>())));
    Throws<InvalidDataException>(()=>Read(Wave(bits:32,encoding:3,pcm:BitConverter.GetBytes(float.NaN))));
    Check(!AudioSystem.IsAvailable);
});

File.WriteAllBytes(Path.Combine(root,"Audio","UI","Test.wav"),Wave());
Run("Content caches clips with normalized resource names and rejects traversal",()=>
{
    var first=Content.LoadAudioClip("UI/Test")!;
    Check(ReferenceEquals(first,Content.LoadAudioClip("UI\\Test")));
    Check(Content.LoadAudioClip("Missing")==null);
    Throws<InvalidDataException>(()=>Content.LoadAudioClip("../Outside"));
    first.Dispose();Check(first.IsDisposed && first.Duration==2);
    Check(!ReferenceEquals(first,Content.LoadAudioClip("UI/Test")));
});

Run("Game and editor load the same configured Audio Groups without opening a sound device",()=>
{
    var path=Content.GetAudioGroupsPath("StartupMixer");
    File.WriteAllText(path,"""{"Groups":[{"Name":"Music","Volume":0.6},{"Name":"Effects","Volume":0.8}]}""");
    var ini="[Content]\nCustomPath="+root+"\n[Audio]\nGroups=StartupMixer\n";
    File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"GameSettings.ini"),ini);
    File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"EditorSettings.ini"),ini);
    Settings.Initialize();AudioSystem.LoadGroupsFromSettings();
    Near(AudioSystem.GetGroup("Music")!.Volume,.6f);Check(!AudioSystem.IsAvailable);
    Settings.Initialize(isEditor:true);AudioSystem.LoadGroupsFromSettings();
    Near(AudioSystem.GetGroup("Effects")!.Volume,.8f);Check(!AudioSystem.IsAvailable);
    Settings.Initialize();AudioSystem.ApplyGroups(new AudioGroupsAsset());
});
if(args.Contains("--unavailable"))
{
    Check(!AudioSystem.Initialize());
    using var source=AudioSystem.CreateSource(Content.LoadAudioClip("UI/Test")!);
    source.Play(1);AudioSystem.Update(1);
    Check(!source.IsPlaying);
    source.Stop();
    var shot=AudioSystem.PlayOneShot(Content.LoadAudioClip("UI/Test")!);Check(shot.IsDisposed);
    Content.UnloadAll();AudioSystem.Shutdown();
    Console.WriteLine("PASS: Missing audio output is logged and playback fails safely without stopping the game.");
    return;
}
Check(AudioSystem.Initialize());
Run("2D native sources have independent volume, loop, Play Pause Resume Stop and linear fades",()=>
{
    var clip=Content.LoadAudioClip("UI/Test")!;
    using var source=AudioSystem.CreateSource(clip);using var second=AudioSystem.CreateSource(clip);
    source.Loop=true;source.Volume=.5f;source.Play(fadeIn:1);
    Check(source.IsPlaying);Near(source.EffectiveVolume,0);
    AudioSystem.Update(.5f);Near(source.EffectiveVolume,.25f);
    AudioSystem.Api.GetSourceProperty(source.Handle,SourceFloat.Gain,out float gain);Near(gain,.25f);
    source.Volume=.8f;Near(source.EffectiveVolume,.4f);
    source.Pause();Check(source.IsPaused);AudioSystem.Update(1);Near(source.EffectiveVolume,.4f);
    source.Resume();Check(source.IsPlaying);AudioSystem.Update(.5f);Near(source.EffectiveVolume,.8f);
    second.Loop=true;second.Volume=.2f;second.Play();Near(second.EffectiveVolume,.2f);
    source.Stop(fadeOut:1);AudioSystem.Update(.25f);Near(source.EffectiveVolume,.6f);
    source.Play(fadeIn:1);Near(source.EffectiveVolume,.6f);AudioSystem.Update(.5f);Near(source.EffectiveVolume,.7f);
    var scale=Time.TimeScale;Time.TimeScale=0;
    try {source.Stop(.5f);AudioSystem.Update(.5f);Check(!source.IsPlaying);Near(source.Volume,.8f);}
    finally {Time.TimeScale=scale;}
    Check(second.IsPlaying);
    source.Play();source.Pause();source.Stop(3);Check(source.State==AudioPlaybackState.Stopped);
    Throws<ArgumentOutOfRangeException>(()=>source.Volume=1.1f);
    Throws<ArgumentOutOfRangeException>(()=>source.Play(float.NaN));
    Throws<ArgumentOutOfRangeException>(()=>AudioSystem.Update(-1));
    AudioSystem.CheckError("finish native source test");
});
Run("Components autoplay only in gameplay, pause with owner/enable and release on destruction",()=>
{
    var world=new World();var owner=world.Spawn<ProjectShockGL.Core.Object>();
    var component=owner.AddComponent<AudioSourceComponent>();component.Clip=Content.LoadAudioClip("UI/Test");component.Loop=true;
    world.Update(0);Check(component.IsPlaying);
    owner.SetActive(false);Check(component.IsPaused);
    component.IsEnabled=false;owner.SetActive(true);Check(component.IsPaused);
    component.IsEnabled=true;Check(component.IsPlaying);
    component.Pause();component.IsEnabled=false;component.IsEnabled=true;Check(component.IsPaused);
    component.Resume();Check(component.IsPlaying);
    world.Clear();Check(component.IsDestroyed && !component.IsPlaying);
    var secondWorld=new World();var off=secondWorld.Spawn<ProjectShockGL.Core.Object>().AddComponent<AudioSourceComponent>();
    off.Clip=Content.LoadAudioClip("UI/Test");off.Stop();secondWorld.Update(0);Check(!off.IsPlaying);secondWorld.Clear();
    var editor=typeof(Application).GetProperty(nameof(Application.IsEditor))!;var previous=Application.IsEditor;
    try
    {
        editor.SetValue(null,true);
        var preview=new World();var audio=preview.Spawn<ProjectShockGL.Core.Object>().AddComponent<AudioSourceComponent>();
        audio.Clip=Content.LoadAudioClip("UI/Test");preview.Update(0,simulate:false);audio.Play();Check(!audio.IsPlaying);preview.Clear();
    }
    finally {editor.SetValue(null,previous);}
});
Run("3D sources use world position, linear distance attenuation and mono buffers without changing 2D clips",()=>
{
    foreach(var bits in new[]{8,16})
    {
        using var clip=Read(Wave(channels:2,bits:bits));
        using var source=AudioSystem.CreateSource(clip);
        source.Loop=true;source.Play();
        AudioSystem.Api.GetBufferProperty(clip.GetBuffer(),GetBufferInteger.Channels,out int stereo);
        Check(stereo==2 && source.SpatialMode==AudioSpatialMode.TwoD);
        source.Position=new System.Numerics.Vector3(3,4,5);
        source.MinDistance=2;source.MaxDistance=12;
        AudioSystem.Api.SetSourceProperty(source.Handle,SourceFloat.SecOffset,.5f);
        source.Pause();source.SpatialMode=AudioSpatialMode.ThreeD;
        Check(source.IsPaused);
        AudioSystem.Api.GetSourceProperty(source.Handle,SourceFloat.SecOffset,out float offset);Near(offset,.5f);
        AudioSystem.Api.GetSourceProperty(source.Handle,SourceBoolean.SourceRelative,out bool relative);Check(!relative);
        AudioSystem.Api.GetSourceProperty(source.Handle,SourceVector3.Position,out System.Numerics.Vector3 position);Check(position==source.Position);
        AudioSystem.Api.GetSourceProperty(source.Handle,SourceFloat.ReferenceDistance,out float min);Near(min,2);
        AudioSystem.Api.GetSourceProperty(source.Handle,SourceFloat.MaxDistance,out float max);Near(max,12);
        AudioSystem.Api.GetSourceProperty(source.Handle,SourceFloat.RolloffFactor,out float rolloff);Near(rolloff,1);
        AudioSystem.Api.GetBufferProperty(clip.GetBuffer(true),GetBufferInteger.Channels,out int mono);Check(mono==1);
        source.Resume();source.SpatialMode=AudioSpatialMode.TwoD;Check(source.IsPlaying);
        AudioSystem.Api.GetSourceProperty(source.Handle,SourceBoolean.SourceRelative,out relative);Check(relative);
        AudioSystem.Api.GetSourceProperty(source.Handle,SourceVector3.Position,out position);Check(position==System.Numerics.Vector3.Zero);
        AudioSystem.Api.GetSourceProperty(source.Handle,SourceFloat.RolloffFactor,out rolloff);Near(rolloff,0);
        source.Stop();source.SpatialMode=AudioSpatialMode.ThreeD;Check(!source.IsPlaying);
        Throws<ArgumentOutOfRangeException>(()=>source.MinDistance=12);
        Throws<ArgumentOutOfRangeException>(()=>source.MaxDistance=2);
        Throws<ArgumentOutOfRangeException>(()=>source.Position=new System.Numerics.Vector3(float.NaN));
        Throws<ArgumentOutOfRangeException>(()=>source.SpatialMode=(AudioSpatialMode)999);
    }
    AudioSystem.CheckError("finish 3D source check");
});
Run("Listener follows only active camera and components follow owners even with TimeScale zero",()=>
{
    using var active=ProjectShockGL.Rendering.Camera.Create();
    using var surveillance=ProjectShockGL.Rendering.Camera.Create();
    active.Transform.Position=new System.Numerics.Vector3(7,8,9);
    active.Transform.Rotation=System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY,1);
    ProjectShockGL.Rendering.Camera.ToggleTo(active);
    var world=new World();var owner=world.Spawn<ProjectShockGL.Core.Object>();
    var component=owner.AddComponent<AudioSourceComponent>();
    component.Clip=Content.LoadAudioClip("UI/Test");component.SpatialMode=AudioSpatialMode.ThreeD;component.Loop=true;
    world.Update(0);
    var source=(AudioSource)typeof(AudioSourceComponent).GetField("_source",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(component)!;
    var scale=Time.TimeScale;
    try
    {
        Time.TimeScale=0;owner.Transform.Position=new System.Numerics.Vector3(10,11,12);
        surveillance.Transform.Position=new System.Numerics.Vector3(100);
        AudioSystem.SynchronizeSpatialAudio();
        AudioSystem.Api.GetListenerProperty(ListenerVector3.Position,out System.Numerics.Vector3 listener);
        Check(listener==active.Transform.Position);
        unsafe
        {
            float* orientation=stackalloc float[6];
            AudioSystem.Api.GetListenerProperty(ListenerFloatArray.Orientation,orientation);
            var forward=active.Transform.Forward;var up=active.Transform.Up;
            Near(orientation[0],forward.X);Near(orientation[1],forward.Y);Near(orientation[2],forward.Z);
            Near(orientation[3],up.X);Near(orientation[4],up.Y);Near(orientation[5],up.Z);
        }
        AudioSystem.Api.GetSourceProperty(source.Handle,SourceVector3.Position,out System.Numerics.Vector3 position);
        Check(position==owner.Transform.Position);
        // An unrelated capture camera cannot become the listener simply by being created or moved.
        surveillance.Transform.Position=System.Numerics.Vector3.Zero;
        AudioSystem.SynchronizeSpatialAudio();
        AudioSystem.Api.GetListenerProperty(ListenerVector3.Position,out listener);Check(listener==active.Transform.Position);
        ProjectShockGL.Rendering.Camera.UseDefault();AudioSystem.SynchronizeSpatialAudio();
        AudioSystem.Api.GetListenerProperty(ListenerVector3.Position,out listener);
        Check(listener==ProjectShockGL.Rendering.Camera.Default.Transform.Position);
    }
    finally {Time.TimeScale=scale;ProjectShockGL.Rendering.Camera.UseDefault();world.Clear();}
    AudioSystem.CheckError("finish listener check");
});Run("AudioEmitter loads clips before autoplay and its Inputs control playback without editor sound",()=>
{
    var world=new World();
    var emitter=world.Spawn<ProjectShockGL.Placeables.AudioEmitter>();
    emitter.ClipName="UI/Test";emitter.Loop=true;emitter.Volume=.4f;
    Check(emitter.Source.Clip==null && !emitter.Source.IsPlaying);
    world.Update(0);Check(emitter.Source.IsPlaying && emitter.SpatialMode==AudioSpatialMode.ThreeD);
    Check(ReferenceEquals(emitter.Source.Clip,Content.LoadAudioClip("UI/Test")));
    InputInvoker.Invoke(emitter,"Pause","");Check(emitter.Source.IsPaused);
    InputInvoker.Invoke(emitter,"Resume","");Check(emitter.Source.IsPlaying);
    InputInvoker.Invoke(emitter,"Stop","");Check(!emitter.Source.IsPlaying);
    InputInvoker.Invoke(emitter,"Play","");Check(emitter.Source.IsPlaying);
    emitter.ClipName="";Check(emitter.Source.Clip==null && !emitter.Source.IsPlaying);
    emitter.ClipName="UI/Test";Check(!emitter.Source.IsPlaying);
    InputInvoker.Invoke(emitter,"Play","");Check(emitter.Source.IsPlaying);
    world.Clear();Check(emitter.Source.IsDestroyed && !emitter.Source.IsPlaying);
    world=new World();emitter=world.Spawn<ProjectShockGL.Placeables.AudioEmitter>();
    emitter.ClipName="UI/Test";emitter.PlayOnStart=false;world.Update(0);Check(!emitter.Source.IsPlaying);
    InputInvoker.Invoke(emitter,"Play","");Check(emitter.Source.IsPlaying);world.Clear();
    var editor=typeof(Application).GetProperty(nameof(Application.IsEditor))!;var previous=Application.IsEditor;
    try
    {
        editor.SetValue(null,true);world=new World();emitter=world.Spawn<ProjectShockGL.Placeables.AudioEmitter>();
        emitter.ClipName="UI/Test";world.Update(0,simulate:false);InputInvoker.Invoke(emitter,"Play","");
        Check(emitter.Source.Clip==null && !emitter.Source.IsPlaying);world.Clear();
    }
    finally {editor.SetValue(null,previous);}
});Run("Stationary audio sends no spatial writes; moving sources send only position and timing resets on shutdown",()=>
{
    using var clip=Read(Wave());using var source=AudioSystem.CreateSource(clip);
    source.SpatialMode=AudioSpatialMode.ThreeD;source.Loop=true;source.Play();
    var writes=source.SpatialWriteCount;
    for(var i=0;i<10;i++)
    {
        source.Position=source.Position;source.MinDistance=source.MinDistance;source.MaxDistance=source.MaxDistance;
    }
    Check(source.SpatialWriteCount==writes);
    source.Position=new System.Numerics.Vector3(2,3,4);Check(source.SpatialWriteCount==writes+1);
    source.MaxDistance=30;Check(source.SpatialWriteCount==writes+2);
    AudioSystem.SynchronizeSpatialAudio();var listenerWrites=AudioSystem.ListenerWriteCount;
    for(var i=0;i<10;i++) AudioSystem.SynchronizeSpatialAudio();
    Check(AudioSystem.ListenerWriteCount==listenerWrites);
    using var camera=ProjectShockGL.Rendering.Camera.Create();
    camera.Transform.Position=ProjectShockGL.Rendering.Camera.Active.Transform.Position+System.Numerics.Vector3.UnitX;
    camera.Transform.Rotation=ProjectShockGL.Rendering.Camera.Active.Transform.Rotation;
    ProjectShockGL.Rendering.Camera.ToggleTo(camera);AudioSystem.SynchronizeSpatialAudio();
    Check(AudioSystem.ListenerWriteCount==listenerWrites+1);
    camera.Transform.Rotation=System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY,.5f);
    AudioSystem.SynchronizeSpatialAudio();Check(AudioSystem.ListenerWriteCount==listenerWrites+2);
    ProjectShockGL.Rendering.Camera.UseDefault();AudioSystem.SynchronizeSpatialAudio();
    AudioSystem.Update(.01f);
    Check(double.IsFinite(AudioSystem.UpdateTimeMs) && AudioSystem.UpdateTimeMs>=0);
    Check(double.IsFinite(AudioSystem.SpatialUpdateTimeMs) && AudioSystem.SpatialUpdateTimeMs>=0);
    Check(AudioSystem.TotalUpdateTimeMs==AudioSystem.UpdateTimeMs+AudioSystem.SpatialUpdateTimeMs);
    AudioSystem.Shutdown();Check(AudioSystem.TotalUpdateTimeMs==0);
    Check(AudioSystem.Initialize());
    using var fresh=AudioSystem.CreateSource(clip);fresh.Play();AudioSystem.SynchronizeSpatialAudio();
    Check(fresh.SpatialWriteCount==5 && AudioSystem.ListenerWriteCount==2);
});Run("Audio groups multiply parents with source volume and fades, update paused sources and reject cycles",()=>
{
    var effects=AudioSystem.CreateGroup("CheckEffects");
    var ambience=AudioSystem.CreateGroup("CheckAmbience",effects);
    var music=AudioSystem.CreateGroup("CheckMusic");
    using var clip=Read(Wave());using var source=AudioSystem.CreateSource(clip);
    using var defaultSource=AudioSystem.CreateSource(clip);
    source.GroupName=ambience.Name;source.Volume=.8f;source.Loop=true;source.Play(1);
    defaultSource.Loop=true;defaultSource.Play();
    try
    {
        AudioSystem.Master.Volume=.5f;effects.Volume=.7f;ambience.Volume=.5f;
        AudioSystem.Update(.5f);Near(source.EffectiveVolume,.07f);
        AudioSystem.Api.GetSourceProperty(source.Handle,SourceFloat.Gain,out float gain);Near(gain,.07f);
        Near(defaultSource.EffectiveVolume,.5f);
        source.Pause();effects.Volume=.4f;
        AudioSystem.Api.GetSourceProperty(source.Handle,SourceFloat.Gain,out gain);Near(gain,.04f);
        Check(source.IsPaused);Near(source.Volume,.8f);
        music.Volume=.25f;ambience.Parent=music;
        AudioSystem.Api.GetSourceProperty(source.Handle,SourceFloat.Gain,out gain);Near(gain,.025f);
        Throws<InvalidOperationException>(()=>music.Parent=ambience);
        Throws<InvalidOperationException>(()=>AudioSystem.Master.Parent=effects);
        Throws<ArgumentOutOfRangeException>(()=>effects.Volume=float.NaN);
        Throws<ArgumentOutOfRangeException>(()=>effects.Volume=1.1f);
        Throws<ArgumentException>(()=>AudioSystem.CreateGroup("checkeffects"));
        Check(ReferenceEquals(AudioSystem.GetGroup("CHECKAMBIENCE"),ambience));
        source.GroupName="CheckLater";Near(source.EffectiveVolume,.2f);
        var later=AudioSystem.CreateGroup("CheckLater");later.Volume=.1f;
        AudioSystem.Api.GetSourceProperty(source.Handle,SourceFloat.Gain,out gain);Near(gain,.02f);
        source.Resume();AudioSystem.Update(.5f);Near(source.EffectiveVolume,.04f);
        source.GroupName="";Near(source.EffectiveVolume,.4f);
        ambience.Parent=null;Check(ReferenceEquals(ambience.Parent,AudioSystem.Master));
        var world=new World();var emitter=world.Spawn<ProjectShockGL.Placeables.AudioEmitter>();
        emitter.ClipName="UI/Test";emitter.GroupName=effects.Name;world.Update(0);
        Check(emitter.Source.GroupName==effects.Name && emitter.Source.IsPlaying);
        emitter.GroupName=music.Name;Check(emitter.Source.GroupName==music.Name);world.Clear();
        AudioSystem.CheckError("finish audio group checks");
    }
    finally {AudioSystem.Master.Volume=1;}
});
Run("Audio Groups JSON loads through Content, validates before replacing live groups and keeps sources playing",()=>
{
    var path=Content.GetAudioGroupsPath("Checks/Mixer");Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path,"""{"Groups":[{"Name":"Ambience","Parent":"Effects","Volume":0.5},{"Name":"Master","Volume":0.8},{"Name":"Effects","Volume":0.5}]}""");
    using var clip=Read(Wave());using var source=AudioSystem.CreateSource(clip);
    source.GroupName="Ambience";source.Loop=true;source.Play();
    AudioSystem.ApplyGroups(Content.LoadAudioGroups("Checks/Mixer"));
    Near(source.EffectiveVolume,.2f);Check(source.IsPlaying);
    var master=AudioSystem.Master;var effects=AudioSystem.GetGroup("Effects")!;
    AudioSystem.Api.GetSourceProperty(source.Handle,SourceFloat.Gain,out float gain);Near(gain,.2f);
    foreach(var json in new[]{
        """{"Groups":[{"Name":"A","Parent":"Missing"}]}""",
        """{"Groups":[{"Name":"A","Parent":"B"},{"Name":"B","Parent":"A"}]}""",
        """{"Groups":[{"Name":"A"},{"Name":"a"}]}""",
        """{"Groups":[{"Name":"Master","Parent":"A"},{"Name":"A"}]}""",
        """{"Groups":[{"Name":"A","Volume":2}]}"""})
    {
        File.WriteAllText(path,json);Throws<InvalidDataException>(()=>AudioSystem.ApplyGroups(Content.LoadAudioGroups("Checks/Mixer")));
        Check(ReferenceEquals(effects,AudioSystem.GetGroup("Effects")));Near(source.EffectiveVolume,.2f);
    }
    Throws<InvalidDataException>(()=>Content.GetAudioGroupsPath("../Outside"));
    File.WriteAllText(path,"""{"Groups":[{"Name":"Effects","Volume":0.3}]}""");
    AudioSystem.ApplyGroups(Content.LoadAudioGroups("Checks/Mixer"));
    Check(ReferenceEquals(master,AudioSystem.Master) && ReferenceEquals(effects,AudioSystem.GetGroup("Effects")));
    Near(effects.Volume,.3f);Near(source.EffectiveVolume,1);Check(source.IsPlaying);
    AudioSystem.ApplyGroups(new AudioGroupsAsset());
    Throws<InvalidOperationException>(()=>effects.Parent=AudioSystem.Master);
    Near(AudioSystem.Master.Volume,1);
});
Run("Reverb zones use oriented Box/Sphere bounds, priorities, active listener and smooth unscaled EFX transitions",()=>
{
    Check(AudioSystem.IsReverbAvailable);
    var world=new World();var otherWorld=new World();
    var low=world.Spawn<ProjectShockGL.Placeables.AudioReverbZone>();
    low.Shape=ProjectShockGL.Placeables.ReverbZoneShape.Sphere;low.Radius=10;low.Preset=ProjectShockGL.Placeables.ReverbPreset.Room;
    var high=world.Spawn<ProjectShockGL.Placeables.AudioReverbZone>();
    high.BoxSize=new System.Numerics.Vector3(2);high.Priority=10;high.Preset=ProjectShockGL.Placeables.ReverbPreset.Cave;
    high.Transform.Rotation=System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY,.7f);
    high.Transform.Scale=new System.Numerics.Vector3(2,1,1);
    var local=System.Numerics.Vector3.Transform(new System.Numerics.Vector3(1.9f,0,0),high.Transform.Rotation);
    Check(high.Contains(local) && !high.Contains(local*2));
    Check(high.Preset==ProjectShockGL.Placeables.ReverbPreset.Cave);
    high.Priority=20;high.Radius=3;Check(high.Preset==ProjectShockGL.Placeables.ReverbPreset.Cave);
    high.DecayTime=3;Check(high.Preset==ProjectShockGL.Placeables.ReverbPreset.Custom);
    high.Preset=ProjectShockGL.Placeables.ReverbPreset.Cave;
    var unrelated=otherWorld.Spawn<ProjectShockGL.Placeables.AudioReverbZone>();unrelated.Priority=100;
    Check(ReferenceEquals(ProjectShockGL.Placeables.AudioReverbZone.Find(world,System.Numerics.Vector3.Zero),high));
    var owner=world.Spawn<ProjectShockGL.Core.Object>();var camera=owner.AddComponent<CameraComponent>();
    owner.Transform.Position=new System.Numerics.Vector3(20,0,0);Check(camera.MakeActive());
    using var clip=Read(Wave());using var source=AudioSystem.CreateSource(clip);source.Loop=true;source.Play();
    var scale=Time.TimeScale;
    try
    {
        AudioSystem.SynchronizeSpatialAudio(1);Near(AudioSystem.CurrentReverb.Strength,0);
        Time.TimeScale=0;owner.Transform.Position=System.Numerics.Vector3.Zero;camera.Synchronize();
        AudioSystem.SynchronizeSpatialAudio(.5f);Near(AudioSystem.CurrentReverb.Strength,.35f);
        AudioSystem.SynchronizeSpatialAudio(.5f);Near(AudioSystem.CurrentReverb.Strength,.7f);
        unsafe
        {
            var get=(delegate* unmanaged[Cdecl]<uint,int,float*,void>)(void*)AudioSystem.Api.GetProcAddress("alGetAuxiliaryEffectSlotf");
            float gain;get(AudioSystem.ReverbSlot,2,&gain);Near(gain,.7f);
        }
        source.ReverbEnabled=false;Check(source.IsPlaying && !source.ReverbEnabled);
        source.ReverbEnabled=true;Check(source.IsPlaying);
        using(var capture=ProjectShockGL.Rendering.Camera.Create())
        {
            capture.Transform.Position=new System.Numerics.Vector3(1000);
            AudioSystem.SynchronizeSpatialAudio(1);Near(AudioSystem.CurrentReverb.Strength,.7f);
        }
        high.SetActive(false);AudioSystem.SynchronizeSpatialAudio(.5f);Near(AudioSystem.CurrentReverb.Strength,.6f);
        AudioSystem.SynchronizeSpatialAudio(.5f);Near(AudioSystem.CurrentReverb.Strength,.5f);
        world.Destroy(high);Check(ReferenceEquals(ProjectShockGL.Placeables.AudioReverbZone.Find(world,System.Numerics.Vector3.Zero),low));
        owner.Transform.Position=new System.Numerics.Vector3(20,0,0);camera.Synchronize();
        AudioSystem.SynchronizeSpatialAudio(.5f);Near(AudioSystem.CurrentReverb.Strength,.25f);
        AudioSystem.SynchronizeSpatialAudio(.5f);Near(AudioSystem.CurrentReverb.Strength,0);
        Check(source.IsPlaying);AudioSystem.CheckError("finish reverb checks");
        Throws<ArgumentOutOfRangeException>(()=>low.DecayTime=0);
        Throws<ArgumentOutOfRangeException>(()=>low.Strength=float.NaN);
        Throws<ArgumentOutOfRangeException>(()=>AudioSystem.SynchronizeSpatialAudio(-1));
    }
    finally {Time.TimeScale=scale;ProjectShockGL.Rendering.Camera.UseDefault();world.Clear();otherWorld.Clear();}
});
Run("PlayOneShot applies settings, keeps paused sources, releases on completion/Stop and retains shared clips",()=>
{
    using var clip=Read(Wave());
    var group=AudioSystem.GetGroup("OneShots") ?? AudioSystem.CreateGroup("OneShots");group.Volume=.5f;
    var source=AudioSystem.PlayOneShot(clip,AudioSpatialMode.ThreeD,new System.Numerics.Vector3(1,2,3),
        volume:.8f,groupName:"OneShots",reverbEnabled:false,minDistance:30,maxDistance:50);
    Check(source.IsPlaying && !source.Loop && source.SpatialMode==AudioSpatialMode.ThreeD);
    Check(source.Position==new System.Numerics.Vector3(1,2,3) && source.MinDistance==30 && source.MaxDistance==50);
    Check(!source.ReverbEnabled && source.GroupName=="OneShots");Near(source.EffectiveVolume,.4f);
    source.Pause();AudioSystem.Update(10);Check(source.IsPaused && !source.IsDisposed);
    source.Resume();Check(source.IsPlaying);source.Stop();Check(source.IsDisposed && source.Handle==0 && !clip.IsDisposed);
    var fading=AudioSystem.PlayOneShot(clip);fading.Stop(1);AudioSystem.Update(.5f);
    Check(!fading.IsDisposed);AudioSystem.Update(.5f);Check(fading.IsDisposed && !clip.IsDisposed);
    var paused=AudioSystem.PlayOneShot(clip);paused.Pause();paused.Stop();Check(paused.IsDisposed);
    var manual=AudioSystem.CreateSource(clip);manual.Play();manual.Stop();Check(!manual.IsDisposed);manual.Dispose();
    using var shortClip=Read(Wave(pcm:new byte[16]));
    var completed=AudioSystem.PlayOneShot(shortClip);
    var wait=System.Diagnostics.Stopwatch.StartNew();
    while(!completed.IsDisposed && wait.Elapsed.TotalSeconds<2){Thread.Sleep(5);AudioSystem.Update(0);}
    Check(completed.IsDisposed && !shortClip.IsDisposed);
    var cached=Content.LoadAudioClip("UI/Test")!;var shared=AudioSystem.CreateSource(cached);shared.Loop=true;shared.Play();
    var shot=AudioSystem.PlayOneShot(cached);shot.Stop();
    Check(ReferenceEquals(cached,Content.LoadAudioClip("UI/Test")) && shared.IsPlaying && !cached.IsDisposed);shared.Dispose();
    var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
    var sources=(System.Collections.Generic.HashSet<AudioSource>)typeof(AudioSystem).GetField("Sources",flags)!.GetValue(null)!;
    var count=sources.Count;
    Throws<ArgumentOutOfRangeException>(()=>AudioSystem.PlayOneShot(clip,volume:2));
    Throws<ArgumentOutOfRangeException>(()=>AudioSystem.PlayOneShot(clip,minDistance:5,maxDistance:2));
    Check(sources.Count==count && !clip.IsDisposed);
    AudioSystem.CheckError("finish one-shot checks");
});
Run("Natural completion, Content unload and context restart release sources and buffers safely",()=>
{
    using(var shortClip=Read(Wave(pcm:new byte[16])))
    using(var shortSource=AudioSystem.CreateSource(shortClip))
    {
        shortSource.Play();var wait=System.Diagnostics.Stopwatch.StartNew();
        while(shortSource.IsPlaying && wait.Elapsed.TotalSeconds<2) Thread.Sleep(5);
        Check(shortSource.State==AudioPlaybackState.Stopped);
    }
    var clip=Content.LoadAudioClip("UI/Test")!;var source=AudioSystem.CreateSource(clip);source.Loop=true;source.Play();
    Content.UnloadAll();Check(source.IsDisposed && clip.IsDisposed);
    source.Dispose();clip.Dispose();
    using var retained=Read(Wave());var old=AudioSystem.CreateSource(retained);old.Play();
    AudioSystem.Shutdown();Check(old.IsDisposed && !AudioSystem.IsAvailable && !AudioSystem.IsReverbAvailable && AudioSystem.ReverbSlot==0 && !retained.IsDisposed);
    Check(AudioSystem.Initialize());using var fresh=AudioSystem.CreateSource(retained);fresh.Play();Check(fresh.IsPlaying);
    AudioSystem.CheckError("finish lifetime test");
});
Content.UnloadAll();AudioSystem.Shutdown();
Console.WriteLine("All audio checks passed (OpenAL Soft null output).");