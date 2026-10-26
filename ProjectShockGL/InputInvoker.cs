using System.Globalization;
using System.Reflection;
using ProjectShockGL.Core;
using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGL;

public static class InputInvoker
{
    public static void Invoke(Object target, string name, string parameter)
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