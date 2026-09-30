using System;

namespace net.puk06.AnimScript
{
    internal enum BinaryOp
    {
        Add,
        Sub,
        Mul,
        Div,
        Mod,
    }

    /// <summary>
    /// ビルド時に評価される数値式のAST。
    /// `1 + 2`、`$i`、`$i * 2` など。ループ展開（AstExpander）の時点で
    /// すべて <see cref="ConstExpr"/> に置き換わる。
    ///
    /// 生成器側は <see cref="Const"/> で値を取り出すだけでよい。
    /// </summary>
    internal abstract class ExprAst
    {
        public SourceLocation Location { get; set; }

        /// <summary>変数スコープを使って評価する。失敗時は診断にエラーを積んで 0 を返す。</summary>
        public abstract double Eval(VarScope scope, DiagnosticBag diagnostics);

        /// <summary>
        /// 展開済みの定数から値を取り出す。
        /// AstExpander 通過後はすべて ConstExpr になっている前提（内部不変条件）。
        /// </summary>
        public double Const()
        {
            if (this is ConstExpr constant) return constant.Value;
            throw new InvalidOperationException("展開されていない式があります（内部エラー）");
        }
    }

    /// <summary>定数。`1`、`2.5` など。</summary>
    internal sealed class ConstExpr : ExprAst
    {
        public double Value { get; }

        public ConstExpr(double value, SourceLocation location = default)
        {
            Value = value;
            Location = location;
        }

        public override double Eval(VarScope scope, DiagnosticBag diagnostics) => Value;
    }

    /// <summary>変数参照。`$i`。</summary>
    internal sealed class VarExpr : ExprAst
    {
        public string Name { get; }

        public VarExpr(string name, SourceLocation location)
        {
            Name = name;
            Location = location;
        }

        public override double Eval(VarScope scope, DiagnosticBag diagnostics)
        {
            if (scope.TryGet(Name, out var value)) return value;

            diagnostics.Error(Location,
                $"変数「{Name}」は宣言されていません。先に「var {Name} = 0」のように宣言してください");
            return 0;
        }
    }

    /// <summary>二項演算。`a + b` など。負数は `0 - x` として表現する。</summary>
    internal sealed class BinaryExpr : ExprAst
    {
        public ExprAst Left { get; }
        public BinaryOp Op { get; }
        public ExprAst Right { get; }

        public BinaryExpr(ExprAst left, BinaryOp op, ExprAst right, SourceLocation location)
        {
            Left = left;
            Op = op;
            Right = right;
            Location = location;
        }

        public override double Eval(VarScope scope, DiagnosticBag diagnostics)
        {
            var left = Left.Eval(scope, diagnostics);
            var right = Right.Eval(scope, diagnostics);

            switch (Op)
            {
                case BinaryOp.Add: return left + right;
                case BinaryOp.Sub: return left - right;
                case BinaryOp.Mul: return left * right;
                case BinaryOp.Div:
                    if (right == 0)
                    {
                        diagnostics.Error(Location, "0 で割ることはできません");
                        return 0;
                    }
                    return left / right;
                case BinaryOp.Mod:
                    if (right == 0)
                    {
                        diagnostics.Error(Location, "0 で割った余りは計算できません");
                        return 0;
                    }
                    return left % right;
                default:
                    return 0;
            }
        }
    }
}
