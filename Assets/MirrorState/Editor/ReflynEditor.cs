using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.MirrorState.Reflyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mirror;
using MirrorState.Reflyn.Editor;
using MirrorState.Scripts;
using MirrorState.Scripts.Editor.Reflyn;
using RailgunNet.Ticks;
using RailgunNet.Ticks.Interfaces;
using Reflyn.Mixins;
using Reflyn.Refly;
using Reflyn.Refly.CodeDom;
using Reflyn.Refly.CodeDom.Statements;
using UnityEditor;
using UnityEngine;

namespace Mayberry.Scripts.Editor
{

    public class ReflynEditor : ScriptableObject
    {

        public static readonly string Namespace = "MirrorState.Scripts";

        [MenuItem("Tools/MirrorState/Generate All Network States")]
        public static void GenerateAll()
        {
            var commands = MayberryUtils.FindAssetsByType<CommandScriptable>();

            foreach (CommandScriptable command in commands)
            {
                if (!command)
                {
                    continue;
                }

                if (command.Input == null)
                {
                    Debug.LogError(command.name + " is missing inputs.");
                    continue;
                }

                if (command.State == null)
                {
                    Debug.LogError(command.name + " is missing outputs.");
                    continue;
                }

                GenerateBaseController(command);
            }

            var states = MayberryUtils.FindAssetsByType<EntityStateScriptable>();

            foreach (EntityStateScriptable state in states)
            {
                if (!state)
                {
                    continue;
                }

                GenerateEntityState(state);
            }

            Debug.Log("Generate All Complete");
        }

        public static string GenerateBaseController(CommandScriptable command, bool overrideBuild = false)
        {
            string className = command.Name + "ControllerBase";
            var demo = new NamespaceDeclaration(Namespace + ".Generated.Commands");
            demo
                .AddImport("System")
                .AddImport("System.Collections.Generic")
                .AddImport("UnityEngine")
                .AddImport("Mirror")
                .AddImport("MirrorState.Mirror")
                .AddImport("MirrorState.Scripts")
                .AddImport("MirrorState.Scripts.Generated.States")
                .AddImport("RailgunNet.Ticks")
                .AddImport("RailgunNet.Ticks.Interfaces")
                .AddImport("RailgunNet.Ticks.Buffers");

            string entityStateClassName = command.State.Name + "State";

            ClassDeclaration controllerClass =
                demo
                    .AddClass(className)
                    .ToAbstract()
                    .SetBaseType<NetworkBehaviour>();

            controllerClass
                .AddCustomAttribute(typeof(RequireComponent))
                .WithPositional(Expr.TypeOf(entityStateClassName));

            var stateField = controllerClass
                .AddField(entityStateClassName, "State")
                .ToPublic();

            // THOUGHT: Is there any way to use the FixedUpdate frames as a 'tick' of sorts?

            ClassDeclaration controlsClass = controllerClass.AddClass("Controls").ToStruct().WithCustomAttribute(typeof(SerializableAttribute));
            ClassDeclaration outputClass = controllerClass.AddClass("Output").ToStruct().WithCustomAttribute(typeof(SerializableAttribute));
            ClassDeclaration cmdClass = controllerClass.AddClass("Command").ToStruct().WithCustomAttribute(typeof(SerializableAttribute));
            cmdClass.AddInterface<ITick>();

            foreach (var prop in command.Input.Fields.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
            {
                controlsClass
                    .AddField(prop.InputType.ToType(), prop.Name)
                    .ToPublic();
            }

            foreach (var prop in command.Output.Fields.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
            {
                outputClass
                    .AddField(prop.StateType.ToType(), prop.Name)
                    .ToPublic();
            }


            // TODO: Have this be either A) a field on the scriptable or B) a field on the behaviour that defaults to the value from the scriptable.
            // TODO: Need to remove this, it should be per client probably based on RTT rather than a fixed step back.
            //int estimatedTicksBack = 8;

            var cmdTickField = cmdClass.AddField(typeof(uint), "CmdTick").ToPublic();
            //var cmdTimestampField = cmdClass.AddField(typeof(double), "Timestamp").ToPublic();
            var cmdFirstField = cmdClass.AddField(typeof(bool), "FirstExecute").ToPublic();
            var cmdControlsField = cmdClass.AddField(controlsClass, "Controls").ToPublic();
            var cmdOutputField = cmdClass.AddField(outputClass, "Output").ToPublic();

            cmdClass
                .AddProperty(typeof(uint), "Tick")
                .WithGet(Stm.Return(Expr.This.Field(cmdTickField)));

            //cmdClass
            var cmdIsNewProp = cmdClass
                .AddProperty(typeof(bool), "IsNew")
                .WithGet(Stm.Return(Expr.This.Field(cmdFirstField)))
                .WithSet(Stm.Assign(cmdFirstField, Expr.Value));


            // TODO: Add logic for saving current player's states and replaying them via the command (result or input?) history.
            /*var historyField = controllerClass
                .AddGenericField("List", "_cmdHistory", cmdClass.Name)
                .EqualTo(Expr.NewGeneric("List", cmdClass));*/

            /*var inputQueueField = controllerClass
                .AddGenericField("Queue", "InputQueue", inputClass.Name)
                .ToProtected()
                .EqualTo(Expr.NewGeneric("Queue", inputClass));*/

            /*var cmdQueueField = controllerClass
                .AddGenericField("Queue", "_cmdQueue", cmdClass.Name)
                .EqualTo(Expr.NewGeneric("Queue", cmdClass));*/

            var lastServerTickField = controllerClass
                .AddField(typeof(uint), "_lastServerTick")
                .EqualTo(Expr.Type(typeof(TickConstants)).Field("BadTick"));
            var lastFixedTickField = controllerClass
                .AddField(typeof(uint), "_lastFixedTick")
                .EqualTo(Expr.Type(typeof(TickConstants)).Field("BadTick"));

            var cmdBufferField = controllerClass
                .AddGenericField("DejitterStructBuffer", "_cmdBuffer", cmdClass.Name)
                .EqualTo(Expr.NewGeneric("DejitterStructBuffer", cmdClass, 60));

            var cmdHistoryField = controllerClass
                .AddGenericField("DejitterStructBuffer", "_cmdBufferHistory", cmdClass.Name)
                .EqualTo(Expr.NewGeneric("DejitterStructBuffer", cmdClass, 200));
            var lastCommandField = controllerClass
                .AddField(cmdClass, "_lastCommand"); // It's a struct so it's automatically new'd up.

            controllerClass
                .AddMethod("Awake")
                .ToProtected()
                .ToVirtual()
                .AddAssign(stateField, GameObjectExpr.GetComponent(entityStateClassName))
                ;



            controllerClass
                .AddMethod("OnStartClient")
                .ToOverride()
                .ToPublic()
                .AddAssign(
                    lastFixedTickField, TickSystemExpr.Instance.ServerTick
                );

            var simAuthMethod = controllerClass
                .AddMethod("SimulateAuthority")
                .ToPublic()
                .ToAbstract()
                .WithParameter(controlsClass, "controls", FieldDirectionReflyn.Ref)
                .SetReturnType(typeof(bool));

            var exeCmdMethod = controllerClass
                .AddMethod("ExecuteCommand")
                .ToPublic()
                .ToAbstract()
                .SetReturnType(outputClass);
            exeCmdMethod
                .AddParameter(cmdClass, "cmd");
            exeCmdMethod
                .AddParameter(typeof(bool), "reset");


            var commandVar = Stm.Var(cmdClass, "cmd");

            // TODO: maybe only use state result class, directs the developer better.
            // TODO: How does validate work? We need to be aware of the player's current state before we can compare...
            var validateStateMethod = controllerClass
                .AddMethod("TargetValidateState")
                .ToPrivate()
                .WithParameter(typeof(uint), "tick")
                .WithParameter(outputClass, "server")
                .WithCustomAttribute(typeof(TargetRpcAttribute));

            validateStateMethod
                .Add(
                    commandVar,
                    StmCommon.IfNotTryGet(cmdHistoryField, Expr.Var("tick"), Expr.Var(commandVar))
                        .WithTrue(
                            UnityExpr.Debug.LogWarning("Missing state to correct, possibly still on filling?"),
                            Stm.Return()
                        ),
                    Stm.Assign(lastServerTickField, Expr.Var("tick")),
                    Stm.Assign(commandVar.ToExpr().Field(cmdOutputField), Expr.Var("server")),
                    Stm.IfIdentity(cmdHistoryField.ToExpr().Method("Replace").Invoke(commandVar), false)
                        .WithTrue(
                            UnityExpr.Debug.LogWarning("Didn't replace Server State")
                        )
                );

            // TODO: This should find the closest state, reset all variables, then re-run ExecuteCommand up to that point.
            var resetStateMethod = controllerClass
                .AddMethod("ResetState")
                .ToPrivate();

            // TODO: This passes back a state, but then it reruns all of the inputs in history.

            // The cmdTickField + 1 on GetRange is because the tick that is reset to is considered to have already run to have gotten this result.
            resetStateMethod
                .Add(
                    Stm.If(lastServerTickField.ToExpr().Identity(Expr.Type(typeof(TickConstants)).Field("BadTick")))
                        .WithTrue(
                            Stm.Return()    
                        ),
                    commandVar,
                    StmCommon.IfNotTryGet(cmdHistoryField, lastServerTickField, Expr.Var(commandVar))
                        .WithTrue(
                            UnityExpr.Debug.LogError("Missing state to correct."),
                            Stm.Return()
                        ),
                    exeCmdMethod.InvokeOnThis(commandVar, true),
                    Stm.ForEach(cmdClass, "history", cmdHistoryField.ToExpr().Method("GetRange").Invoke(commandVar.ToExpr().Field(cmdTickField) + 1), false)
                        .With(
                            exeCmdMethod.InvokeOnThis(Expr.Var("history"), false)
                        )
                )
                ;
            // TODO: replace inputqueue if statement
            var executeResultVar = Stm.Var(outputClass, "result")
                .EqualTo(exeCmdMethod.InvokeOnThis(commandVar, false));

            var serverExecuteCommandMethod = controllerClass
                .AddMethod("ServerExecuteCommand")
                .WithCustomAttribute(typeof(ServerAttribute))
                .WithParameter(cmdClass, "cmd")
                .Add(
                    executeResultVar,
                    validateStateMethod.InvokeOnThis(commandVar.ToExpr().Field(cmdTickField), executeResultVar),
                    Stm.Assign(lastCommandField, Expr.Var(commandVar))
                );

            // TODO: We have to send state as well, otherwise we have no point to compare to...
            // TODO: Replace Input with Command, verify passed in timestamp is after now.
            var cmdProcessCommandMethod = controllerClass
                    .AddMethod("CmdProcessCommand")
                    .WithParameter(cmdClass, "cmd");

            if (command.Udp)
            {
                cmdProcessCommandMethod
                    .AddCustomAttribute<CommandAttribute>()
                    .With("channel", 1) // Typically channel 1 is unreliable. This should be an field on the scriptable later on.
                    ;
            }
            else
            {
                cmdProcessCommandMethod
                    .AddCustomAttribute<CommandAttribute>();
            }

            cmdProcessCommandMethod
                    .Add(
                        Stm.If(NetworkObjectExpr.This.hasAuthority.Or(MirrorExpr.NetworkServer.active.Identity(false)))
                            .WithTrue(
                                Stm.Return()
                            )
                    )
                    .AddAssign(commandVar.ToExpr().Field(cmdFirstField), true)
                    .Add(cmdBufferField.ToExpr().Method("Store").Invoke(Expr.Var("cmd")))
                    .Add(
                        Stm.If(cmdBufferField.ToExpr().Prop("Latest").Field(cmdTickField) < TickSystemExpr.Instance.Tick)
                            .WithTrue(
                                //UnityExpr.Debug.LogWarning("Client got too far behind Server Delay"), // TODO: add in exact ticks?
                                serverExecuteCommandMethod.InvokeOnThis(commandVar)
                            )
                    )
                ;

            // So the issue here is that once you go over 150 ms the commands are coming in too late
            // and the _estimatedTick is running past them.

            Stm.While(lastServerTickField < TickSystemExpr.Instance.Tick);

            // TODO: do "TickSystemExpr.Instance.Tick - 1" but does this match with the server properly? IE what happens when this command is sent back and it's off by 1 tick?
            //          We need to add a delay, always a minimum of 1 frame, but do we need to compensate for that?

            var fixedUpdateMethod = controllerClass
                .AddMethod("FixedUpdate")
                .Add(commandVar)
                .Add(
                    Stm.If(NetworkObjectExpr.This.HasAnyAuthority())
                        .WithTrue(
                            Stm.While(lastFixedTickField < TickSystemExpr.Instance.Tick)
                                .With(
                                    StmAssign.Increment(lastFixedTickField),
                                    Stm.If(NetworkObjectExpr.This.isClientOnly).WithTrue(
                                        resetStateMethod.InvokeOnThis()
                                    ),
                                    Stm.Assign(commandVar, Expr.New(cmdClass)),
                                    simAuthMethod.InvokeOnThis(Expr.Arg(commandVar.ToExpr().Field(cmdControlsField), FieldDirectionReflyn.Ref)),
                                    Stm.Assign(commandVar.ToExpr().Field(cmdTickField), lastFixedTickField),
                                    Stm.Assign(commandVar.ToExpr().Field(cmdFirstField), true),
                                    Stm.Assign(commandVar.ToExpr().Field(cmdOutputField), exeCmdMethod.InvokeOnThis(commandVar, false)),
                                    Stm.If(NetworkObjectExpr.This.isClientOnly)
                                        .WithTrue(
                                            cmdProcessCommandMethod.InvokeOnThis(commandVar), // This needs to change to be the complete command.
                                            Stm.Assign(commandVar.ToExpr().Field(cmdFirstField), false),
                                            cmdHistoryField.ToExpr().Method("Store").Invoke(commandVar)
                                        )
                                )
                            )
                        .WithFalse(
                            Stm.If(MirrorExpr.NetworkServer.active.And(TickSystemExpr.Instance.Tick > 1))
                                .WithTrue(
                                    //duplicateStateVar,
                                    StmCommon.IfNotTryGet(cmdBufferField, TickSystemExpr.Instance.Tick - 1, Expr.Var(commandVar))
                                        .WithTrue(
                                            Stm.If(lastCommandField.ToExpr().Field(cmdTickField).Identity(Expr.Type(typeof(TickConstants)).Field("BadTick")))
                                                .WithTrue(
                                                    Stm.Return()
                                                ),

                                            Stm.Assign(commandVar, lastCommandField),
                                            Stm.Assign(commandVar.ToExpr().Field(cmdTickField), TickSystemExpr.Instance.Tick - 1)
                                        ),
                                    serverExecuteCommandMethod.InvokeOnThis(commandVar)
                                )
                            )
                        );
            // TODO: We need some kind of settings box for things like 'Generate Target Correction' and others.

            // Generation
            var comp = demo.ToCompilationUnit();
            if (!ReflynUtils.CompileSyntax(comp) && !overrideBuild)
            {
                throw new Exception("Failed build.");
            }

            var str = comp.NormalizeWhitespace().ToFullString();
            string path = "Assets/MirrorState/Scripts/Generated/Commands";

            Directory.CreateDirectory(path);

            string fullPath = $"{path}/{className}.cs";

            var writer = new StreamWriter(fullPath, false);
            writer.WriteLine(str);
            writer.Close();

            //Re-import the file to update the reference in the editor
            AssetDatabase.ImportAsset(fullPath);
            return fullPath;
        }

        public static string GenerateEntityState(EntityStateScriptable state)
        {
            return new EntityStateGeneration(state).Generate();

        }
    }
}
