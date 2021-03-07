using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MirrorState.Scripts;
using MirrorState.Scripts.Generated.States;
using MirrorState.Scripts.Generation;
using Unity.Mathematics;

public enum EntityStateType
{
    Transform,
    Vector,
    Quaternion,
    Float,
    Integer,
    Bool,
    Trigger,
    // TODO:
    LocalTransform,
    //Rename to AnimFloat or have a checkbox for animations on EntityStateProperty
    Half,
    Short,
    UShort,
    Byte
}

[Serializable]
public class EntityStateProperty
{
    public string Name;
    /// <summary>
    /// Sets the animator automatically, triggers will call it on trigger as well.
    /// </summary>
    public bool IsAnim;

    /// <summary>
    /// If a Trigger is predicted by Client Authority? If not event can only be triggered by Server then propagated to all clients.
    /// </summary>
    public bool Predicted = true;

    /// <summary>
    /// Used by Transform and LocalTransform
    /// </summary>
    public bool Position = true;
    public bool Rotation = true;
    public bool Scale = true;
    public EntityStateType StateType;
}

/*[AttributeUsage(AttributeTargets.Field)]
public class StateInterfaceAttribute : PropertyAttribute
{

}*/

[Serializable]
public class EntityStateInterface
{
    public string Name;

    private static List<Type> GetInterfaces()
    {
        return (from domainAssembly in AppDomain.CurrentDomain.GetAssemblies()
                from assemblyType in domainAssembly.GetTypes()
                where assemblyType.IsInterface && typeof(IStateBase).IsAssignableFrom(assemblyType) && assemblyType != typeof(IStateBase)
                select assemblyType).ToList();
    }

    public static List<Type> GetInterfaces(List<string> interfaces)
    {
        return Interfaces.Where(x => interfaces.Contains(x.Name)).ToList();
    }

    public static List<Type> GetInterfaces(List<EntityStateInterface> interfaces)
    {
        var list = interfaces.Select(x => x.Name).ToList();
        return GetInterfaces(list);
    }

    public static readonly List<Type> Interfaces = GetInterfaces();

    public static readonly Dictionary<string, int> InterfaceIndex = Interfaces
        .Select((item, index) => new { item.Name, Index = index })
        .ToDictionary(x => x.Name, x => x.Index);
    public static readonly string[] InterfaceNames = Interfaces.Select(x => x.Name).ToArray();
}

public class EntityStateScriptable : ScriptableObject
{
    public string Name;
    public bool Tracked;
    public List<EntityStateProperty> Fields;
    public List<EntityStateInterface> Interfaces;
    public static implicit operator bool(EntityStateScriptable cmd) => cmd != null;
}


/*
// http://answers.unity.com/answers/1463038/view.html
[Serializable]
public class SerializableType : ISerializationCallbackReceiver
{
    public Type type;
    public byte[] data;
    public SerializableType(Type aType)
    {
        type = aType;
    }

    public static Type Read(BinaryReader aReader)
    {
        var paramCount = aReader.ReadByte();
        if (paramCount == 0xFF)
            return null;
        var typeName = aReader.ReadString();
        var type = Type.GetType(typeName);
        if (type == null)
            throw new Exception("Can't find type; '" + typeName + "'");
        if (type.IsGenericTypeDefinition && paramCount > 0)
        {
            var p = new Type[paramCount];
            for (int i = 0; i < paramCount; i++)
            {
                p[i] = Read(aReader);
            }
            type = type.MakeGenericType(p);
        }
        return type;
    }

    public static void Write(BinaryWriter aWriter, Type aType)
    {
        if (aType == null)
        {
            aWriter.Write((byte)0xFF);
            return;
        }
        if (aType.IsGenericType)
        {
            var t = aType.GetGenericTypeDefinition();
            var p = aType.GetGenericArguments();
            aWriter.Write((byte)p.Length);
            aWriter.Write(t.AssemblyQualifiedName);
            for (int i = 0; i < p.Length; i++)
            {
                Write(aWriter, p[i]);
            }
            return;
        }
        aWriter.Write((byte)0);
        aWriter.Write(aType.AssemblyQualifiedName);
    }


    public void OnBeforeSerialize()
    {
        using (var stream = new MemoryStream())
        using (var w = new BinaryWriter(stream))
        {
            Write(w, type);
            data = stream.ToArray();
        }
    }

    public void OnAfterDeserialize()
    {
        using (var stream = new MemoryStream(data))
        using (var r = new BinaryReader(stream))
        {
            type = Read(r);
        }
    }
}
*/

public static class EntityStateExtensions
{
    public static Type ToType(this EntityStateType type)
    {
        switch (type)
        {
            case EntityStateType.Transform:
            case EntityStateType.LocalTransform:
                // TODO: Probably should error here, Transform requires custom logic.
                return typeof(Transform);
            case EntityStateType.Vector:
                return typeof(Vector3);
            case EntityStateType.Quaternion:
                return typeof(Quaternion);
            case EntityStateType.Float:
                return typeof(float);
            case EntityStateType.Half:
                return typeof(ushort);
            case EntityStateType.Integer:
                return typeof(int);
            case EntityStateType.Short:
                return typeof(short);
            case EntityStateType.UShort:
                return typeof(ushort);
            case EntityStateType.Byte:
                return typeof(byte);
            case EntityStateType.Bool:
                return typeof(bool);
            case EntityStateType.Trigger:
                return typeof(bool); // TODO: Will change later.
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    public static EntityStateType FromProperty(PropertyInfo prop)
    {
        if (prop.PropertyType == typeof(Transform))
        {
            if (prop.GetCustomAttribute<StateTransformAttribute>()?.Child == true)
            {
                return EntityStateType.LocalTransform;
            }
            return EntityStateType.Transform;
        }
        else if (prop.PropertyType == typeof(Vector3))
        {
            return EntityStateType.Vector;
        }
        else if (prop.PropertyType == typeof(Quaternion))
            return EntityStateType.Quaternion;
        else if (prop.PropertyType == typeof(float))
            return EntityStateType.Float;
        else if (prop.PropertyType == typeof(half))
            return EntityStateType.Half;
        else if (prop.PropertyType == typeof(int))
            return EntityStateType.Integer;
        else if (prop.PropertyType == typeof(short))
            return EntityStateType.Short;
        else if (prop.PropertyType == typeof(ushort))
            return EntityStateType.UShort;
        else if (prop.PropertyType == typeof(byte))
            return EntityStateType.Byte;
        else if (prop.PropertyType == typeof(bool))
            return EntityStateType.Bool;
        else if (prop.PropertyType == typeof(MirrorStateEvent))
            return EntityStateType.Trigger; // TODO: Will change later.
        else
            throw new ArgumentOutOfRangeException(nameof(prop), prop, null);
    }
}
