using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.MirrorState.Reflyn;
using MirrorState.Scripts.Generation;
using Mayberry.Reflyn;
using Mayberry.Scripts.Editor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mirror;
using MirrorState.Mirror;
using MirrorState.Reflyn.Editor;
using MirrorState.Scripts.Scriptables;
using RailgunNet.Ticks.Buffers;
using RailgunNet.Ticks.Interfaces;
using Reflyn.Mixins;
using Reflyn.Refly;
using Reflyn.Refly.CodeDom;
using Reflyn.Refly.CodeDom.Expressions;
using Reflyn.Refly.CodeDom.Statements;
using UnityEditor;
using UnityEngine;
using NullReferenceException = System.NullReferenceException;

namespace MirrorState.Scripts.Editor.Reflyn
{
    public class EntityStateGeneration
    {
        private class StateField
        {
            public List<ChildTransform> Transforms = new List<ChildTransform>();
            public List<StateFieldInfo> Fields = new List<StateFieldInfo>(); // And also properties...
        }

        private class ChildTransform
        {
            public TransformExpr TransformExpr;
            public FieldDeclaration TransformDec;
            public FieldDeclaration SyncPosition;
            public FieldDeclaration StructPosition;
            public FieldDeclaration SyncRotation;
            public FieldDeclaration StructRotation;
            public FieldDeclaration SyncScale;
            public FieldDeclaration StructScale;
        }

        private class StateFieldInfo
        {
            public EntityStateProperty ScriptableStateProperty; // Required
            public FieldDeclaration StateField; // Required
            public FieldDeclaration PredictField;
            public FieldDeclaration ClassField; // Required
            public PropertyDeclaration ClassProperty;
            public FieldDeclaration AnimHash;
        }


        private EntityStateScriptable _state;
        private string _className;
        private NamespaceDeclaration _namespaceDec;
        private ClassDeclaration _stateClassDec;
        private EnumDeclaration _enumEventDec;
        private DelegateDeclaration _stateDelegate;
        private FieldGenericDeclaration _priorityQueueField;
        private MethodDeclaration _triggerEventMethod;
        private MethodDeclaration _rpcTriggerEventMethod;
        private MethodDeclaration _rpcTriggerEventMethodServer;
        private PropertyDeclaration _trackedTickDec;
        private FieldDeclaration _animDec;
        private FieldDeclaration _rbDec;
        private ChildTransform _rootTransform;
        private FieldDeclaration _syncPos;
        private FieldDeclaration _syncRot;
        private FieldGenericDeclaration _stateDejitterDec;
        private FieldDeclaration _cmdTickDec;
        private FieldDeclaration _nextUpdateDec;
        private FieldDeclaration _updateRateDec;
        private FieldDeclaration _stateCount;
        private MethodDeclaration _bufferMethod;
        private MethodDeclaration _rpcServerStateMethod;
        private FieldDeclaration _timeElapsedDec;
        private FieldDeclaration _timeToTargetDec;
        private FieldDeclaration _fromStateDec;
        private FieldDeclaration _toStateDec;
        //private FieldArrayDeclaration _stateBuffer;
        private NativePropertyReferenceExpression _delayTick;
        private MethodDeclaration _setTransStatesMethod;
        private ClassDeclaration _stateStruct;
        private ClassDeclaration _predictStruct;
        private FieldDeclaration _predictStructField;
        private ClassDeclaration _syncStruct;
        private FieldDeclaration _syncStructField;

        public EntityStateGeneration(EntityStateScriptable state)
        {
            _state = state;
            _className = state.Name + "State";
            _namespaceDec = new NamespaceDeclaration(ReflynEditor.Namespace + ".Generated.States");
            _namespaceDec
                .AddImport("System")
                .AddImport("UnityEngine")
                .AddImport("Mayberry.Reflyn")
                .AddImport("Mirror")
                .AddImport("MirrorState.Mirror")
                .AddImport("MirrorState.Scripts")
                .AddImport("MirrorState.Scripts.Rollback")
                .AddImport("MirrorState.Scripts.Generation")
                .AddImport("RailgunNet")
                .AddImport("RailgunNet.Ticks")
                .AddImport("RailgunNet.Ticks.Interfaces")
                .AddImport("RailgunNet.Ticks.Buffers");

            _stateClassDec =
                _namespaceDec
                    .AddClass(_className)
                    .SetBaseType<NetworkBehaviour>();

            _enumEventDec = _namespaceDec
                    .AddEnum(state.Name + "EventEnum", false)
                    .SetBaseType<byte>();

            _stateDelegate = _stateClassDec
                .AddDelegate(_className + "Event")
                .WithParameter(typeof(uint), "tick")
                .ToPublic();

            // This should only be done if it also Syncs Tranforms (Need to rework how this is done, IE Tracked implies it tracks a transform.)
            //  We should have it so tracked just adds Position/Rotation, no naming required.
            if (state.Tracked)
            {
                _stateClassDec
                    .AddInterface<ITrackedEntity>();
                _trackedTickDec = _stateClassDec
                    .AddProperty(typeof(uint), "TrackedTick")
                    .ToPublic();
            }

            _stateClassDec
                .AddCustomAttribute<DefaultExecutionOrder>()
                .WithPositional(10);
            
            _stateStruct = _stateClassDec
                .AddClass("State")
                .ToStruct()
                .AddInterface(typeof(ITick))
                .WithCustomAttribute(typeof(SerializableAttribute));

            _syncStruct = _stateClassDec
                .AddClass("SyncState")
                .ToStruct();

            _predictStruct = _stateClassDec
                .AddClass("PredictedState")
                .ToStruct();

            _syncStructField = _stateClassDec
                .AddField(_syncStruct, "Sync")
                .WithCustomAttribute(typeof(SyncVarAttribute))
                .ToPublic();

            _predictStructField = _stateClassDec
                .AddField(_predictStruct, "Predicted")
                .ToPublic();


            _stateDejitterDec = _stateClassDec
                .AddGenericField("DejitterStructBuffer", "_stateDejitter")
                .AddGenericType(_stateStruct)
                .EqualToNew(50);


            _priorityQueueField =
                _stateClassDec
                    .AddGenericField("PriorityQueue", "_priorityQueue")
                    .AddGenericType<uint>()
                    .AddGenericType(_enumEventDec)
                    .EqualToNew();

            _triggerEventMethod =
                _stateClassDec
                    .AddMethod("TriggerEvent")
                    .WithParameter(typeof(uint), "tick")
                    .WithParameter(_enumEventDec, "evnt");

            _delayTick = Expr.Type(typeof(TickSystem)).Prop("Instance").Prop("delayTick");

            _rpcTriggerEventMethod =
                    _stateClassDec
                        .AddMethod("RpcTriggerEventPredicted")
                        .WithParameter(typeof(uint), "tick")
                        .WithParameter(_enumEventDec, "evnt")
                        .Add(
                            Stm.If(NetworkObjectExpr.This.hasAuthority.Or(MirrorExpr.NetworkServer.active))
                                .WithTrue(
                                    Stm.Return()
                                )
                        )
                        .Add(
                            Stm.If(Expr.Var("tick") <= _delayTick)
                                .WithTrue(
                                    _triggerEventMethod.InvokeOnThis(Expr.Var("tick"), Expr.Var("evnt"))
                                )
                                .WithFalse(
                                    _priorityQueueField.ToExpr().Method("Enqueue").Invoke(Stm.Var(typeof(uint), "tick"), Stm.Var(_enumEventDec, "evnt"))
                                )
                        )
                ;

            _rpcTriggerEventMethod
                .AddCustomAttribute<ClientRpcAttribute>()
                .With("excludeOwner", true);

            // This version is for events that can only be triggered by the server, IE unpredicted events.
            _rpcTriggerEventMethodServer =
                    _stateClassDec
                        .AddMethod("RpcTriggerEvent")
                        .WithCustomAttribute(typeof(ClientRpcAttribute))
                        .WithParameter(typeof(uint), "tick")
                        .WithParameter(_enumEventDec, "evnt")
                        .Add(
                            Stm.If(MirrorExpr.NetworkServer.active)
                                .WithTrue(
                                    Stm.Return()
                                )
                        )
                        .Add(
                            Stm.If(Expr.Var("tick") <= _delayTick)
                                .WithTrue(
                                    _triggerEventMethod.InvokeOnThis(Expr.Var("tick"), Expr.Var("evnt"))
                                )
                                .WithFalse(
                                    _priorityQueueField.ToExpr().Method("Enqueue").Invoke(Stm.Var(typeof(uint), "tick"), Stm.Var(_enumEventDec, "evnt"))
                                )
                        )
                ;

            _cmdTickDec = _stateStruct.AddField(typeof(uint), "CmdTick").ToPublic();

            _stateStruct
                .AddProperty(typeof(uint), "Tick")
                .WithGet(Stm.Return(Expr.This.Field(_cmdTickDec)));

            var cmdIsNewProp = _stateStruct
                    .AddAutoProperty(typeof(bool), "IsNew")
                ;

            _rbDec = _stateClassDec
                .AddField(typeof(Rigidbody), "_rb");
            _animDec = _stateClassDec
                .AddField(typeof(Animator), "_animator")
                .WithCustomAttribute(typeof(SerializeField));

            // if transform is null, should use current transform.
            _rootTransform = new ChildTransform();

            var stateFields = AddStateFields();

            _timeElapsedDec = _stateClassDec.AddField(typeof(float), "_timeElapsed");
            _timeToTargetDec = _stateClassDec.AddField(typeof(float), "_timeToReachTarget").EqualTo(0.1f);

            _fromStateDec = _stateClassDec.AddField(_stateStruct, "_from");
            _toStateDec = _stateClassDec.AddField(_stateStruct, "_to");

            _trackedTickDec.Get.Add(
                    Stm.If(NetworkObjectExpr.This.HasAnyAuthority())
                        .WithTrue(Stm.Return(TickSystemExpr.Instance.Tick))
                );
            _trackedTickDec.Get.Add(
                Stm.If(MirrorExpr.NetworkServer.active)
                    .WithTrue(Stm.Return(TickSystemExpr.Instance.Tick - 1))
                );

            _trackedTickDec.Get.Return(_toStateDec.ToExpr().Field(_cmdTickDec));

            // I don't think this name is accurate
            _updateRateDec = _stateClassDec.AddField(typeof(uint), "TicksPerUpdate");
            _updateRateDec
                .ToPublic()
                .EqualTo(1)
                .AddCustomAttribute<RangeAttribute>()
                .WithPositional(1, 100);

            _nextUpdateDec = _stateClassDec.AddField(typeof(uint), "_nextTickUpdate");

            _stateCount = _stateClassDec
                .AddField(typeof(int), "_stateCount")
                .EqualTo(0);


            if (_state.Tracked)
            {
                _stateClassDec
                    .AddMethod("OnEnable")
                    .Add(
                        RollbackSystemExpr.Instance.Register(Expr.This)
                    );

                _stateClassDec
                    .AddMethod("OnDisable")
                    .Add(
                        RollbackSystemExpr.Instance.Unregister(Expr.This)
                    );
            }


            _syncPos = _stateClassDec.AddField(typeof(Vector3), "LastPosition")
                .WithCustomAttribute(typeof(SyncVarAttribute))
                .EqualTo(UnityExpr.Vector3.zero);
            _syncRot = _stateClassDec.AddField(typeof(Quaternion), "LastRotation")
                .WithCustomAttribute(typeof(SyncVarAttribute))
                .EqualTo(UnityExpr.Quaternion.identity);

            _stateClassDec.AddMethod("Awake")
                .AddAssign(Expr.This.Field("syncInterval"), 0f)
                .AddAssign(_rbDec, GameObjectExpr.GetComponent<Rigidbody>())
                .Add(
                    Stm.If(TickSystemExpr.Instance.ToExpr().Identity(Expr.Null))
                        .WithTrue(
                            Stm.Assign(Expr.This.Field("enabled"), false)
                        )
                );

            var onStartServerMethod = _stateClassDec
                .AddMethod("OnStartServer")
                .ToOverride()
                .ToPublic();

            var onStartClientMethod = _stateClassDec
                .AddMethod("OnStartClient")
                .ToOverride()
                .ToPublic();

            if (_rootTransform.TransformExpr != null)
            {

                onStartClientMethod
                    .Add(Stm.If(_rbDec.ToExpr().NotIdentity(Expr.Null).And(NetworkObjectExpr.This.HasAnyAuthority().Identity(false)))
                        .WithTrue(Stm.Assign(_rbDec.ToExpr().Prop("isKinematic"), true)));

                if (_rootTransform.StructPosition != null)
                {
                    onStartServerMethod.AddAssign(_rootTransform.SyncPosition, GameObjectExpr.transform.position);
                    onStartClientMethod.AddAssign(GameObjectExpr.transform.position, _rootTransform.SyncPosition);
                }
                if (_rootTransform.StructRotation != null)
                {
                    onStartServerMethod.AddAssign(_rootTransform.SyncRotation, GameObjectExpr.transform.rotation);
                    onStartClientMethod.AddAssign(GameObjectExpr.transform.rotation, _rootTransform.SyncRotation);
                }
                if (_rootTransform.StructScale != null)
                {
                    onStartServerMethod.AddAssign(_rootTransform.SyncScale, GameObjectExpr.transform.localScale);
                    onStartClientMethod.AddAssign(GameObjectExpr.transform.localScale, _rootTransform.SyncScale);
                }
            }

            // REQUIRES TEST: We're only using Predicted fields as the Sync'd fields will have a SyncVar attribute already.
            foreach (var stateFieldInfo in stateFields.Fields.Where(x => x.ScriptableStateProperty.Predicted))
            {
                // TODO: Do we only want predicted fields here??? What about the sync'd fields?
                // TODO: How do we handle Health, which is a non-predicted field that should be read?
                onStartClientMethod
                    .Add(
                        // Class property handles transform from ushort to float and back.
                        //  If it has no property it doesn't need to be converted.
                        Stm.Assign(Expr.This.Field(stateFieldInfo.ScriptableStateProperty.Predicted ? _predictStructField : _syncStructField).Field(stateFieldInfo.PredictField),
                            stateFieldInfo.ClassProperty?.ToExpr() ?? stateFieldInfo.ClassField.ToExpr()
                        )
                    );
            }

            var argDec = Stm.Var(_stateStruct, "state");

            _setTransStatesMethod = _stateClassDec
                .AddMethod("SetTransitionStates")
                .WithParameter(_stateStruct, "from")
                .WithParameter(_stateStruct, "to")
                .Add(
                    Stm.Assign(_fromStateDec, Expr.Var("from")),
                    Stm.Assign(_toStateDec, Expr.Var("to")),
                    Stm.Assign(_timeElapsedDec, 0f),
                    Stm.Assign(_timeToTargetDec,
                        Expr.Parens(
                            _toStateDec.ToExpr().Field(_cmdTickDec) -
                            _fromStateDec.ToExpr().Field(_cmdTickDec)
                        ) *
                        TickSystemExpr.SecsPerTick)
                );

            if (stateFields.Fields.Count > 0)
            {
                // REQUIRES TEST
                foreach (var stateFieldInfo in stateFields.Fields)
                {
                    if (stateFieldInfo.PredictField != null)
                    {
                        _setTransStatesMethod
                            .Add(
                                // Class property handles transform from ushort to float and back.
                                //  If it has no property it doesn't need to be converted.
                                Stm.Assign(Expr.This.Field(stateFieldInfo.ScriptableStateProperty.Predicted ? _predictStructField : _syncStructField).Field(stateFieldInfo.PredictField), _toStateDec.ToExpr().Field(stateFieldInfo.StateField))
                            );

                    }
                    else
                    {
                        // TODO: I think this branch is no longer possible due to Predict/Sync changes.
                        _setTransStatesMethod
                            .Add(
                                // Class property handles transform from ushort to float and back.
                                //  If it has no property it doesn't need to be converted.
                                Stm.Assign(
                                    stateFieldInfo.ClassProperty?.ToExpr() ?? stateFieldInfo.ClassField.ToExpr(), _toStateDec.ToExpr().Field(stateFieldInfo.StateField)
                                )
                            );
                    }
                }
            }

            _bufferMethod = _stateClassDec
                    .AddMethod("BufferState")
                    .WithParameter(_stateStruct, "state")
                ;

            if (state.Tracked)
            {
                // Alternative to bufferMethod, testing atm.
                _bufferMethod
                    .Add(
                        _stateDejitterDec.ToExpr().Method("Store").Invoke(argDec)
                    );
            }

            _bufferMethod
                .Add(
                    Stm.If(NetworkObjectExpr.This.hasAuthority.Identity(false)
                            .And(MirrorExpr.NetworkServer.active.Identity(false))
                            .And(argDec.ToExpr().Field(_cmdTickDec) < TickSystemExpr.Instance.DelayTick)
                            .And(argDec.ToExpr().Field(_cmdTickDec) > _toStateDec.ToExpr().Field(_cmdTickDec))
                        )
                        .WithTrue(
                            _setTransStatesMethod.InvokeOnThis(_toStateDec, argDec)
                        )
                );

            _rpcServerStateMethod = _stateClassDec.AddMethod("RpcServerState");
            var stateParam = _rpcServerStateMethod.AddParameter(_stateStruct, "state");
            _rpcServerStateMethod
                .AddCustomAttribute<ClientRpcAttribute>()
                .With("excludeOwner", true);

            // TODO: with Exclude Owner set we can probably remove the authority check...
            _rpcServerStateMethod
                .Add(
                    Stm.If(NetworkObjectExpr.This.hasAuthority)
                        .WithTrue(Stm.Return())
                )
                .Add(_bufferMethod.InvokeOnThis(stateParam))
                ;

            AddFixedUpdate(stateFields);
            AddUpdate(stateFields);
        }

        public string Generate()
        {
            CompilationUnitSyntax comp = _namespaceDec.ToCompilationUnit();

            if (!ReflynUtils.CompileSyntax(comp, typeof(PriorityQueue<>), typeof(NetworkBehaviour), /*typeof(ClientRpcAttribute), */typeof(NetworkIdentityExtensions), typeof(DejitterBuffer<>)))
            {
                throw new Exception("Failed build.");
            }

            string str = comp.NormalizeWhitespace().ToFullString();
            string path = "Assets/MirrorState/Runtime/Generated/States";

            Directory.CreateDirectory(path);

            string fullPath = $"{path}/{_className}.cs";

            var writer = new StreamWriter(fullPath, false);
            writer.WriteLine(str);
            writer.Close();

            //Re-import the file to update the reference in the editor
            AssetDatabase.ImportAsset(fullPath);
            return fullPath;
        }

        // MAJOR TODO: Replace string evnt queueing with a Enum that has a base type of `short`, far smaller than sending a string.
        private StateField AddStateFields()
        {
            var stateFieldData = new StateField();

            foreach (var prop in _state.Fields.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
            {
                if (prop.StateType == EntityStateType.Transform)
                {
                    if (_rootTransform.TransformExpr != null)
                    {
                        Debug.LogWarning("Trying to add " + prop.Name +
                                         " to the State but it already has a Transform field.");
                        continue;
                    }

                    // This is essentially required, can't get away with it...
                    if (!prop.Position && !prop.Rotation && !prop.Scale)
                    {
                        throw new Exception(prop.Name + " Requires at least one field selected.");
                    }

                    if (prop.Position)
                    {
                        _rootTransform.StructPosition = _stateStruct.AddField(typeof(Vector3), prop.Name + "Position").ToPublic();
                        _rootTransform.SyncPosition = _stateClassDec.AddField(typeof(Vector3), prop.Name + "Position");
                        _rootTransform.SyncPosition.AddCustomAttribute<SyncVarAttribute>();
                    }

                    if (prop.Rotation)
                    {
                        _rootTransform.StructRotation = _stateStruct.AddField(typeof(Quaternion), prop.Name + "Rotation").ToPublic();
                        _rootTransform.SyncRotation = _stateClassDec.AddField(typeof(Quaternion), prop.Name + "Rotation");
                        _rootTransform.SyncRotation.AddCustomAttribute<SyncVarAttribute>();
                    }

                    // This is only ever localScale.
                    if (prop.Scale)
                    {
                        _rootTransform.StructScale = _stateStruct.AddField(typeof(Vector3), prop.Name + "Scale").ToPublic();
                        _rootTransform.SyncScale = _stateClassDec.AddField(typeof(Vector3), prop.Name + "Scale");
                        _rootTransform.SyncScale.AddCustomAttribute<SyncVarAttribute>();
                    }

                    // defaults to this.transform
                    _rootTransform.TransformExpr = new TransformExpr();
                }
                else if (prop.StateType == EntityStateType.LocalTransform)
                {
                    if (!prop.Position && !prop.Rotation && !prop.Scale)
                    {
                        Debug.LogError(prop.Name + " Requires at least one field selected.");
                        continue;
                    }

                    var childTransform = new ChildTransform();

                    if (prop.Position)
                    {
                        childTransform.StructPosition = _stateStruct
                            .AddField(typeof(Vector3), prop.Name + "LocalPosition")
                            .ToPublic();
                        childTransform.SyncPosition = _stateClassDec.AddField(typeof(Vector3), prop.Name + "LocalPosition");
                        childTransform.SyncPosition.AddCustomAttribute<SyncVarAttribute>();
                    }

                    if (prop.Rotation)
                    {
                        childTransform.StructRotation = _stateStruct
                            .AddField(typeof(Quaternion), prop.Name + "LocalRotation")
                            .ToPublic();

                        childTransform.SyncRotation = _stateClassDec.AddField(typeof(Quaternion), prop.Name + "LocalRotation");
                        childTransform.SyncRotation.AddCustomAttribute<SyncVarAttribute>();
                    }

                    if (prop.Scale)
                    {
                        childTransform.StructScale = _stateStruct
                            .AddField(typeof(Vector3), prop.Name + "LocalScale")
                            .ToPublic();

                        _rootTransform.SyncScale = _stateClassDec.AddField(typeof(Vector3), prop.Name + "LocalScale");
                        _rootTransform.SyncScale.AddCustomAttribute<SyncVarAttribute>();
                    }

                    // Transformed to be assigned in inspector
                    childTransform.TransformDec = _stateClassDec
                            .AddField(typeof(Transform), prop.Name + "LocalTransform")
                            .ToPublic()
                        ;

                    childTransform.TransformExpr = new TransformExpr(childTransform.TransformDec);
                    stateFieldData.Transforms.Add(childTransform);
                }
                else if (prop.StateType == EntityStateType.Trigger)
                {

                    // TODO: Do we want to use 'IsAnim' check here? IE should we let state handle it or should the developer be explicit
                    //          about when he wants the anim to start?
                    //          Answer: Duh, if it's marked as 'IsAnim', state just auto handles, if not the dev can still manually trigger it
                    //                  in the trigger listener
                    var eventName = prop.Name;
                    var eventTriggerName = "Trigger" + prop.Name;

                    _stateClassDec
                        .AddEvent(_stateDelegate, eventName)
                        .ToPublic();

                    _enumEventDec.AddField(eventName);

                    var ifEvnt = Stm.If(Stm.Var(_enumEventDec, "evnt").ToExpr().Identity(Expr.Type(_enumEventDec).Field(eventName)));

                    FieldDeclaration animHash = null;
                    if (prop.IsAnim)
                    {
                        animHash = _stateClassDec
                            .AddField(typeof(int), prop.Name + "_Anim")
                            .ToPrivate()
                            .ToStatic()
                            .ToReadOnly()
                            .EqualTo(Expr.Type(typeof(Animator)).Method("StringToHash").Invoke(Expr.Prim(prop.Name)));

                        ifEvnt
                            .WithTrue(
                                _animDec.ToExpr().Method("SetTrigger").Invoke(Expr.Type(_stateClassDec).Field(animHash))
                            );
                    }

                    ifEvnt
                        .WithTrue(
                            Expr.This.Event(eventName).ConditionalInvoke(Expr.Var("tick")),
                            Stm.Return()
                        );

                    _triggerEventMethod
                        .Add(
                            ifEvnt
                        );

                    MethodDeclaration triggerEvent = _stateClassDec
                            .AddMethod(eventTriggerName)
                            .WithParameter(typeof(uint), "tick")
                            .ToPublic()
                        ;

                    if (!prop.Predicted)
                    {
                        // TODO: Possibly add a Server or ServerCallback Custom Attribute here instead.
                        triggerEvent
                            .Add(
                                Stm.If(MirrorExpr.NetworkServer.active.Identity(false))
                                    .WithTrue(
                                        Stm.Return()
                                    )
                            );

                        // Broken in 2020.2, code above does the check anyway.
                        /*triggerEvent
                            .AddCustomAttribute<ServerAttribute>();*/
                    }
                    else
                    {
                        triggerEvent
                            .Add(
                                Stm.If(NetworkObjectExpr.This.hasAuthority.Identity(false).And(MirrorExpr.NetworkServer.active.Identity(false)))
                                    .WithTrue(
                                        Stm.Return()
                                    )
                            );
                    }

                    if (prop.IsAnim)
                    {
                        // This would be nicer if it was just in Trigger Event and this called TriggerEvent instead of "On<Event>" directly,
                        //  but this is faster at least.
                        triggerEvent
                            .Add(
                                _animDec.ToExpr().Method("SetTrigger").Invoke(Expr.Type(_stateClassDec).Field(animHash))
                            );
                    }

                    triggerEvent
                        .Add(
                            _priorityQueueField.ToExpr().Method("Enqueue")
                                .Invoke(Stm.Var(typeof(uint), "tick"), Expr.Type(_enumEventDec).Field(eventName))
                        )
                        ;

                    if (!prop.Predicted)
                    {
                        triggerEvent
                            .Add(
                                _rpcTriggerEventMethodServer.InvokeOnThis(Expr.Var("tick"), Expr.Type(_enumEventDec).Field(eventName))
                            );
                    }
                    else
                    {
                        triggerEvent
                            .Add(
                                Stm.If(MirrorExpr.NetworkServer.active)
                                    .WithTrue(
                                        _rpcTriggerEventMethod.InvokeOnThis(Expr.Var("tick"), Expr.Type(_enumEventDec).Field(eventName))
                                    )
                            );
                    }
                }
                else if (prop.StateType == EntityStateType.Half)
                {
                    // Half is used for animations
                    string propName = "_" + prop.Name.ToLowerInvariant();

                    var stateField = _stateStruct
                        .AddField(prop.StateType.ToType(), prop.Name)
                        .ToPublic();

                    var classField = _stateClassDec
                        .AddField(typeof(float), prop.Name)
                        .ToPublic();

                    var classPropConverter = _stateClassDec
                        .AddProperty(typeof(ushort), propName)
                        .WithGet(
                            Stm.Return(Expr.Type(typeof(Mathf)).Method("FloatToHalf").Invoke(classField))
                        )
                        .WithSet(
                            Stm.Assign(classField, Expr.Type(typeof(Mathf)).Method("HalfToFloat").Invoke(Expr.Value))
                        )
                        .ToPrivate();

                    var animFieldInst = new StateFieldInfo()
                    {
                        ScriptableStateProperty = prop,
                        StateField = stateField,
                        ClassField = classField,
                        ClassProperty = classPropConverter
                    };

                    //classField.AddCustomAttribute<SyncVarAttribute>();
                    // TODO: Should this be float or ushort?
                    if (prop.Predicted)
                    {
                        var predictField = _predictStruct
                            .AddField(prop.StateType.ToType(), prop.Name)
                            .ToPublic();
                        animFieldInst.PredictField = predictField;
                    }
                    else
                    {
                        var syncField = _syncStruct
                            .AddField(prop.StateType.ToType(), prop.Name)
                            .ToPublic();
                        animFieldInst.PredictField = syncField;
                    }

                    if (prop.IsAnim)
                    {
                        animFieldInst.AnimHash = _stateClassDec
                            .AddField(typeof(int), prop.Name + "_Anim")
                            .ToPrivate()
                            .ToStatic()
                            .ToReadOnly()
                            .EqualTo(Expr.Type(typeof(Animator)).Method("StringToHash").Invoke(Expr.Prim(prop.Name)));
                    }

                    stateFieldData.Fields.Add(animFieldInst);

                }
                else
                {
                    var stateField = _stateStruct
                        .AddField(prop.StateType.ToType(), prop.Name)
                        .ToPublic();

                    var classField = _stateClassDec
                        .AddField(prop.StateType.ToType(), prop.Name)
                        .ToPublic();

                    var stateFieldInfo = new StateFieldInfo()
                    {
                        ScriptableStateProperty = prop,
                        StateField = stateField,
                        ClassField = classField
                    };

                    //classField.AddCustomAttribute<SyncVarAttribute>();
                    if (prop.Predicted)
                    {
                        var predictField = _predictStruct
                            .AddField(prop.StateType.ToType(), prop.Name)
                            .ToPublic();
                        stateFieldInfo.PredictField = predictField;
                    }
                    else
                    {
                        var syncField = _syncStruct
                            .AddField(prop.StateType.ToType(), prop.Name)
                            .ToPublic();
                        stateFieldInfo.PredictField = syncField;
                    }

                    if (prop.IsAnim)
                    {
                        stateFieldInfo.AnimHash = _stateClassDec
                            .AddField(typeof(int), prop.Name + "_Anim")
                            .ToPrivate()
                            .ToStatic()
                            .ToReadOnly()
                            .EqualTo(Expr.Type(typeof(Animator)).Method("StringToHash").Invoke(Expr.Prim(prop.Name)));
                    }

                    stateFieldData.Fields.Add(stateFieldInfo);
                }
            }

            List<Type> interfaces = EntityStateInterface.GetInterfaces(_state.Interfaces);

            foreach (var interfce in interfaces)
            {
                foreach (var evnt in interfce.GetEvents())
                {
                    if (evnt.EventHandlerType != typeof(MirrorStateEvent))
                    {
                        Debug.LogError(evnt.Name + " must use " + nameof(MirrorStateEvent) + " as it's delegate.");
                        continue;
                    }

                    var eventName = evnt.Name;

                    var evntDec = _stateClassDec
                        .AddEvent(typeof(MirrorStateEvent), evnt.Name)
                        .ToPublic();

                    _enumEventDec.AddField(eventName);

                    var eventTriggerName = "Trigger" + evnt.Name;

                    var ifEvnt = Stm.If(Stm.Var(_enumEventDec, "evnt").ToExpr().Identity(Expr.Type(_enumEventDec).Field(eventName)));

                    FieldDeclaration animHash = null;
                    var isAnim = evnt.GetCustomAttribute<StateAnimAttribute>() != null;
                    if (isAnim)
                    {
                        animHash = _stateClassDec
                            .AddField(typeof(int), eventName + "_Anim")
                            .ToPrivate()
                            .ToStatic()
                            .ToReadOnly()
                            .EqualTo(Expr.Type(typeof(Animator)).Method("StringToHash").Invoke(Expr.Prim(eventName)));

                        ifEvnt
                            .WithTrue(
                                _animDec.ToExpr().Method("SetTrigger").Invoke(Expr.Type(_stateClassDec).Field(animHash))
                            );
                    }

                    ifEvnt
                        .WithTrue(
                            Expr.This.Event(eventName).ConditionalInvoke(Expr.Var("tick")),
                            Stm.Return()
                        );

                    _triggerEventMethod
                        .Add(
                            ifEvnt
                        );

                    MethodDeclaration fireEvent = _stateClassDec
                            .AddMethod(eventTriggerName)
                            .WithParameter(typeof(uint), "tick")
                            .ToPublic()
                        ;

                    var isPredicted = evnt.GetCustomAttribute<StatePredictedAttribute>() != null;
                    if (!isPredicted)
                    {
                        // TODO: Possibly add a Server or ServerCallback Custom Attribute here instead.
                        fireEvent
                            .Add(
                                Stm.If(MirrorExpr.NetworkServer.active.Identity(false))
                                    .WithTrue(
                                        Stm.Return()
                                    )
                            );
                        fireEvent
                            .AddCustomAttribute<ServerAttribute>();
                    }
                    else
                    {
                        fireEvent
                            .Add(
                                Stm.If(NetworkObjectExpr.This.hasAuthority.Identity(false).And(MirrorExpr.NetworkServer.active.Identity(false)))
                                    .WithTrue(
                                        Stm.Return()
                                    )
                            );
                    }

                    if (isAnim)
                    {
                        // This would be nicer if it was just in Trigger Event and this called TriggerEvent instead of "On<Event>" directly,
                        //  but this is faster at least.
                        fireEvent
                            .Add(
                                _animDec.ToExpr().Method("SetTrigger").Invoke(Expr.Type(_stateClassDec).Field(animHash))
                            );
                    }

                    fireEvent
                        .Add(
                            _priorityQueueField.ToExpr().Method("Enqueue")
                                .Invoke(Stm.Var(typeof(uint), "tick"), Expr.Type(_enumEventDec).Field(eventName))
                        )
                        ;

                    if (!isPredicted)
                    {
                        fireEvent
                            .Add(
                                _rpcTriggerEventMethodServer.InvokeOnThis(Expr.Var("tick"), Expr.Type(_enumEventDec).Field(eventName))
                            );
                    }
                    else
                    {
                        fireEvent
                            .Add(
                                Stm.If(MirrorExpr.NetworkServer.active)
                                    .WithTrue(
                                        _rpcTriggerEventMethod.InvokeOnThis(Expr.Var("tick"), Expr.Type(_enumEventDec).Field(eventName))
                                    )
                            );
                    }
                }

                // TODO: How to handle MirrorStateEvent properties?
                foreach (var prop in interfce.GetProperties())
                {
                    if (prop.PropertyType == typeof(Transform))
                    {
                        Debug.LogError("State Interfaces do not support Tranforms, please disable these properties for now.");
                        continue;

                        var attr = prop.GetCustomAttribute<StateTransformAttribute>();
                        if (attr == null)
                        {
                            Debug.LogWarning("Property " + prop.Name +
                                             " a Transform that is missing the StateTransform Attribute.");
                            continue;
                        }

                        if (!attr.Position && !attr.Rotation && !attr.Scale)
                        {
                            Debug.LogWarning(prop.Name + " Requires at least one field selected.");
                            continue;
                        }

                        var stateProp = new EntityStateProperty();
                        stateProp.Name = prop.Name;
                        stateProp.IsAnim = false;
                        stateProp.Predicted =
                            true; // Defaults to true I believe, and I think for Transforms is unused anyway.
                        stateProp.Position = attr.Position;
                        stateProp.Rotation = attr.Rotation;
                        stateProp.Scale = attr.Scale;
                        stateProp.StateType = EntityStateExtensions.FromProperty(prop);

                        // TODO: Unused until regular props are setup.
                    }
                    else if (prop.PropertyType == typeof(MirrorStateEvent))
                    {
                        Debug.LogError(prop.Name + " should not be a property, instead write 'event MirrorStateEvent " + prop.Name + "' in the interface.");
                        continue;
                    }
                    else if (prop.PropertyType?.BaseType?.BaseType == typeof(StateScriptableBase)) // CustomScriptable -> StateScriptableObject<T> -> StateScriptableBase
                    {
                        MethodInfo info = prop.PropertyType.GetMethod("GetScriptable", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

                        if (info == null)
                        {
                            throw new NullReferenceException(prop.Name + " requires a static method called GetScriptable that returns " + prop.PropertyType + " and takes a byte as an argument.");
                            //continue;
                        }

                        var stateFieldInfo = new StateFieldInfo();

                        var stateProp = new EntityStateProperty();
                        stateProp.Name = prop.Name;
                        stateProp.IsAnim = false;
                        stateProp.Predicted = false;
                        stateProp.StateType = EntityStateType.Byte; // If you need more than byte we'll need something more to be flexible.

                        Type dataType = typeof(byte);
                        var stateField = _stateStruct
                            .AddField(dataType, prop.Name)
                            .ToPublic();

                        var syncField = _syncStruct
                            .AddField(dataType, prop.Name)
                            .ToPublic();
                        stateFieldInfo.PredictField = syncField;

                        var classProp = _stateClassDec
                            .AddProperty(prop.PropertyType, prop.Name)
                            .WithGet(
                                Stm.Return(Expr.Type(prop.PropertyType).Method("GetScriptable").Invoke(Expr.This.Field(_syncStructField).Field(syncField)))
                            )
                            .WithSet(
                                Stm.Assign(Expr.This.Field(_syncStructField).Field(syncField), Expr.Value.Method("ToIndex").Invoke())
                            )
                            .ToPublic();

                        stateFieldInfo.ClassProperty = classProp;
                        stateFieldInfo.ScriptableStateProperty = stateProp;
                        stateFieldInfo.StateField = stateField;

                        stateFieldData.Fields.Add(stateFieldInfo);
                        //throw new NotImplementedException();
                    }
                    else if(prop.PropertyType.IsValueType)
                    {
                        var stateFieldInfo = new StateFieldInfo();

                        var stateProp = new EntityStateProperty();
                        stateProp.Name = prop.Name;
                        stateProp.IsAnim = prop.GetCustomAttribute<StateAnimAttribute>() != null;
                        stateProp.Predicted = prop.GetCustomAttribute<StatePredictedAttribute>() != null;
                        stateProp.StateType = EntityStateExtensions.FromProperty(prop);

                        var stateField = _stateStruct
                            .AddField(stateProp.StateType.ToType(), prop.Name)
                            .ToPublic();

                        // This is tracked on the SyncState field, we don't need an additional field on the root object.
                        /*var classField = _stateClassDec
                            .AddField(stateProp.StateType.ToType(), "_" + prop.Name.ToLowerInvariant())
                            .ToPrivate();*/

                        if (stateProp.Predicted)
                        {
                            var predictField = _predictStruct
                                .AddField(stateProp.StateType.ToType(), prop.Name)
                                .ToPublic();
                            stateFieldInfo.PredictField = predictField;

                            // Only predicted is used. The server will write the predicted to the SyncVar in FixedUpdate, and the non-authority observers will write to it in SetTransitionStates.
                            var classProp = _stateClassDec
                                .AddProperty(stateProp.StateType.ToType(), prop.Name)
                                .WithGet(
                                    Stm.Return(Expr.This.Field(_predictStructField).Field(predictField))
                                )
                                .WithSet(
                                    Stm.Assign(Expr.This.Field(_predictStructField).Field(predictField), Expr.Value)
                                )
                                .ToPublic();

                            stateFieldInfo.ClassProperty = classProp;
                        }
                        else
                        {
                            var syncField = _syncStruct
                                .AddField(stateProp.StateType.ToType(), prop.Name)
                                .ToPublic();
                            stateFieldInfo.PredictField = syncField;

                            var classProp = _stateClassDec
                                .AddProperty(stateProp.StateType.ToType(), prop.Name)
                                .WithGet(
                                    Stm.Return(Expr.This.Field(_syncStructField).Field(syncField))
                                )
                                .WithSet(
                                    Stm.Assign(Expr.This.Field(_syncStructField).Field(syncField), Expr.Value)
                                )
                                .ToPublic();

                            stateFieldInfo.ClassProperty = classProp;
                        }

                        stateFieldInfo.ScriptableStateProperty = stateProp;
                        stateFieldInfo.StateField = stateField;
                        //stateFieldInfo.ClassField = classField;

                        if (stateProp.IsAnim)
                        {
                            stateFieldInfo.AnimHash = _stateClassDec
                                .AddField(typeof(int), prop.Name + "_Anim")
                                .ToPrivate()
                                .ToStatic()
                                .ToReadOnly()
                                .EqualTo(
                                    Expr.Type(typeof(Animator)).Method("StringToHash").Invoke(Expr.Prim(prop.Name)));
                        }

                        stateFieldData.Fields.Add(stateFieldInfo);
                    }
                    else
                    {
                        throw new NotImplementedException("The type " + prop.PropertyType.Name + " is not implemented.");
                    }
                }

                _stateClassDec.AddInterface(interfce);
            }


            // TODO: Add in Interface fields / Properties.

            return stateFieldData;
        }

        private void AddFixedUpdate(StateField stateFields)
        {

            var fixedUpdateMethod = _stateClassDec.AddMethod("FixedUpdate");

            /*var fixedUpdateMethod = _stateClassDec
                .AddMethod("FixedUpdate")
                .ToProtected()
                .ToOverride();

            fixedUpdateMethod.CallBaseWithParameters();*/

            VariableDeclarationStatement usedTickDec = Stm.Var(typeof(uint), "usedTick").EqualTo(0);

            fixedUpdateMethod
                .Add(
                    usedTickDec,
                    Stm.If(NetworkObjectExpr.This.HasAnyAuthority())
                        .WithTrue(
                            Stm.Assign(usedTickDec, TickSystemExpr.Instance.Tick)
                        )
                        .WithFalse(
                            Stm.If(MirrorExpr.NetworkServer.active)
                                .WithTrue(
                                    Stm.Assign(usedTickDec, TickSystemExpr.Instance.Tick - 1)
                                )
                                .WithFalse(
                                    Stm.Assign(usedTickDec, TickSystemExpr.Instance.DelayTick)
                                )
                        )
                );

            var newStateDec = Stm.Var(_stateStruct, "newState").EqualTo(Expr.New(_stateStruct));

            if (stateFields.Fields.Any(x => x.ScriptableStateProperty.IsAnim))
            {
                var ifClient = Stm.If(NetworkObjectExpr.This.isClient);
                foreach (var stateFieldInfo in stateFields.Fields)
                {
                    if (!stateFieldInfo.ScriptableStateProperty.IsAnim)
                    {
                        continue;
                    }

                    if (stateFieldInfo.ScriptableStateProperty.StateType == EntityStateType.Half || stateFieldInfo.ScriptableStateProperty.StateType == EntityStateType.Float)
                    {
                        var structType = stateFieldInfo.ScriptableStateProperty.Predicted ? _predictStructField : _syncStructField;

                        ifClient
                            .WithTrue(
                                _animDec.ToExpr().Method("SetFloat").Invoke(Expr.Type(_stateClassDec).Field(stateFieldInfo.AnimHash), structType.ToExpr().Field(stateFieldInfo.StateField))
                            );
                    }
                }

                fixedUpdateMethod.Add(ifClient);
            }

            var getNewStateMethod = _stateClassDec.AddMethod("GetNewState")
                    .SetReturnType(_stateStruct);

            var tickParam = getNewStateMethod
                .AddParameter(typeof(uint), "tick");

            getNewStateMethod
                    .Add(
                        newStateDec,
                        Stm.Assign(newStateDec.ToExpr().Field(_cmdTickDec), Expr.Var("tick"))
                    )
                ;

            #region Root Transform Buffering

            if (_rootTransform.StructPosition != null)
            {
                getNewStateMethod
                    .Add(
                        Stm.Assign(newStateDec.ToExpr().Field(_rootTransform.StructPosition), _rootTransform.TransformExpr.position)
                    //, Stm.Assign(_syncPos, newStateDec.ToExpr().Field(_rootTransform.StructPosition))
                    );
            }

            if (_rootTransform.StructRotation != null)
            {
                getNewStateMethod
                    .Add(
                        Stm.Assign(newStateDec.ToExpr().Field(_rootTransform.StructRotation), _rootTransform.TransformExpr.rotation)
                    //, Stm.Assign(_syncRot, newStateDec.ToExpr().Field(_rootTransform.StructRotation))
                    );
            }

            if (_rootTransform.StructScale != null)
            {
                getNewStateMethod
                    .Add(
                        Stm.Assign(newStateDec.ToExpr().Field(_rootTransform.StructScale), _rootTransform.TransformExpr.localScale)
                    );
            }

            #endregion

            #region Child Transform Buffering

            foreach (var child in stateFields.Transforms)
            {
                if (child.StructPosition != null)
                {
                    getNewStateMethod
                        .Add(
                            Stm.Assign(newStateDec.ToExpr().Field(child.StructPosition), child.TransformExpr.localPosition)
                        );
                }

                if (child.StructRotation != null)
                {
                    getNewStateMethod
                        .Add(
                            Stm.Assign(newStateDec.ToExpr().Field(child.StructRotation), child.TransformExpr.localRotation)
                        );
                }

                if (child.StructScale != null)
                {
                    getNewStateMethod
                        .Add(
                            Stm.Assign(newStateDec.ToExpr().Field(child.StructScale), child.TransformExpr.localScale)
                        );
                }
            }

            #endregion

            foreach (var stateFieldInfo in stateFields.Fields)
            {
                getNewStateMethod
                    .Add(
                        Stm.Assign(
                            newStateDec.ToExpr().Field(stateFieldInfo.StateField),
                            Expr.This.Field(stateFieldInfo.ScriptableStateProperty.Predicted ? _predictStructField : _syncStructField).Field(stateFieldInfo.PredictField)
                        )
                    );
            }

            getNewStateMethod
                .Add(
                    Stm.Return(newStateDec)
                );

            var getAtMethod = _stateClassDec.AddMethod("GetAt")
                .ToPublic()
                .WithParameter(typeof(uint), "tick")
                .SetReturnType(_stateStruct)
                .Add(
                    Stm.If(NetworkObjectExpr.This.hasAuthority)
                        .WithTrue(
                            Stm.Return(getNewStateMethod.InvokeOnThis(Expr.Var("tick")))
                        ),

                    Stm.Return(_stateDejitterDec.ToExpr().Method("GetLatestAt").Invoke(Expr.Var("tick")))
                );


            var ifServerOrAuthority = Stm
                .If(Expr.Parens(MirrorExpr.NetworkServer.active.Or(NetworkObjectExpr.This.hasAuthority)).And(TickSystemExpr.Instance.Tick >= _nextUpdateDec))
                .WithTrue(
                    Stm.Assign(_nextUpdateDec, TickSystemExpr.Instance.Tick + _updateRateDec),
                    Stm.VarVar("newState").EqualTo(getNewStateMethod.InvokeOnThis(usedTickDec)),
                    _bufferMethod.InvokeOnThis(newStateDec)
                );

            ifServerOrAuthority.WithTrue(
                Stm.If(MirrorExpr.NetworkServer.active)
                    .WithTrue(
                        _rpcServerStateMethod.InvokeOnThis(newStateDec)
                    )
            );

            fixedUpdateMethod
                .Add(
                    ifServerOrAuthority
                );

            fixedUpdateMethod
                .Add(
                    Stm.While(_priorityQueueField.ToExpr().Method("Any").Invoke().And(_priorityQueueField.ToExpr().Method("Peek").Invoke().Field("Key") <= usedTickDec))
                        .With(
                            Stm.VarVar("evnt").EqualTo(_priorityQueueField.ToExpr().Method("Dequeue").Invoke()),
                            _triggerEventMethod.InvokeOnThis(Expr.Var("evnt").Prop("Key"), Expr.Var("evnt").Prop("Value"))
                        )
                );

        }

        private void AddUpdate(StateField stateFields)
        {
            var updateMethod = _stateClassDec.AddMethod("Update");

            var rotSmoothDec
                = _stateClassDec
                    .AddField(typeof(float), "RotationSmooth")
                    .ToPublic()
                    .EqualTo(15f);
            var lerpAmtVar = Stm.Var(typeof(float), "lerpAmt").EqualTo(_timeElapsedDec.ToExpr() / _timeToTargetDec);
            var latestAt = Stm.VarVar("latest").EqualTo(_stateDejitterDec.ToExpr().Method("GetLatestAt").Invoke(TickSystemExpr.Instance.DelayTick));
            var current = Stm.Var(_stateStruct, "current");
            var next = Stm.Var(_stateStruct, "next");

            // TODO: Do we want to move the tick check loop to fixed update? It'd make more sense there since we know that's when a tick is updated.
            // Get first at or before delay, or the last one.
            updateMethod
                .Add(
                    Stm.If(MirrorExpr.NetworkServer.active.Or(
                            NetworkObjectExpr.This.hasAuthority.Or(_stateCount.ToExpr().Identity(0))))
                        .WithTrue(
                            Stm.Return()
                        )
                )
                .Add(
                    Stm.If(_stateDejitterDec.ToExpr().Prop("Latest").Field(_cmdTickDec) > _delayTick)
                        .WithTrue(
                            latestAt,
                            Stm.IfNotIdentity(latestAt.ToExpr().Field("Tick"), 0)
                                .WithTrue(
                                    current,
                                    next,
                                    _stateDejitterDec.ToExpr().Method("GetFirstAfter").Invoke(latestAt.ToExpr().Field("Tick"), Expr.Arg(current, FieldDirectionReflyn.Out), Expr.Arg(next, FieldDirectionReflyn.Out)),
                                    _setTransStatesMethod.InvokeOnThis(current, next)
                                )
                        ),
                    Stm.Assign(_timeElapsedDec, _timeElapsedDec + UnityExpr.Time.deltaTime),
                    lerpAmtVar
                );


            if (_rootTransform != null)
            {
                if (_rootTransform.StructPosition != null)
                {
                    updateMethod.Add(
                        Stm.Assign(_rootTransform.TransformExpr.position,
                            UnityExpr.Vector3.Lerp(_fromStateDec.ToExpr().Field(_rootTransform.StructPosition),
                                _toStateDec.ToExpr().Field(_rootTransform.StructPosition), lerpAmtVar))
                    );
                }

                if (_rootTransform.StructRotation != null)
                {
                    updateMethod.Add(
                        Stm.Assign(_rootTransform.TransformExpr.rotation,
                            UnityExpr.Quaternion.Slerp(_fromStateDec.ToExpr().Field(_rootTransform.StructRotation),
                                _toStateDec.ToExpr().Field(_rootTransform.StructRotation),
                                UnityExpr.Time.deltaTime * rotSmoothDec))
                    );
                }

                if (_rootTransform.StructScale != null)
                {
                    updateMethod.Add(
                        Stm.Assign(_rootTransform.TransformExpr.localScale,
                            UnityExpr.Vector3.Lerp(_fromStateDec.ToExpr().Field(_rootTransform.StructScale),
                                _toStateDec.ToExpr().Field(_rootTransform.StructScale), lerpAmtVar))
                    );
                }
            }

            foreach (var child in stateFields.Transforms)
            {
                if (child.StructPosition != null)
                {
                    updateMethod.Add(
                        Stm.Assign(child.TransformExpr.localPosition,
                            UnityExpr.Vector3.Lerp(_fromStateDec.ToExpr().Field(child.StructPosition), _toStateDec.ToExpr().Field(child.StructPosition), lerpAmtVar))
                    );
                }

                if (child.StructRotation != null)
                {
                    updateMethod.Add(
                        Stm.Assign(child.TransformExpr.localRotation,
                            UnityExpr.Quaternion.Slerp(_fromStateDec.ToExpr().Field(child.StructRotation), _toStateDec.ToExpr().Field(child.StructRotation), UnityExpr.Time.deltaTime * rotSmoothDec))
                    );
                }

                if (child.StructScale != null)
                {
                    updateMethod.Add(
                        Stm.Assign(child.TransformExpr.localScale,
                            UnityExpr.Vector3.Lerp(_fromStateDec.ToExpr().Field(child.StructScale), _toStateDec.ToExpr().Field(child.StructScale), lerpAmtVar))
                    );
                }
            }

            /*if (stateFields.Fields.Count > 0)
            {
                // REQUIRES TEST
                foreach (var stateFieldInfo in stateFields.Fields)
                {
                    updateMethod
                        .Add(
                            // Class property handles transform from ushort to float and back.
                            //  If it has no property it doesn't need to be converted.
                            Stm.Assign(
                                stateFieldInfo.ClassProperty?.ToExpr() ?? stateFieldInfo.ClassField.ToExpr(),
                                _toStateDec.ToExpr().Field(stateFieldInfo.StateField)
                            )
                        );
                }
            }*/

            /*updateMethod.Add(
            Stm.While(priorityQueueField.ToExpr().Method("Any").Invoke().And(priorityQueueField.ToExpr().Method("Peek").Invoke().Field("Key") <= delayTick))
                .With(
                    Stm.VarVar("evnt").EqualTo(priorityQueueField.ToExpr().Method("Dequeue").Invoke()),
                    triggerEventMethod.InvokeOnThis(Expr.Var("evnt").Prop("Key"), Expr.Var("evnt").Prop("Value"))
                )
        );*/

            if (_state.Tracked)
            {
                var savedStateDec = _stateClassDec
                    .AddField(_stateStruct, "_savedState");

                var tickVar = Expr.Var("tick");
                var stateVar = Expr.Var("state");
                var stateVarDec = Stm.Var(_stateStruct, "state");

                var setToStateMethod = _stateClassDec.AddMethod("SetToState")
                    .WithParameter(_stateStruct, "state");

                if (_rootTransform.StructPosition != null)
                {
                    setToStateMethod.Add(
                        Stm.Assign(_rootTransform.TransformExpr.position, stateVar.Field(_rootTransform.StructPosition))
                    );
                }

                if (_rootTransform.StructRotation != null)
                {
                    setToStateMethod.Add(
                        Stm.Assign(_rootTransform.TransformExpr.rotation, stateVar.Field(_rootTransform.StructRotation))
                    );
                }

                if (_rootTransform.StructScale != null)
                {
                    setToStateMethod.Add(
                        Stm.Assign(_rootTransform.TransformExpr.localScale, stateVar.Field(_rootTransform.StructScale))
                    );
                }


                foreach (var child in stateFields.Transforms)
                {
                    if (child.StructPosition != null)
                    {
                        setToStateMethod.Add(
                            Stm.Assign(child.TransformExpr.localPosition, stateVar.Field(child.StructPosition))
                        );
                    }

                    if (child.StructRotation != null)
                    {
                        setToStateMethod.Add(
                            Stm.Assign(child.TransformExpr.localRotation, stateVar.Field(child.StructRotation))
                        );
                    }

                    if (child.StructScale != null)
                    {
                        setToStateMethod.Add(
                            Stm.Assign(child.TransformExpr.localScale, stateVar.Field(child.StructScale))
                        );
                    }
                }


                var restoreMethod = _stateClassDec.AddMethod("Restore")
                    .ToPublic()
                    .Add(
                        setToStateMethod.InvokeOnThis(savedStateDec)
                    );

                _stateClassDec
                    .AddMethod("Rollback")
                    .ToPublic()
                    .WithParameter(typeof(uint), "tick")
                    .Add(
                        stateVarDec,
                        Stm.If(tickVar > TickSystemExpr.Instance.Tick)
                            .WithTrue(
                                UnityExpr.Debug.LogError("Trying to rollback to a future state."),
                                Stm.Return()
                            ),
                        StmCommon.IfNotTryGet(_stateDejitterDec, tickVar, stateVar)
                            .WithTrue(
                                Stm.If(tickVar < _stateDejitterDec.ToExpr().Prop("Last").Field(_cmdTickDec))
                                    .WithTrue(
                                        Stm.Assign(stateVar, _stateDejitterDec.ToExpr().Prop("Last")),
                                        UnityExpr.Debug.LogWarning("Entity exceeded oldest rollback state, default to last")
                                    )
                                    .WithFalse(
                                        Stm.Assign(stateVar, _stateDejitterDec.ToExpr().Method("GetLatestAt").Invoke(tickVar)),
                                        UnityExpr.Debug.LogError("Unable to find appropriate tick for rollback, using nearest State.") // This error might account for the weird rollback issues I've seen with the tank and the cube?
                                    )
                            ),
                        // TODO: This could be the source of the off by 1 rollback, if this doesn't have the latest when rollback is called...
                        Stm.Assign(savedStateDec, _stateDejitterDec.ToExpr().Prop("Latest")),
                        setToStateMethod.InvokeOnThis(stateVar)
                    );
            }
        }

    }
}
