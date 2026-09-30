using UnityEditor.Animations;
using UnityEngine;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// Animator ウィンドウ上でのステート配置。
    /// 全部 (0,0) に重なると見づらいので、定義順に2列で並べる。
    /// </summary>
    internal static class StateLayout
    {
        public static void Apply(AnimatorStateMachine stateMachine)
        {
            var children = stateMachine.states;
            for (var i = 0; i < children.Length; i++)
                children[i].position = PositionFor(i);
            stateMachine.states = children;
        }

        static Vector3 PositionFor(int index)
        {
            var column = index % 2;
            var row = index / 2;
            return new Vector3(300 + column * 280, 60 + row * 80, 0);
        }
    }
}
