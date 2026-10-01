using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// ループ展開器（ビルド時マクロ実行の本体）。
    ///
    /// Parser が作った「そのままのAST」（Items）を読み、
    /// for / while / var / 代入 を実行しながら、生成器がそのまま使える
    /// 平坦なAST（Parameters / Layers / States / Transitions / Actions）を組み立てる。
    ///
    /// 展開後はすべての ExprAst が ConstExpr になり、
    /// 文字列・名前に含まれる $変数 も展開済みになる。
    /// ループ内の要素は回数分だけ「別オブジェクトとして」複製される
    /// （同じインスタンスを共有しないので、クリップ解決などが衝突しない）。
    /// </summary>
    internal sealed class AstExpander
    {
        const int MaxLoopIterations = 512;    // ループ1個あたりの回数上限（無限ループ防止）
        const int MaxTotalExpansions = 4096;  // ネストしたループ全体の暴走への保険
        const double Epsilon = 1e-9;          // for の浮動小数点誤差対策

        readonly DiagnosticBag _diagnostics;
        int _totalExpansions;
        bool _limitReported;

        AstExpander(DiagnosticBag diagnostics)
        {
            _diagnostics = diagnostics;
        }

        /// <summary>AST のループをすべて展開する。エラーは diagnostics に蓄積される。</summary>
        public static void Expand(AnimScriptAst ast, DiagnosticBag diagnostics)
            => new AstExpander(diagnostics).ExpandScript(ast);

        // ================================================================
        // ブロックの展開
        // ================================================================

        void ExpandScript(AnimScriptAst ast)
        {
            var scope = new VarScope(null);
            ExpandItems(ast.Items, scope, (item, itemScope) =>
            {
                switch (item)
                {
                    case ParameterAst parameter:
                        ast.Parameters.Add(ExpandParameter(parameter, itemScope));
                        break;
                    case LayerAst layer:
                        ast.Layers.Add(ExpandLayer(layer, itemScope));
                        break;
                    default:
                        _diagnostics.Error(item.Location, "内部エラー: ファイル直下に置けない要素です");
                        break;
                }
            });
        }

        /// <summary>
        /// 文リストを順に実行する。var / 代入 / ループはここで処理し、
        /// それ以外（実際に出力される要素）は sink に流す。
        /// </summary>
        void ExpandItems(List<IBlockItem> items, VarScope scope, Action<IBlockItem, VarScope> sink)
        {
            foreach (var item in items)
            {
                switch (item)
                {
                    case VarDeclAst declaration:
                        scope.Declare(
                            declaration.Name,
                            declaration.Value.Eval(scope, _diagnostics),
                            declaration.Location,
                            _diagnostics);
                        break;

                    case AssignAst assignment:
                        scope.Assign(
                            assignment.Name,
                            assignment.Value.Eval(scope, _diagnostics),
                            assignment.Location,
                            _diagnostics);
                        break;

                    case LoopAst loop:
                        ExpandLoop(loop, scope, sink);
                        break;

                    default:
                        sink(item, scope);
                        break;
                }
            }
        }

        // ================================================================
        // ループ
        // ================================================================

        void ExpandLoop(LoopAst loop, VarScope scope, Action<IBlockItem, VarScope> sink)
        {
            if (loop.Kind == LoopKind.For) ExpandFor(loop, scope, sink);
            else ExpandWhile(loop, scope, sink);
        }

        void ExpandFor(LoopAst loop, VarScope scope, Action<IBlockItem, VarScope> sink)
        {
            var from = loop.From.Eval(scope, _diagnostics);
            var to = loop.To.Eval(scope, _diagnostics);
            var step = loop.Step?.Eval(scope, _diagnostics) ?? 1;

            if (step == 0)
            {
                _diagnostics.Error(loop.Location, "for の step に 0 は指定できません");
                return;
            }

            // 回数を事前計算して上限チェック（両端含む）
            var count = step > 0
                ? Math.Floor((to - from) / step) + 1
                : Math.Floor((from - to) / -step) + 1;

            if (count <= 0)
            {
                _diagnostics.Warning(loop.Location,
                    $"for {loop.VarName} in {FormatValue(from)}..{FormatValue(to)} は1回も実行されません（範囲を確認してください）");
                return;
            }
            if (count > MaxLoopIterations)
            {
                _diagnostics.Error(loop.Location,
                    $"for の回数（{FormatValue(count)}回）が上限 {MaxLoopIterations} 回を超えています。範囲か step を見直してください");
                return;
            }

            for (var value = from;
                 step > 0 ? value <= to + Epsilon : value >= to - Epsilon;
                 value += step)
            {
                if (!CountExpansion(loop.Location)) return;

                var child = new VarScope(scope);
                child.Declare(loop.VarName, value, loop.Location, _diagnostics);
                ExpandItems(loop.Body, child, sink);
            }
        }

        void ExpandWhile(LoopAst loop, VarScope scope, Action<IBlockItem, VarScope> sink)
        {
            var guard = 0;
            while (EvalCondition(loop.Condition, scope))
            {
                guard++;
                if (guard > MaxLoopIterations)
                {
                    _diagnostics.Error(loop.Location,
                        $"while が {MaxLoopIterations} 回を超えました。無限ループの可能性があります（カウンタ変数の更新忘れではありませんか？）");
                    return;
                }
                if (!CountExpansion(loop.Location)) return;

                // 本体は毎回新しい子スコープ。外側の変数への代入はちゃんと届く
                ExpandItems(loop.Body, new VarScope(scope), sink);
            }
        }

        bool EvalCondition(WhileCondAst condition, VarScope scope)
        {
            var left = condition.Left.Eval(scope, _diagnostics);
            if (!condition.Op.HasValue) return left != 0; // 比較無し → 0以外で真

            var right = condition.Right.Eval(scope, _diagnostics);
            switch (condition.Op.Value)
            {
                case CmpOp.Equals:       return left == right;
                case CmpOp.NotEquals:    return left != right;
                case CmpOp.Less:         return left < right;
                case CmpOp.Greater:      return left > right;
                case CmpOp.LessEqual:    return left <= right;
                case CmpOp.GreaterEqual: return left >= right;
                default:                 return false;
            }
        }

        /// <summary>ループ展開の総数を数えて、暴走していればエラーを出して止める。</summary>
        bool CountExpansion(SourceLocation location)
        {
            _totalExpansions++;
            if (_totalExpansions <= MaxTotalExpansions) return true;

            if (!_limitReported)
            {
                _limitReported = true;
                _diagnostics.Error(location,
                    $"ループ展開の合計が {MaxTotalExpansions} 回を超えました。ループのネストが深すぎるかもしれません");
            }
            return false;
        }

        // ================================================================
        // 各ノードの複製（値を評価し、$変数を展開しながら新しいオブジェクトを作る）
        // ================================================================

        ParameterAst ExpandParameter(ParameterAst source, VarScope scope)
        {
            return new ParameterAst
            {
                Name = ExpandText(source.Name, scope, source.Location),
                Kind = source.Kind,
                DefaultValue = ExpandExpr(source.DefaultValue, scope),
                Location = source.Location,
            };
        }

        LayerAst ExpandLayer(LayerAst source, VarScope scope)
        {
            var layer = new LayerAst
            {
                Name = ExpandText(source.Name, scope, source.Location),
                IsDefault = source.IsDefault,
                WriteDefaults = source.WriteDefaults,
                Loop = source.Loop,
                Weight = ExpandExpr(source.Weight, scope),
                Location = source.Location,
            };

            ExpandItems(source.Items, new VarScope(scope), (item, itemScope) =>
            {
                switch (item)
                {
                    case StateAst state:
                        layer.States.Add(ExpandState(state, itemScope));
                        break;
                    case TransitionAst transition:
                        layer.Transitions.Add(ExpandTransition(transition, itemScope));
                        break;
                    default:
                        _diagnostics.Error(item.Location, "内部エラー: レイヤー内に置けない要素です");
                        break;
                }
            });

            return layer;
        }

        StateAst ExpandState(StateAst source, VarScope scope)
        {
            var state = new StateAst
            {
                Name = ExpandText(source.Name, scope, source.Location),
                HasClip = source.HasClip,
                ClipPath = source.ClipPath == null ? null : ExpandText(source.ClipPath, scope, source.Location),
                Speed = ExpandExpr(source.Speed, scope),
                SpeedParameter = source.SpeedParameter == null ? null : ExpandText(source.SpeedParameter, scope, source.Location),
                CycleOffset = ExpandExpr(source.CycleOffset, scope),
                CycleOffsetParameter = source.CycleOffsetParameter == null ? null : ExpandText(source.CycleOffsetParameter, scope, source.Location),
                TimeParameter = source.TimeParameter == null ? null : ExpandText(source.TimeParameter, scope, source.Location),
                WriteDefaults = source.WriteDefaults,
                Loop = source.Loop,
                Mirror = source.Mirror,
                MirrorParameter = source.MirrorParameter == null ? null : ExpandText(source.MirrorParameter, scope, source.Location),
                FootIK = source.FootIK,
                Location = source.Location,
            };

            foreach (var driver in source.Drivers)
                state.Drivers.Add(ExpandDriver(driver, scope));
            foreach (var control in source.PlayableLayerControls)
                state.PlayableLayerControls.Add(new PlayableLayerControlAst
                {
                    Layer = ExpandText(control.Layer, scope, control.Location),
                    GoalWeight = ExpandExpr(control.GoalWeight, scope),
                    BlendDuration = ExpandExpr(control.BlendDuration, scope),
                    Location = control.Location,
                });
            foreach (var control in source.AnimatorTrackingControls)
            {
                var expanded = new AnimatorTrackingControlAst { Location = control.Location };
                foreach (var setting in control.Settings)
                    expanded.Settings[setting.Key] = ExpandText(setting.Value, scope, control.Location);
                state.AnimatorTrackingControls.Add(expanded);
            }

            return state;
        }

        DriverBlockAst ExpandDriver(DriverBlockAst source, VarScope scope)
        {
            var block = new DriverBlockAst
            {
                LocalOnly = source.LocalOnly,
                Location = source.Location,
            };

            ExpandItems(source.Items, new VarScope(scope), (item, itemScope) =>
            {
                if (item is DriverActionAst action)
                    block.Actions.Add(ExpandDriverAction(action, itemScope));
                else
                    _diagnostics.Error(item.Location, "内部エラー: driver 内に置けない要素です");
            });

            return block;
        }

        DriverActionAst ExpandDriverAction(DriverActionAst source, VarScope scope)
        {
            return new DriverActionAst
            {
                Kind = source.Kind,
                Target = ExpandText(source.Target, scope, source.Location),
                Source = source.Source == null ? null : ExpandText(source.Source, scope, source.Location),
                Value = ExpandExpr(source.Value, scope),
                ValueMin = ExpandExpr(source.ValueMin, scope),
                ValueMax = ExpandExpr(source.ValueMax, scope),
                Location = source.Location,
            };
        }

        TransitionAst ExpandTransition(TransitionAst source, VarScope scope)
        {
            var transition = new TransitionAst
            {
                ExitTime = ExpandExpr(source.ExitTime, scope),
                Duration = ExpandExpr(source.Duration, scope),
                Offset = ExpandExpr(source.Offset, scope),
                AllowSelf = source.AllowSelf,
                Location = source.Location,
            };

            foreach (var node in source.Chain)
                transition.Chain.Add(ExpandText(node, scope, source.Location));

            transition.Condition = ExpandConditionExpr(source.Condition, scope);

            return transition;
        }

        /// <summary>条件式を複製しながら、パラメータ名と値の $変数 を展開する。</summary>
        ConditionExprAst ExpandConditionExpr(ConditionExprAst source, VarScope scope)
        {
            switch (source)
            {
                case null:
                    return null;

                case ConditionAst term:
                    return new ConditionAst
                    {
                        Param = ExpandText(term.Param, scope, term.Location),
                        Op = term.Op,
                        Value = ExpandExpr(term.Value, scope),
                        Location = term.Location,
                    };

                case ConditionBinaryAst binary:
                    return new ConditionBinaryAst
                    {
                        IsOr = binary.IsOr,
                        Left = ExpandConditionExpr(binary.Left, scope),
                        Right = ExpandConditionExpr(binary.Right, scope),
                        Location = binary.Location,
                    };

                default:
                    return null;
            }
        }

        // ================================================================
        // 式の評価とテキスト展開
        // ================================================================

        /// <summary>式を評価して定数に置き換える。null は null のまま。</summary>
        ExprAst ExpandExpr(ExprAst expression, VarScope scope)
            => expression == null
                ? null
                : new ConstExpr(expression.Eval(scope, _diagnostics), expression.Location);

        /// <summary>
        /// 文字列中の $変数 を展開する。`$$` は `$` そのもの。
        /// ステート名・クリップパス・パラメータ名などに使える。
        /// </summary>
        string ExpandText(string text, VarScope scope, SourceLocation location)
        {
            if (!text.Contains('$')) return text;

            var builder = new StringBuilder();
            var index = 0;
            while (index < text.Length)
            {
                var c = text[index];

                if (c != '$')
                {
                    builder.Append(c);
                    index++;
                    continue;
                }

                // $$ → $ そのもの
                if (index + 1 < text.Length && text[index + 1] == '$')
                {
                    builder.Append('$');
                    index += 2;
                    continue;
                }

                var start = index + 1;
                var end = start;
                while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
                    end++;

                if (end == start)
                {
                    _diagnostics.Error(location, "「$」の後には変数名が必要です。$ をそのまま書きたいときは「$$」と書いてください");
                    index++;
                    continue;
                }

                var name = text.Substring(start, end - start);
                if (scope.TryGet(name, out var value))
                {
                    builder.Append(FormatValue(value));
                }
                else
                {
                    _diagnostics.Error(location,
                        $"変数「{name}」は宣言されていません。先に「var {name} = 0」のように宣言してください");
                }
                index = end;
            }

            return builder.ToString();
        }

        /// <summary>値を文字列にする。整数なら小数点を付けない（Pose3 のように綺麗な名前にするため）。</summary>
        static string FormatValue(double value)
        {
            if (value == Math.Floor(value) && !double.IsInfinity(value))
                return ((long)value).ToString(CultureInfo.InvariantCulture);
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
