using System.Reflection;
using Newtonsoft.Json.Linq;

namespace ProjectShockGL.UI;

[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public sealed class UIActionAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

public enum UIActionTarget { Widget, Global }

public sealed class UIActionCall
{
    public UIActionTarget Target { get; set; }
    public Guid TargetId { get; set; }
    public string Method { get; set; } = "";
    public JToken? Parameter { get; set; }

    public UIActionCall Copy() => new()
    {
        Target = Target, TargetId = TargetId, Method = Method, Parameter = Parameter?.DeepClone()
    };
}

public sealed class UIActionMethod
{
    internal MethodInfo Method { get; }
    public string Id { get; }
    public string Name { get; }
    public Type? ParameterType { get; }
    public string Label => Name + (ParameterType == null ? "()" : $"({ParameterType.Name})");

    internal UIActionMethod(MethodInfo method)
    {
        Method = method;
        ParameterType = method.GetParameters().FirstOrDefault()?.ParameterType;
        Name = method.GetCustomAttribute<UIActionAttribute>()!.Name;

        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = method.Name;
        }
        
        var type = method.DeclaringType!;
        
        Id = $"{type.FullName}, {type.Assembly.GetName().Name}::{method.Name}({ParameterType?.FullName})";
    }

    public JToken? DefaultParameter() => ParameterType == null ? null :
        ParameterType == typeof(string) ? new JValue("") :
        ParameterType == typeof(bool) ? new JValue(false) :
        ParameterType == typeof(int) ? new JValue(0) : new JValue(0f);

    internal void Invoke(object? target, JToken? parameter)
    {
        object? value = null;
        
        if (ParameterType != null)
        {
            if (parameter == null)
            {
                throw new InvalidDataException("Action parameter is missing.");
            }

            if (ParameterType == typeof(string) && parameter.Type == JTokenType.String)
            {
                value = parameter.Value<string>();
            }
            else if (ParameterType == typeof(bool) && parameter.Type == JTokenType.Boolean)
            {
                value = parameter.Value<bool>();
            }
            else if (ParameterType == typeof(int) && parameter.Type == JTokenType.Integer)
            {
                value = parameter.Value<int>();
            }
            else if (ParameterType == typeof(float) && parameter.Type is JTokenType.Float or JTokenType.Integer)
            {
                var number = parameter.Value<float>();
                if (float.IsFinite(number)) value = number;
            }

            if (value == null)
            {
                throw new InvalidDataException($"Action requires a {ParameterType.Name} parameter.");
            }
        }
        
        Method.Invoke(target, ParameterType == null ? null : [value]);
    }
}

public static class UIActionCatalog
{
    private static readonly Dictionary<Type, UIActionMethod[]> WidgetMethods = [];
    private static UIActionMethod[]? _globals;
    private static readonly HashSet<Assembly> RegisteredAssemblies = [];

    static UIActionCatalog()
    {
        AppDomain.CurrentDomain.AssemblyLoad += (_, _) => _globals = null;
    }

    public static void RegisterAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        if (assembly.IsDynamic)
        {
            throw new ArgumentException("UI actions require a non-dynamic assembly.", nameof(assembly));
        }

        if (RegisteredAssemblies.Add(assembly))
        {
            _globals = null;
        }
    }
    private static bool Supported(MethodInfo method)
    {
        if (!method.IsPublic || method.IsAbstract || method.ContainsGenericParameters ||
            method.ReturnType != typeof(void) ||
            method.GetCustomAttribute<UIActionAttribute>() == null)
        {
            return false;
        }
        
        var parameters = method.GetParameters();
        
        return parameters.Length == 0 || parameters.Length == 1 && parameters[0].ParameterType is { } type &&
            (type == typeof(string) || type == typeof(int) || type == typeof(float) || type == typeof(bool));
    }

    public static IReadOnlyList<UIActionMethod> GetWidgetMethods(Type type)
    {
        if (!typeof(Widget).IsAssignableFrom(type))
        {
            return [];
        }
        
        if (!WidgetMethods.TryGetValue(type, out var methods))
        {
            methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public).Where(Supported)
                .Select(method => new UIActionMethod(method)).OrderBy(method => method.Label, StringComparer.Ordinal).ToArray();
            WidgetMethods.Add(type, methods);
        }
        
        return methods;
    }

    public static IReadOnlyList<UIActionMethod> GetGlobalMethods()
    {
        if (_globals != null)
        {
            return _globals;
        }
        
        var methods = new List<UIActionMethod>();
        
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Concat(RegisteredAssemblies).Distinct().Where(assembly => !assembly.IsDynamic))
        {
            Type[] types;

            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException error)
            {
                types = error.Types.OfType<Type>().ToArray();
            }
            
            foreach (var type in types.Where(type => type.IsVisible && !type.ContainsGenericParameters))
            {
                methods.AddRange(type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Where(Supported).Select(method => new UIActionMethod(method)));
            }
        }
        
        return _globals = methods.OrderBy(method => method.Label, StringComparer.Ordinal).ToArray();
    }

    internal static bool Execute(UIActionCall call, IReadOnlyDictionary<Guid, Widget> widgets, string sender)
    {
        try
        {
            Widget? target = null;
            IReadOnlyList<UIActionMethod> methods;

            if (call.Target == UIActionTarget.Global)
            {
                methods = GetGlobalMethods();
            }
            else if (call.Target == UIActionTarget.Widget)
            {
                if (!widgets.TryGetValue(call.TargetId, out target) || target.IsDestroyed)
                {
                    throw new InvalidOperationException("The action's target widget is missing or destroyed.");
                }
                
                methods = GetWidgetMethods(target.GetType());
            }
            else
            {
                throw new InvalidDataException("Unknown UI action target.");
            }
            
            var method = methods.FirstOrDefault(method => method.Id == call.Method)
                ?? throw new InvalidOperationException($"UIAction method '{call.Method}' is unavailable.");
            
            method.Invoke(target, call.Parameter);
            return true;
        }
        catch (Exception error)
        {
            if (error is TargetInvocationException { InnerException: { } inner })
            {
                error = inner;
            }
            
            Log.PrintError($"Click action '{call.Method}' on widget '{sender}' failed.", error);
            
            return false;
        }
    }
}
