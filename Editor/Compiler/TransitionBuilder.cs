using System.Collections.Generic;
using System.Collections.Immutable;
using nadena.dev.ndmf.animator;
using UnityEditor.Animations;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// TransitionAst の1区間（チェーンの1つ分）→ VirtualStateTransition の生成。
    ///
    /// 条件式に or が含まれる場合は DNF（AND のかたまりの OR）に展開され、
    /// かたまり1つにつき1本の遷移が作られる。
    /// 例: A -> B when X or Y  →  A→B [X] と A→B [Y] の2本
    /// </summary>
    internal static class TransitionBuilder
    {
        /// <param name="from">区間の始点（"any" またはステート名）</param>
        /// <param name="to">区間の終点（"exit" またはステート名）</param>
        public static void Build(VirtualStateMachine stateMachine,
            IReadOnlyDictionary<string, VirtualState> states,
            TransitionAst ast, string from, string to)
        {
            var dnf = ConditionLogic.ToDnf(ast.Condition);

            for (var i = 0; i < dnf.Count; i++)
            {
                var transition = VirtualStateTransition.Create();

                if (from == "any")
                {
                    transition.SetDestination(states[to]);
                    transition.CanTransitionToSelf = ast.AllowSelf;
                    stateMachine.AnyStateTransitions = stateMachine.AnyStateTransitions.Add(transition);
                }
                else if (to == "exit")
                {
                    transition.SetExitDestination();
                    states[from].Transitions = states[from].Transitions.Add(transition);
                }
                else
                {
                    transition.SetDestination(states[to]);
                    states[from].Transitions = states[from].Transitions.Add(transition);
                }

                // or で分かれた遷移は、後から見分けられるよう番号を付ける
                transition.Name = dnf.Count > 1
                    ? $"{from} -> {to} [{i + 1}]"
                    : $"{from} -> {to}";

                ApplyOptions(transition, ast, dnf[i]);
            }
        }

        static void ApplyOptions(VirtualStateTransition transition, TransitionAst ast, List<ConditionAst> term)
        {
            // exitTime が無い遷移は「即座に条件判定」が animscript の既定
            transition.ExitTime = ast.ExitTime != null ? (float)ast.ExitTime.Const() : null;

            if (ast.Duration != null)
            {
                transition.HasFixedDuration = true;
                transition.Duration = (float)ast.Duration.Const();
            }

            if (ast.Offset != null)
                transition.Offset = (float)ast.Offset.Const();

            var conditions = ImmutableList.CreateBuilder<AnimatorCondition>();
            foreach (var condition in term)
            {
                // If / IfNot（bool 単体の条件）には比較する値が無いので 0 を渡す
                // （Unity 側でも If / IfNot の threshold は使われない）
                var threshold = condition.Value == null ? 0f : (float)condition.Value.Const();
                conditions.Add(new AnimatorCondition
                {
                    mode = ConditionBuilder.ToMode(condition.Op),
                    threshold = threshold,
                    parameter = condition.Param,
                });
            }
            transition.Conditions = conditions.ToImmutable();
        }
    }
}
