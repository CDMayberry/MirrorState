using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using MirrorState.Reflyn.Editor;
using MirrorState.Scripts;
using MirrorState.Scripts.Rollback;
using Reflyn.Refly.CodeDom.Expressions;
using UnityEngine;


public static class MirrorExpr
{
    public static NetworkTimeExpr NetworkTime => new NetworkTimeExpr();

    public class NetworkTimeExpr : UnityExprTypeBase
    {
        public NativePropertyReferenceExpression time => ToExpr().Prop("time");


        public override Type Type => typeof(NetworkTime);
    }
    public static NetworkServerExpr NetworkServer => new NetworkServerExpr();

    public class NetworkServerExpr : UnityExprTypeBase
    {
        public NativePropertyReferenceExpression active => ToExpr().Prop("active");

        public override Type Type => typeof(NetworkServer);
    }

}

public class TickSystemExpr : UnityExprTypeBase<TickSystem>
{
    public static InstanceExpr Instance => new InstanceExpr();

    public static NativePropertyReferenceExpression SecsPerTick => ToType().Field("SecsPerTick");

    public class InstanceExpr : UnityExprInstance<TickSystem>
    {
        public NativePropertyReferenceExpression Tick => ToExpr().Field("Tick");
        public NativePropertyReferenceExpression DelayTick => ToExpr().Field("delayTick");
        public NativePropertyReferenceExpression ServerTick => ToExpr().Field("ServerTick");
    }

}

public class RollbackSystemExpr : UnityExprTypeBase<RollbackSystem>
{
    public static InstanceExpr Instance => new InstanceExpr();

    public class InstanceExpr : UnityExprInstance<RollbackSystem>
    {
        public MethodInvokeExpression Register(Expression tracked) => ToExpr().Method("Register").Invoke(tracked);
        public MethodInvokeExpression Unregister(Expression tracked) => ToExpr().Method("Unregister").Invoke(tracked);
    }
}
