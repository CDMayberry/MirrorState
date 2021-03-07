using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// TODO: Remove CreateAssetMenu and programatically create all neccessary scriptables from the Tools menu. 
public class CommandScriptable : ScriptableObject
{
    public string Name;
    public bool Udp;
    public EntityStateScriptable State;
    public CommandInputScriptable Input;
    public CommandStateScriptable Output;

    public static implicit operator bool(CommandScriptable cmd) => cmd != null;
}
