using System.Collections.Concurrent;
using Newtonsoft.Json.Serialization;

namespace QF.Integration.Common.Serialization;

public class CrossPlatformTypeBinder : DefaultSerializationBinder
{
    private static readonly bool s_isNetCore = Type.GetType("System.String, System.Private.CoreLib") != null;
    private readonly ConcurrentDictionary<string, Type> _mappedTypes = new();

    public override Type BindToType(string? assemblyName, string typeName)
    {
        _mappedTypes.TryGetValue(typeName, out var type);

        if (type != null)
            return type;

        var originalTypeName = typeName;

        if (assemblyName != null)
        {
            if (s_isNetCore)
            {
                typeName = typeName.Replace("mscorlib", "System.Private.CoreLib");
                assemblyName = assemblyName.Replace("mscorlib", "System.Private.CoreLib");
            }
            else
            {
                typeName = typeName.Replace("System.Private.CoreLib", "mscorlib");
                assemblyName = assemblyName.Replace("System.Private.CoreLib", "mscorlib");
            }
        }

        type = base.BindToType(assemblyName, typeName);
        _mappedTypes.TryAdd(originalTypeName, type);
        return type;
    }
}
