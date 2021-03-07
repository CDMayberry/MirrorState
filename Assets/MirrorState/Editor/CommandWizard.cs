using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Mayberry.Scripts;
using UnityEditor;
using UnityEngine;

public class CommandWizard : ScriptableWizard
{
    public string Name;
    private Action _callback;

    [MenuItem("Tools/Mayberry/Create Command")]
    public static void CreateWizard()
    {
        var wizard = ScriptableWizard.DisplayWizard<CommandWizard>("Create Command", "Create");
        //If you don't want to use the secondary button simply leave it out:
        //ScriptableWizard.DisplayWizard<WizardCreateLight>("Create Light", "Create");
    }

    public static void CreateWizard(Action action)
    {
        var wizard = ScriptableWizard.DisplayWizard<CommandWizard>("Create Command", "Create");
        wizard._callback = action;
        //If you don't want to use the secondary button simply leave it out:
        //ScriptableWizard.DisplayWizard<WizardCreateLight>("Create Light", "Create");
    }

    void OnWizardCreate()
    {
        string path = "Assets/MirrorState/Scripts/Generated/Scriptables/Commands/" + Name;
        Directory.CreateDirectory(path);
        var rootCommand = MayberryUtils.CreateAsset<CommandScriptable>(Name + "Command", path);
        var input = MayberryUtils.CreateAsset<CommandInputScriptable>(Name + "Input", path);
        var result = MayberryUtils.CreateAsset<CommandStateScriptable>(Name + "State", path);

        rootCommand.Name = Name;
        rootCommand.Input = input;
        rootCommand.Output = result;

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = rootCommand;

        _callback?.Invoke();
    }

    void OnWizardUpdate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            errorString = "Name is required.";
            isValid = false;
        }
        else
        {
            errorString = "";
            isValid = true;
        }
    }
}
