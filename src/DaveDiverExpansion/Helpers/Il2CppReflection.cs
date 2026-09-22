using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;

namespace DaveDiverExpansion.Helpers;

/// <summary>
/// Native Il2Cpp reflection helpers (ported from SuperDave's ReflectionUtils).
/// Use these to read/write game fields that are not exposed as interop properties.
/// </summary>
public static class Il2CppReflection
{
    public const BindingFlags BINDING_FLAGS_ALL = BindingFlags.Instance |
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.FlattenHierarchy | BindingFlags.InvokeMethod | BindingFlags.CreateInstance;

    public static Il2CppSystem.Reflection.FieldInfo GetField(Il2CppSystem.Object obj, string name)
    {
        return obj.GetIl2CppType().GetField(name, (Il2CppSystem.Reflection.BindingFlags)BINDING_FLAGS_ALL);
    }

    public static object GetFieldValue(Il2CppSystem.Object obj, string name)
    {
        return GetField(obj, name)?.GetValue(obj);
    }

    public static T GetFieldValue<T>(Il2CppSystem.Object obj, string name)
    {
        return (T)Marshal.PtrToStructure(
            IL2CPP.il2cpp_object_unbox(GetField(obj, name).GetValue(obj).Pointer),
            (typeof(T).IsEnum ? Enum.GetUnderlyingType(typeof(T)) : typeof(T))
        );
    }
}
