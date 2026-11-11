namespace ProjectShockGL.Core;

public interface IObjectStateWriter
{
    void Set(string key, object? value);
}

public interface IObjectStateReader
{
    T Get<T>(string key, T defaultValue = default!);
}
