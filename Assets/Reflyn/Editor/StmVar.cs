using Reflyn.Refly.CodeDom;
using Reflyn.Refly.CodeDom.Expressions;
using Reflyn.Refly.CodeDom.Statements;

public static class StmVar
{
    public static VariableDeclarationStatement Index1 => Stm.Var(typeof(int), "i").EqualTo(0);
}

public static class StmAssign
{
    public static AssignStatement Increment(Expression expr) => Stm.Assign(expr, expr + 1);
    public static AssignStatement Decrement(Expression expr) => Stm.Assign(expr, expr - 1);

    public static AssignStatement Index1Increment => Increment(StmVar.Index1);
    public static AssignStatement Index1Decrement => Decrement(StmVar.Index1);
}