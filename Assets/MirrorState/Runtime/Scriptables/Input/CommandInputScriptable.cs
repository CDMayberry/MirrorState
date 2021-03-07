using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CommandInputType
{
    Axis,
    Float,
    Integer,
    Button,
    Short,
    UShort,
    Byte
}


[Serializable]
public class CommandInputProperty
{
    public string Name;
    public CommandInputType InputType;
}

[CreateAssetMenu(fileName = "CommandInput", menuName = "Mayberry/CommandInputScriptable", order = 21)]
public class CommandInputScriptable : ScriptableObject
{
    public List<CommandInputProperty> Fields;

    public static implicit operator bool(CommandInputScriptable cmd) => cmd != null;
}

public static class CommandInputExtensions
{
    public static Type ToType(this CommandInputType type)
    {
        switch (type)
        {
            case CommandInputType.Axis:
                return typeof(Vector2);
            case CommandInputType.Float:
                return typeof(float);
            case CommandInputType.Integer:
                return typeof(int);
            case CommandInputType.Short:
                return typeof(short);
            case CommandInputType.UShort:
                return typeof(ushort);
            case CommandInputType.Byte:
                return typeof(byte);
            case CommandInputType.Button:
                return typeof(bool);
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        throw new Exception("No matching type found.");
    }
}
