using System.Collections.Generic;
using UnityEditor.Animations;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// TransitionAst の1区間（チェーンの1つ分）→ AnimatorStateTransition の生成。
    /// </summary>
    internal static class TransitionBuilder
    {
        /// <param name="from">区間の始点（"any" またはステート名）</param>
        /// <param name="to">区間の終点（"exit" またはステート名）</param>
        public static void Build(AnimatorStateMachine stateMachine,
            IReadOnlyDictionary<string, AnimatorState> states,
            TransitionAst ast, string from, string to)
        {
            AnimatorStateTransition transition;

            if (from == "any")
            {
                transition = stateMachine.AddAnyStateTransition(states[to]);
                transition.canTransitionToSelf = ast.AllowSelf;
            }
            else if (to == "exit")
            {
                transition = states[from].AddExitTransition();
            }
            else
            {
                transition = states[from].AddTransition(states[to]);
            }

            transition.name = $"{from} -> {to}";
            ApplyOptions(transition, ast);
        }

        static void ApplyOptions(AnimatorStateTransition transition, TransitionAst ast)
        {
            // exitTime が無い遷移は「即座に条件判定」が animscript の既定
            transition.hasExitTime = ast.ExitTime != null;
            if (ast.ExitTime != null)
                transition.exitTime = (float)ast.ExitTime.Const();

            if (ast.Duration != null)
            {
                transition.hasFixedDuration = true;
                transition.duration = (float)ast.Duration.Const();
            }

            foreach (var condition in ast.Conditions)
            {
                transition.AddCondition(
                    ConditionBuilder.ToMode(condition.Op),
                    (float)condition.Value.Const(),
                    condition.Param);
            }
        }
    }
}
