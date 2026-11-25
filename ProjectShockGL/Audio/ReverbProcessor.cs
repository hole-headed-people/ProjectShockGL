using System.Runtime.InteropServices;

namespace ProjectShockGL.Audio;

// EFX entry points are resolved from the same OpenAL context as the normal sources.
internal sealed unsafe class ReverbProcessor : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Objects(int count, uint* objects);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Integer(uint handle, int parameter, int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Float(uint handle, int parameter, float value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SourceSend(uint source, int parameter, int slot, int send, int filter);
    private readonly Objects _deleteEffects, _deleteSlots;
    private readonly Integer _effectInteger, _slotInteger;
    private readonly Float _effectFloat, _slotFloat;
    private readonly SourceSend _sourceSend;
    private uint _effect, _slot;
    private ReverbSettings? _applied;
    internal uint Slot => _slot;

    private static T Load<T>(string name) where T : Delegate
    {
        var address = Audio.Api.GetProcAddress(name);
        if (address == 0) throw new NotSupportedException($"OpenAL EFX entry point {name} is unavailable.");
        return Marshal.GetDelegateForFunctionPointer<T>(address);
    }

    public ReverbProcessor()
    {
        var generateEffects = Load<Objects>("alGenEffects");
        var generateSlots = Load<Objects>("alGenAuxiliaryEffectSlots");
        _deleteEffects = Load<Objects>("alDeleteEffects");
        _deleteSlots = Load<Objects>("alDeleteAuxiliaryEffectSlots");
        _effectInteger = Load<Integer>("alEffecti");
        _slotInteger = Load<Integer>("alAuxiliaryEffectSloti");
        _effectFloat = Load<Float>("alEffectf");
        _slotFloat = Load<Float>("alAuxiliaryEffectSlotf");
        _sourceSend = Load<SourceSend>("alSource3i");
        try
        {
            uint effect = 0, slot = 0;
            generateEffects(1, &effect); _effect = effect;
            generateSlots(1, &slot); _slot = slot;
            _effectInteger(_effect, 0x8001, 0x0001); // AL_EFFECT_TYPE, AL_EFFECT_REVERB
            Apply(ReverbSettings.Silent);
            Audio.CheckError("initialize reverb");
        }
        catch { Dispose(); throw; }
    }

    public void Attach(uint source, bool enabled)
    {
        _sourceSend(source, 0x20006, enabled ? (int)_slot : 0, 0, 0); // AL_AUXILIARY_SEND_FILTER
        Audio.CheckError("configure reverb send");
    }

    public void Apply(ReverbSettings settings)
    {
        if (_applied == settings) return;
        if (_applied == null || _applied.Value with { Strength = settings.Strength } != settings)
        {
            _effectFloat(_effect, 0x0001, settings.Density);
            _effectFloat(_effect, 0x0002, settings.Diffusion);
            _effectFloat(_effect, 0x0003, .32f); // Reverb gain, wet strength is controlled by the slot.
            _effectFloat(_effect, 0x0004, settings.GainHF);
            _effectFloat(_effect, 0x0005, settings.DecayTime);
            _effectFloat(_effect, 0x0006, settings.DecayHFRatio);
            _slotInteger(_slot, 0x0001, (int)_effect); // AL_EFFECTSLOT_EFFECT copies the parameters.
        }
        if (_applied == null || _applied.Value.Strength != settings.Strength)
            _slotFloat(_slot, 0x0002, settings.Strength); // AL_EFFECTSLOT_GAIN
        Audio.CheckError("update reverb");
        _applied = settings;
    }

    public void Dispose()
    {
        var slot = _slot; _slot = 0;
        var effect = _effect; _effect = 0;
        if (slot != 0) _deleteSlots(1, &slot);
        if (effect != 0) _deleteEffects(1, &effect);
    }
}
