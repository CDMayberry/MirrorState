using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CommandStateType
{
    //Transform,
    Vector,
    Quaternion,
    Float,
    Integer,
    Bool,
    Short,
    Byte
}

[Serializable]
public class CommandStateProperty
{
    public string Name;
    public CommandStateType StateType;
}

public class CommandStateScriptable : ScriptableObject
{
    public List<CommandStateProperty> Fields;
    public static implicit operator bool(CommandStateScriptable cmd) => cmd != null;
}

public static class CommandStateExtensions
{
    public static Type ToType(this CommandStateType type)
    {
        switch (type)
        {
            /*case CommandStateType.Transform:
                return typeof(Transform);*/
            case CommandStateType.Vector:
                return typeof(Vector3);
            case CommandStateType.Quaternion:
                return typeof(Quaternion);
            case CommandStateType.Float:
                return typeof(float);
            case CommandStateType.Integer:
                return typeof(int);
            case CommandStateType.Short:
                return typeof(ushort);
            case CommandStateType.Byte:
                return typeof(byte);
            case CommandStateType.Bool:
                return typeof(bool);
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        throw new Exception("No matching type found.");
    }
}
