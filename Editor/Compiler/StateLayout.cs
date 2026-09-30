using System.Collections.Immutable;
using nadena.dev.ndmf.animator;
using UnityEngine;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// Animator ウィンドウ上でのステート配置。
    /// 全部 (0,0) に重なると見づらいので、定義順に2列で並べる。
    /// </summary>
    internal static class StateLayout
    {
        public static void Apply(VirtualStateMachine stateMachine)
        {
            var children = stateMachine.States;
            var builder = ImmutableList.CreateBuilder<VirtualStateMachine.VirtualChildState>();

            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                child.Position = PositionFor(i);
                builder.Add(child);
            }

            stateMachine.States = builder.ToImmutable();
        }

        static Vector3 PositionFor(int index)
        {
            var column = index % 2;
            var row = index / 2;
            return new Vector3(300 + column * 280, 60 + row * 80, 0);
        }
    }
}
