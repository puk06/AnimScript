using System.Collections.Generic;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// 遷移定義。`A -> B -> C when X dur 0.2 offset 0.1` のような1行。
    ///
    /// Chain はノード名の並び（"entry" / "exit" / "any" / ステート名。$変数を含むことがある）。
    /// Chain.Count >= 2 が Parser により保証される。
    /// 先頭と末尾が同じステートなら「ループ」（環状の遷移）になる。
    /// </summary>
    internal sealed class TransitionAst : IBlockItem
    {
        public List<string> Chain { get; } = new List<string>();

        /// <summary>when の条件式（and / or / 括弧 を含む木）。無条件なら null。</summary>
        public ConditionExprAst Condition { get; set; }

        /// <summary>exitTime 指定。null なら hasExitTime = false。</summary>
        public ExprAst ExitTime { get; set; }

        /// <summary>dur 指定（遷移時間・秒）。</summary>
        public ExprAst Duration { get; set; }

        /// <summary>offset 指定（遷移先クリップの再生位置）。</summary>
        public ExprAst Offset { get; set; }

        /// <summary>any からの遷移で自分自身への遷移も許可するか。</summary>
        public bool AllowSelf { get; set; }

        public SourceLocation Location { get; set; }
    }
}
