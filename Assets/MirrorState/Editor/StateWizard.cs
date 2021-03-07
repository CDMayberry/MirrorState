using System;
using System.IO;
using Mayberry.Scripts;
using UnityEngine;
using UnityEditor;

public class StateWizard : ScriptableWizard
{
    public string Name;
    private Action _callback;

    public static void CreateWizard(Action action)
    {
        var wizard = ScriptableWizard.DisplayWizard<StateWizard>("Create State", "Create");
        wizard._callback = action;
        //If you don't want to use the secondary button simply leave it out:
        //ScriptableWizard.DisplayWizard<WizardCreateLight>("Create Light", "Create");
    }

    void OnWizardCreate()
    {
        string path = "Assets/MirrorState/Scripts/Generated/Scriptables/States/" + Name;
        Directory.CreateDirectory(path);
        var rootCommand = MayberryUtils.CreateAsset<EntityStateScriptable>(Name + "EntityState", path);

        rootCommand.Name = Name;

        //EditorUtility.FocusProjectWindow();
        //Selection.activeObject = rootCommand;

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