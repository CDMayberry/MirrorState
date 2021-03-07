using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mayberry.Scripts;
using Mayberry.Scripts.Editor;
using MirrorState.Scripts;
using MirrorState.Scripts.Generation;
using UnityEngine;
using UnityEditor;
using UnityEditor.VersionControl;

class CommandsWindow : EditorWindow
{
    [MenuItem("Tools/MirrorState/Entities Window")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(CommandsWindow), true, "Entities");
    }

    private List<CommandScriptable> _commands;
    private List<EntityStateScriptable> _states;
    private CommandScriptable _selectedCommand;
    private EntityStateScriptable _selectedState;

    private void Awake()
    {
        // Find All scriptables
        _commands = MayberryUtils.FindAssetsByType<CommandScriptable>();
        _states = MayberryUtils.FindAssetsByType<EntityStateScriptable>();
    }

    void OnGUI()
    {
        if(_selectedCommand)
        {
            OnSelectedCommand(_selectedCommand);
        }
        else if (_selectedState)
        {
            OnSelectedState(_selectedState);
        }
        else
        {
            OnMainMenu();
        }
    }

    //private List<EntityStateType> Predictable 

    void OnMainMenu()
    {
        // STATES

        GUILayout.BeginHorizontal();
        GUILayout.Label("States", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(40)))
        {
            // BUG: I think "RefreshStates" is wrong...
            StateWizard.CreateWizard(RefreshStates);
        }
        if (GUILayout.Button("R", EditorStyles.miniButtonRight, GUILayout.Width(40)))
        {
            RefreshStates();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10f);
        if (_states.Count <= 0)
        {
            GUILayout.Label("No States...");
        }
        else
        {
            foreach (var state in _states.ToList())
            {
                if (!state)
                {
                    continue;
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(state.Name + " State"))
                {
                    _selectedState = state;
                    //name = "State";
                }

                if (GUILayout.Button("X", GUILayout.Width(40)))
                {
                    var assetPath = AssetDatabase.GetAssetPath(state);
                    int idx = assetPath.LastIndexOf("/");
                    assetPath = idx >= 0 ? assetPath.Substring(0, idx) : assetPath;

                    Directory.Delete(assetPath, true);
                    AssetDatabase.DeleteAsset(assetPath + ".meta");
                    RefreshCommands();
                    AssetDatabase.Refresh();
                    continue;
                }

                GUILayout.EndHorizontal();
            }
        }
        GUILayout.Space(10f);

        // COMMANDS

        GUILayout.BeginHorizontal();
        GUILayout.Label("Commands", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(40)))
        {
            CommandWizard.CreateWizard(RefreshCommands);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10f);

        if (_commands.Count <= 0)
        {
            GUILayout.Label("No Commands...");
        }
        else
        {
            foreach (var command in _commands.ToList())
            {
                if (!command)
                {
                    continue;
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(command.Name + " Command"))
                {

                    _selectedCommand = command;
                    if (_selectedCommand.State)
                    {
                        _entityStateIdx = _states.FindIndex(x => x == _selectedCommand.State);
                    }
                    //name = "Command";
                }

                if (GUILayout.Button("X", GUILayout.Width(40)))
                {
                    var assetPath = AssetDatabase.GetAssetPath(command);
                    int idx = assetPath.LastIndexOf("/");
                    assetPath = idx >= 0 ? assetPath.Substring(0, idx) : assetPath;

                    string commandScriptPath = "Assets/Mayberry/Scripts/Generated/Commands/" + command.Name + "BaseController";

                    Directory.Delete(assetPath, true);
                    AssetDatabase.DeleteAsset(assetPath + ".meta");

                    if (File.Exists(commandScriptPath + ".cs"))
                    {
                        AssetDatabase.DeleteAsset(commandScriptPath + ".cs");
                        AssetDatabase.DeleteAsset(commandScriptPath + ".meta");
                    }

                    RefreshCommands();
                    AssetDatabase.Refresh();
                    continue;
                }

                GUILayout.EndHorizontal();
            }
        }
        GUILayout.Space(10f);
    }

    void OnSelectedCommand(CommandScriptable command)
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", EditorStyles.miniButtonLeft, GUILayout.Width(40)))
        {
            ReturnToMainMenu();
            return;
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label(command.Name + " Command", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Build", EditorStyles.miniButtonRight, GUILayout.Width(40)))
        {
            ReflynEditor.GenerateBaseController(command);
            Debug.Log("Build Complete");
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(20);
        float halfWidth = position.width / 2f;

        // Check needs to start after menu buttons
        EditorGUI.BeginChangeCheck();

        GUILayout.BeginHorizontal();
        GUILayout.Label("UDP?");
        command.Udp = EditorGUILayout.Toggle(command.Udp);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("State");
        _entityStateIdx = EditorGUILayout.Popup(_entityStateIdx, _states.Select(x => x.Name).ToArray());
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Inputs", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(40)))
        {
            if (command.Input.Fields == null)
            {
                command.Input.Fields = new List<CommandInputProperty>();
            }

            command.Input.Fields.Add(new CommandInputProperty());
        }
        GUILayout.EndHorizontal();
        if (command.Input == null || command.Input.Fields == null || command.Input.Fields.Count <= 0)
        {
            GUILayout.Label("No Inputs...");
        }
        else
        {
            foreach (var field in command.Input.Fields.ToList())
            {
                GUILayout.BeginHorizontal();
                field.Name = GUILayout.TextField(field.Name, GUILayout.Width(halfWidth - 10f));
                field.InputType = (CommandInputType)EditorGUILayout.EnumPopup(field.InputType, GUILayout.Width(halfWidth - 40f));
                if (GUILayout.Button("X", GUILayout.Width(40)))
                {
                    command.Input.Fields.Remove(field);
                }
                GUILayout.EndHorizontal();
            }
        }
        GUILayout.Space(20);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Outputs", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(40)))
        {
            if (command.Output.Fields == null)
            {
                command.Output.Fields = new List<CommandStateProperty>();
            }

            command.Output.Fields.Add(new CommandStateProperty());
        }
        GUILayout.EndHorizontal();

        if (command.Output == null || command.Output.Fields == null || command.Output.Fields.Count <= 0)
        {
            GUILayout.Label("No Outputs...");
        }
        else
        {
            foreach (var field in command.Output.Fields.ToList())
            {
                GUILayout.BeginHorizontal();
                field.Name = GUILayout.TextField(field.Name, GUILayout.Width(halfWidth - 10f));
                field.StateType = (CommandStateType)EditorGUILayout.EnumPopup(field.StateType, GUILayout.Width(halfWidth - 40f));
                if (GUILayout.Button("X", GUILayout.Width(40)))
                {
                    command.Output.Fields.Remove(field);
                }
                GUILayout.EndHorizontal();
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            if (_entityStateIdx > -1)
            {
                command.State = _states[_entityStateIdx];
            }

            EditorUtility.SetDirty(command);
            EditorUtility.SetDirty(command.Input);
            EditorUtility.SetDirty(command.Output);
        }
    }

    void OnSelectedState(EntityStateScriptable state)
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", EditorStyles.miniButtonLeft, GUILayout.Width(40)))
        {
            ReturnToMainMenu();
            return;
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label(state.Name + " State", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Build", EditorStyles.miniButtonRight, GUILayout.Width(40)))
        {
            ReflynEditor.GenerateEntityState(state);
            Debug.Log("Build Complete");
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(20);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Tracked");
        state.Tracked = EditorGUILayout.Toggle(state.Tracked);
        GUILayout.EndHorizontal();

        GUILayout.Space(20);

        // Check needs to start after menu buttons
        EditorGUI.BeginChangeCheck();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Fields", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(40)))
        {
            if (state.Fields == null)
            {
                state.Fields = new List<EntityStateProperty>();
            }

            state.Fields.Add(new EntityStateProperty());
        }
        GUILayout.EndHorizontal();

        float halfWidth = position.width / 2f;
        if (state.Fields == null || state.Fields.Count <= 0)
        {
            GUILayout.Label("No Fields...");
        }
        else
        {
            foreach (var field in state.Fields.ToList())
            {
                GUILayout.BeginHorizontal();
                field.Name = GUILayout.TextField(field.Name, GUILayout.Width(halfWidth - 10f));
                field.StateType = (EntityStateType)EditorGUILayout.EnumPopup(field.StateType, GUILayout.Width(halfWidth - 40f));

                if (GUILayout.Button("X", GUILayout.Width(40)))
                {
                    state.Fields.Remove(field);
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                if (field.StateType == EntityStateType.Half || field.StateType == EntityStateType.Float || field.StateType == EntityStateType.Trigger)
                {
                    field.IsAnim = EditorGUILayout.Toggle(field.IsAnim, GUILayout.Width(10f));
                    GUILayout.Label(new GUIContent("Anim", "If enabled will set the matching property of animator when the field is set or triggered."), GUILayout.Width(40f));
                }
                else if(field.IsAnim)
                {
                    field.IsAnim = false;
                }

                if (IsPredictable(field.StateType))
                {
                    field.Predicted = EditorGUILayout.Toggle(field.Predicted, GUILayout.Width(10f));
                    GUILayout.Label(new GUIContent("Predicted", "Check if field is predicted by the authoritative client. Otherwise all clients wait on server to make changes."), GUILayout.Width(65f));
                }
                else if (field.Predicted)
                {
                    field.Predicted = false;
                }

                if (field.StateType == EntityStateType.Transform || field.StateType == EntityStateType.LocalTransform)
                {
                    field.Position = EditorGUILayout.Toggle(field.Position, GUILayout.Width(10f));
                    GUILayout.Label("Position", GUILayout.Width(40f));
                    field.Rotation = EditorGUILayout.Toggle(field.Rotation, GUILayout.Width(10f));
                    GUILayout.Label("Rotation", GUILayout.Width(40f));
                    field.Scale = EditorGUILayout.Toggle(field.Scale, GUILayout.Width(10f));
                    GUILayout.Label("Scale", GUILayout.Width(40f));
                }
                else
                {
                    field.Position = false;
                    field.Rotation = false;
                    field.Scale = false;
                }


                GUILayout.EndHorizontal();
            }
        }
        if (state.Interfaces == null || state.Interfaces.Count <= 0)
        {
            //GUILayout.Label("No Fields...");
        }
        else
        {
            // TODO: How can we add the Anim/Flag fields to these? OR can we use attributes?
            GUILayout.Label("Interface Fields", EditorStyles.boldLabel);
            List<Type> interfaces = EntityStateInterface.GetInterfaces(state.Interfaces);
            List<PropertyInfo> properties = interfaces.SelectMany(x => x.GetProperties()).ToList();

            foreach (var field in properties.ToList())
            {
                EditorGUI.BeginDisabledGroup(true);
                GUILayout.BeginHorizontal();
                GUILayout.TextField(field.Name, GUILayout.Width(halfWidth - 10f));

                string name = field.PropertyType.Name == nameof(MirrorStateEvent)
                    ? "Trigger"
                    : field.PropertyType.Name;

                GUILayout.Label(name, GUILayout.Width(halfWidth - 40f));

                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);

                if (IsPredictable(field.PropertyType))
                {
                    EditorGUILayout.Toggle(field.GetCustomAttribute<StatePredictedAttribute>() != null, GUILayout.Width(10f));
                    GUILayout.Label(new GUIContent("Predicted", "Checked if field is predicted by the authoritative client. Otherwise all clients wait on server to make changes."), GUILayout.Width(65f));
                }

                if (IsAnimation(field.PropertyType))
                {
                    EditorGUILayout.Toggle(field.GetCustomAttribute<StateAnimAttribute>() != null, GUILayout.Width(10f));
                    GUILayout.Label(new GUIContent("Animation", "If checked will set the matching property of animator when the field is set or triggered."), GUILayout.Width(65f));
                }

                if (field.PropertyType == typeof(Transform))
                {
                    var attr = field.GetCustomAttribute<StateTransformAttribute>();
                    if (attr != null)
                    {
                        EditorGUILayout.Toggle(attr.Position, GUILayout.Width(10f));
                        GUILayout.Label("Position", GUILayout.Width(40f));
                        EditorGUILayout.Toggle(attr.Rotation, GUILayout.Width(10f));
                        GUILayout.Label("Rotation", GUILayout.Width(40f));
                        EditorGUILayout.Toggle(attr.Scale, GUILayout.Width(10f));
                        GUILayout.Label("Scale", GUILayout.Width(40f));
                        EditorGUILayout.Toggle(attr.Child, GUILayout.Width(10f));
                        GUILayout.Label("Child", GUILayout.Width(40f));
                    }
                    else
                    {
                        EditorGUILayout.Toggle(false, GUILayout.Width(10f));
                        GUILayout.Label("Position", GUILayout.Width(40f));
                        EditorGUILayout.Toggle(false, GUILayout.Width(10f));
                        GUILayout.Label("Rotation", GUILayout.Width(40f));
                        EditorGUILayout.Toggle(false, GUILayout.Width(10f));
                        GUILayout.Label("Scale", GUILayout.Width(40f));
                        EditorGUILayout.Toggle(false, GUILayout.Width(10f));
                        GUILayout.Label("Child", GUILayout.Width(40f));
                    }

                }
                
                GUILayout.EndHorizontal();
                EditorGUI.EndDisabledGroup();
            }


            // TODO: How can we add the Anim/Flag fields to these? OR can we use attributes?
            //GUILayout.Label("Interface Events", EditorStyles.boldLabel);
            List<EventInfo> events = interfaces.SelectMany(x => x.GetEvents()).ToList();
            foreach (var evnt in events)
            {
                EditorGUI.BeginDisabledGroup(true);
                GUILayout.BeginHorizontal();

                GUILayout.TextField(evnt.Name, GUILayout.Width(halfWidth - 10f));
                GUILayout.Label("Trigger", GUILayout.Width(halfWidth - 40f));

                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);

                if (IsPredictable(evnt.EventHandlerType))
                {
                    EditorGUILayout.Toggle(evnt.GetCustomAttribute<StatePredictedAttribute>() != null, GUILayout.Width(10f));
                    GUILayout.Label(new GUIContent("Predicted", "Checked if field is predicted by the authoritative client. Otherwise all clients wait on server to make changes."), GUILayout.Width(65f));
                }

                if (IsAnimation(evnt.EventHandlerType))
                {
                    EditorGUILayout.Toggle(evnt.GetCustomAttribute<StateAnimAttribute>() != null, GUILayout.Width(10f));
                    GUILayout.Label(new GUIContent("Animation", "If checked will set the matching property of animator when the field is set or triggered."), GUILayout.Width(65f));
                }

                GUILayout.EndHorizontal();
                EditorGUI.EndDisabledGroup();
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(state);
        }
    }

    void ReturnToMainMenu()
    {
        _selectedState = null;
        _selectedCommand = null;
        //name = "Entities";
    }

    private int _entityStateIdx = -1;
    void RefreshCommands()
    {
        _commands = MayberryUtils.FindAssetsByType<CommandScriptable>();
        _entityStateIdx = -1;
        //Debug.Log("Refreshing Window...");
    }

    void RefreshStates()
    {
        _states = MayberryUtils.FindAssetsByType<EntityStateScriptable>();
        //Debug.Log("Refreshing Window...");
    }

    private bool IsPredictable(EntityStateType type)
    {
        return type == EntityStateType.Trigger || type == EntityStateType.Float || type == EntityStateType.Short || type == EntityStateType.Integer || type == EntityStateType.UShort || type == EntityStateType.Byte || type == EntityStateType.Bool || type == EntityStateType.Half;
    }

    private bool IsPredictable(Type type)
    {
        return type == typeof(MirrorStateEvent) || type == typeof(float);
    }

    private bool IsAnimation(Type type)
    {
        return type == typeof(MirrorStateEvent) || type == typeof(ushort) || type == typeof(float);
    }
}