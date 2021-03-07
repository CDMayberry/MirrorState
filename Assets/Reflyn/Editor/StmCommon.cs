using Reflyn.Refly;
using Reflyn.Refly.CodeDom;
using Reflyn.Refly.CodeDom.Expressions;
using Reflyn.Refly.CodeDom.Statements;

namespace Assets.MirrorState.Reflyn
{
    public static class StmCommon
    {
        public static ConditionStatement IfTryGet(Expression expr, Expression tryValue, Declaration outValue)
        {
            return Stm.If(TryGet(expr, tryValue, outValue));
        }

        public static ConditionStatement IfNotTryGet(Expression expr, Expression tryValue, Declaration outValue)
        {
            return Stm.If(TryGet(expr, tryValue, outValue).Identity(false));
        }

        public static ConditionStatement IfTryGet(Expression expr, Expression tryValue, VariableReferenceExpression outValue)
        {
            return Stm.If(TryGet(expr, tryValue, outValue));
        }

        public static ConditionStatement IfNotTryGet(Expression expr, Expression tryValue, VariableReferenceExpression outValue)
        {
            return Stm.If(TryGet(expr, tryValue, outValue).Identity(false));
        }

        public static MethodInvokeExpression TryGet(Expression expr, Expression tryValue, Declaration outValue)
        {
            return expr.Method("TryGet").Invoke(tryValue, Expr.Arg(outValue, FieldDirectionReflyn.Out));
        }

        public static MethodInvokeExpression TryGet(Expression expr, Expression tryValue, VariableReferenceExpression outValue)
        {
            return expr.Method("TryGet").Invoke(tryValue, Expr.Arg(outValue, FieldDirectionReflyn.Out));
        }
    }
}
