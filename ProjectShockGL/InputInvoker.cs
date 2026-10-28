using System.Globalization;
using System.Reflection;
using ProjectShockGL.Core;
using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGL;

public static class InputInvoker
{
    public static void Invoke(Object target, string name, string parameter)
    {
        InvokeCore(target, name, parameter, null, false);
    }

    public static void Invoke(Object target, string name, string parameter, object? eventValue)
    {
        InvokeCore(target, name, parameter, eventValue, true);
    }

    private static void InvokeCore(Object target, string name, string parameter, object? eventValue, bool hasValue)
    {
        var method = target.GetType()
            .GetMethods()
            .FirstOrDefault(m => m.GetCustomAttribute<InputAttribute>()?.Name == name);

        if (method == null)
        {
            Console.WriteLine($"Input '{name}' not found on {target.Name}");
            return;
        }

        var tokens = string.IsNullOrWhiteSpace(parameter)
            ? []
            : parameter.Split(',');

        var parameters = method.GetParameters();
        
        if (hasValue && parameters.Length == 0)
        {
            InvokeMethod(target, name, method, []);
            
            return;
        }
        
        if (hasValue && parameters.Length == 1 &&
            TryConvertEventValue(eventValue, parameters[0].ParameterType, out var converted))
        {
            InvokeMethod(target, name, method, [converted]);
            
            return;
        }

        if (tokens.Length != parameters.Length)
        {
            Console.WriteLine($"Input '{name}' on {target.Name}: expected {parameters.Length} args, got {tokens.Length}");
            
            return;
        }

        var args = new object[parameters.Length];
        
        try
        {
            for (var i = 0; i < parameters.Length; i++)
            {
                args[i] = Convert.ChangeType(tokens[i].Trim(), parameters[i].ParameterType, CultureInfo.InvariantCulture);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Input '{name}' on {target.Name}: failed to convert args ({ex.Message})");
            
            return;
        }
        
        InvokeMethod(target, name, method, args);
    }

    private static bool TryConvertEventValue(object? value, Type type, out object? converted)
    {
        converted = value;

        if (value == null)
        {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }

        if (type.IsInstanceOfType(value))
        {
            return true;
        }
        
        type = Nullable.GetUnderlyingType(type) ?? type;
        
        static bool Numeric(Type t) => Type.GetTypeCode(t) is
            TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or
            TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or
            TypeCode.Single or TypeCode.Double or TypeCode.Decimal;

        if (type != typeof(string) && !(Numeric(value.GetType()) && Numeric(type)))
        {
            return false;
        }
        
        try
        {
            converted = Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
            
            return true;
        }
        catch (Exception error) when (error is InvalidCastException or FormatException or OverflowException)
        {
            return false;
        }
    }

    private static void InvokeMethod(Object target, string name, MethodInfo method, object?[] args)
    {
        try
        {
            method.Invoke(target, args);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Input '{name}' on {target.Name}: {ex.Message}");
        }
    }
    
    public static string[] GetInputNames(Type type)
    {
        return type.GetMethods()
            .Select(m => m.GetCustomAttribute<InputAttribute>()?.Name)
            .Where(n => n != null)
            .Cast<string>()
            .ToArray();
    }
}
