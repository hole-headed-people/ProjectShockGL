using System.Buffers.Binary;
using Silk.NET.OpenAL;

namespace ProjectShockGL.Audio;

public sealed class AudioClip : IDisposable
{
    private byte[] _samples;
    private uint _buffer;
    private uint _monoBuffer;
    
    public int Channels { get; }
    public int SampleRate { get; }
    public int BitsPerSample { get; }
    public int FrameCount { get; }
    public double Duration => (double)FrameCount / SampleRate;
    public bool IsDisposed { get; private set; }
    
    internal ReadOnlyMemory<byte> Samples => _samples;

    private AudioClip(byte[] samples, int channels, int sampleRate, int bits)
    {
        _samples = samples;
        
        Channels = channels;
        SampleRate = sampleRate;
        BitsPerSample = bits;
        
        FrameCount = samples.Length / (channels * (bits / 8));
    }

    public static AudioClip Load(string path)
    {
        using var stream = File.OpenRead(path);
        
        return ReadWave(stream);
    }

    internal static AudioClip ReadWave(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        
        string Tag() => System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));

        if (stream.Length - stream.Position < 12 || Tag() != "RIFF")
        {
            throw new InvalidDataException("AudioClip requires a little-endian RIFF WAV file.");
        }
        
        var end = stream.Position + 4L + reader.ReadUInt32();

        if (end > stream.Length || Tag() != "WAVE")
        {
            throw new InvalidDataException("Invalid or truncated WAV header.");
        }

        byte[]? format = null;
        byte[]? data = null;
        
        while (stream.Position + 8 <= end)
        {
            var tag = Tag();
            var length = reader.ReadUInt32();
            var start = stream.Position;

            if (length > int.MaxValue || start + length > end)
            {
                throw new InvalidDataException("Truncated WAV chunk.");
            }
            
            if (tag == "fmt ")
            {
                if (format != null || length > 4096)
                {
                    throw new InvalidDataException("Invalid WAV format chunk.");
                }
                
                format = reader.ReadBytes((int)length);
            }
            else if (tag == "data")
            {
                if (data != null || length > 256 * 1024 * 1024)
                {
                    throw new InvalidDataException(
                        "WAV clip must have one data chunk up to 256 MiB; streaming is not implemented.");
                }
                
                data = reader.ReadBytes((int)length);
            }

            stream.Position = start + length;
            
            if ((length & 1) != 0)
            {
                if (stream.Position >= end)
                {
                    throw new InvalidDataException("Missing WAV chunk padding.");
                }
                
                stream.Position++;
            }
        }

        if (stream.Position != end || format is not { Length: >= 16 } || data is not { Length: > 0 })
        {
            throw new InvalidDataException("WAV requires valid fmt and nonempty data chunks.");
        }

        var encoding = U16(0);
        var channels = U16(2);
        
        var rate = U32(4);
        
        var align = U16(12);
        var bits = U16(14);
        
        if (encoding == 0xfffe)
        {
            if (format.Length < 40 || U16(16) < 22 || U16(18) != bits ||
                !format.AsSpan(26, 14).SequenceEqual(new byte[] { 0, 0, 0, 0, 16, 0, 128, 0, 0, 170, 0, 56, 155, 113 }))
            {
                throw new InvalidDataException("Unsupported extensible WAV format.");
            }
            
            encoding = U16(24);
        }

        if (channels is not (1 or 2) || rate == 0 || rate > 384000 ||
            (encoding == 1 ? bits is not (8 or 16 or 24 or 32) : encoding != 3 || bits != 32) ||
            align != channels * (bits / 8) || U32(8) != (long)rate * align || data.Length % align != 0)
        {
            throw new InvalidDataException(
                "WAV must be mono/stereo PCM 8/16/24/32-bit or float32, with complete sample frames.");
        }

        if (encoding == 1 && bits is 8 or 16)
        {
            return new AudioClip(data, channels, (int)rate, bits);
        }
        
        var inputBytes = bits / 8;
        var converted = new byte[data.Length / inputBytes * 2];
        
        for (var i = 0; i < converted.Length / 2; i++)
        {
            var source = data.AsSpan(i * inputBytes, inputBytes);
            short sample;
            
            if (encoding == 3)
            {
                var value = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(source));
                
                if (!float.IsFinite(value))
                {
                    throw new InvalidDataException("WAV contains non-finite samples.");
                }
                
                sample = (short)Math.Clamp(Math.Round(value * 32768d), short.MinValue, short.MaxValue);
            }
            else if (bits == 24)
            {
                sample = (short)((source[0] | source[1] << 8 | source[2] << 16) >> 8);
            }
            else
            {
                sample = (short)(BinaryPrimitives.ReadInt32LittleEndian(source) >> 16);
            }

            BinaryPrimitives.WriteInt16LittleEndian(converted.AsSpan(i * 2), sample);
        }

        return new AudioClip(converted, channels, (int)rate, 16);

        uint U32(int offset)
        {
            return BinaryPrimitives.ReadUInt32LittleEndian(format.AsSpan(offset));
        }

        ushort U16(int offset)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(offset));
        }
    }

    internal uint GetBuffer(bool mono = false)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        Audio.RequireThread();

        if (mono && Channels == 2) return GetMonoBuffer();
        if (_buffer != 0)
        {
            return _buffer;
        }
        
        var al = Audio.Api;
        
        var format = (Channels, BitsPerSample) switch
        {
            (1, 8) => BufferFormat.Mono8, (1, 16) => BufferFormat.Mono16,
            (2, 8) => BufferFormat.Stereo8, _ => BufferFormat.Stereo16
        };
        
        var buffer = al.GenBuffer();
        
        try
        {
            al.BufferData(buffer, format, _samples, SampleRate);
            Audio.CheckError("upload WAV buffer");
            
            _buffer = buffer;
            Audio.RegisterBuffer(this);
            
            return buffer;
        }
        catch
        {
            al.DeleteBuffer(buffer);
            
            throw;
        }
    }

    private uint GetMonoBuffer()
    {
        if (_monoBuffer != 0) return _monoBuffer;
        var bytes = BitsPerSample / 8;
        var samples = new byte[_samples.Length / 2];
        for (var frame = 0; frame < FrameCount; frame++)
        {
            var input = frame * bytes * 2;
            var output = frame * bytes;
            if (bytes == 1) samples[output] = (byte)((_samples[input] + _samples[input + 1]) / 2);
            else
            {
                var left = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(_samples.AsSpan(input, 2));
                var right = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(_samples.AsSpan(input + 2, 2));
                System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(samples.AsSpan(output, 2), (short)((left + right) / 2));
            }
        }
        var buffer = Audio.Api.GenBuffer();
        try
        {
            Audio.Api.BufferData(buffer, bytes == 1 ? BufferFormat.Mono8 : BufferFormat.Mono16, samples, SampleRate);
            Audio.CheckError("upload mono WAV buffer");
            _monoBuffer = buffer;
            Audio.RegisterBuffer(this);
            return buffer;
        }
        catch
        {
            Audio.Api.DeleteBuffer(buffer);
            throw;
        }
    }

    internal void ReleaseBuffer()
    {
        if (_buffer == 0 && _monoBuffer == 0) return;
        Audio.RequireThread();
        if (_buffer != 0) Audio.Api.DeleteBuffer(_buffer);
        if (_monoBuffer != 0) Audio.Api.DeleteBuffer(_monoBuffer);
        _buffer = 0;
        _monoBuffer = 0;
    }
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }
        
        Audio.ReleaseClip(this);
        
        _samples = [];
        IsDisposed = true;
    }
}