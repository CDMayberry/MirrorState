using System;
using Reflyn.Refly.CodeDom;
using Reflyn.Refly.CodeDom.Expressions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MirrorState.Reflyn.Editor
{
    public class UnityExprTypeBase<T>
    {

        public static implicit operator Expression(UnityExprTypeBase<T> dec) => dec.ToExpr();
        public static Expression ToType() => Expr.Type(typeof(T));
        public Expression ToExpr() => ToType();
    }

    public abstract class UnityExprTypeBase
    {
        public abstract Type Type { get; }

        public static implicit operator Expression(UnityExprTypeBase dec) => dec.ToExpr();
        public Expression ToExpr() => Expr.Type(Type);
    }


    public abstract class UnityExprThis
    {
        public static implicit operator Expression(UnityExprThis dec) => dec.ToExpr();
        public Expression ToExpr() => Expr.This;
    }
    public abstract class UnityExprInstance<T>
    {
        public static implicit operator Expression(UnityExprInstance<T> dec) => dec.ToExpr();
        public Expression ToExpr() => Expr.Type(typeof(T)).Prop("Instance");
    }

    public static class UnityExpr
    {
        public static MathfExpr Mathf => new MathfExpr();
        public class MathfExpr : UnityExprTypeBase<Mathf>
        {
            public Expression Max(Expression left, Expression right) => ToExpr().Method("Max").Invoke(left, right);
            public Expression Min(Expression left, Expression right) => ToExpr().Method("Min").Invoke(left, right);
        }

        public static Vector3Expr Vector3 => new Vector3Expr();

        public class Vector3Expr : UnityExprTypeBase<Vector3>
        {
            public Expression Lerp(Expression vector1, Expression vector2, Expression t) => ToExpr().Method("Lerp").Invoke(vector1, vector2, t);
            public Expression zero => ToExpr().Prop("zero");
        }

        public static QuaternionExpr Quaternion => new QuaternionExpr();

        public class QuaternionExpr : UnityExprTypeBase<Quaternion>
        {
            public Expression Lerp(Expression quaternion1, Expression quaternion2, Expression tFloat) => ToExpr().Method("Lerp").Invoke(quaternion1, quaternion2, tFloat);
            public Expression Slerp(Expression quaternion1, Expression quaternion2, Expression tFloat) => ToExpr().Method("Slerp").Invoke(quaternion1, quaternion2, tFloat);
            public Expression identity => ToExpr().Prop("identity");
        }

        public static TimeExpr Time => new TimeExpr();

        public class TimeExpr : UnityExprTypeBase<Time>
        {
            public Expression deltaTime => ToExpr().Prop("deltaTime");
            public Expression fixedDeltaTime => ToExpr().Prop("fixedDeltaTime");
        }

        public static DebugExpr Debug => new DebugExpr();

        // TODO: Add variant's for formatting that take an Expression
        public class DebugExpr : UnityExprTypeBase<Debug>
        {
            public Expression Log(string message) => ToExpr().Method("Log").Invoke(Expr.Prim(message));
            public Expression LogWarning(string message) => ToExpr().Method("LogWarning").Invoke(Expr.Prim(message));
            public Expression LogError(string message) => ToExpr().Method("LogError").Invoke(Expr.Prim(message));
        }
    }

    public static class GameObjectExpr
    {
        public static TransformExpr transform => new TransformExpr();

        public static MethodInvokeExpression GetComponent<T>() where T : Object => Expr.This.GenericMethod<T>("GetComponent").Invoke();
        public static MethodInvokeExpression GetComponent(Type type) => Expr.This.GenericMethod("GetComponent", type).Invoke();
        public static MethodInvokeExpression GetComponent(string type) => Expr.This.GenericMethod("GetComponent", type).Invoke();
        //public static MethodInvokeExpression GetComponent(Type type) where T : Object => Expr.This.GenericMethod<T>("GetComponent").Invoke();
    }

    public class TransformExpr
    {
        public Expression Owner { get; }

        public TransformExpr(Expression owner)
        {
            Owner = owner;
        }

        public TransformExpr()
        {
            Owner = Expr.This.Field("transform");
        }

        public NativePropertyReferenceExpression position => Owner.Prop("position");
        public NativePropertyReferenceExpression localPosition => Owner.Prop("localPosition");
        public NativePropertyReferenceExpression rotation => Owner.Prop("rotation");
        public NativePropertyReferenceExpression localRotation => Owner.Prop("localRotation");
        public NativePropertyReferenceExpression lossyScale => Owner.Prop("lossyScale");
        public NativePropertyReferenceExpression localScale => Owner.Prop("localScale");


        public static implicit operator Expression(TransformExpr dec) => dec.Owner;
    }

    public static class NetworkObjectExpr
    {
        public static ExprThis This => new ExprThis();

        public class ExprThis : UnityExprThis
        {
            public NativePropertyReferenceExpression hasAuthority => ToExpr().Prop("hasAuthority");
            public MethodInvokeExpression HasAnyAuthority() => ToExpr().Method("HasAnyAuthority").Invoke();
            public NativePropertyReferenceExpression isClientOnly => ToExpr().Prop("isClientOnly");
            public NativePropertyReferenceExpression isClient => ToExpr().Prop("isClient");
        }
    }
}
