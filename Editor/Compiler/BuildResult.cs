using System.Collections.Generic;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// ビルド1回分の結果。ウィンドウに表示するための情報をまとめて持つ。
    /// </summary>
    internal sealed class BuildResult
    {
        public bool Success { get; private set; }
        public string OutputPath { get; private set; }
        public IReadOnlyList<Diagnostic> Diagnostics { get; private set; }

        public int LayerCount { get; private set; }
        public int StateCount { get; private set; }
        public int TransitionCount { get; private set; }

        public static BuildResult Failure(DiagnosticBag diagnostics)
        {
            return new BuildResult
            {
                Success = false,
                Diagnostics = diagnostics.ToList(),
            };
        }

        public static BuildResult Succeeded(string outputPath, DiagnosticBag diagnostics, AnimScriptAst ast)
        {
            var stateCount = 0;
            var transitionCount = 0;
            foreach (var layer in ast.Layers)
            {
                stateCount += layer.States.Count;
                foreach (var transition in layer.Transitions)
                {
                    // チェーンの区間数（entry 始まりはデフォルト指定分を除く）× or 展開の本数
                    var startIndex = transition.Chain[0] == "entry" ? 1 : 0;
                    var segments = transition.Chain.Count - 1 - startIndex;
                    transitionCount += segments * ConditionLogic.CountTerms(transition.Condition);
                }
            }

            return new BuildResult
            {
                Success = true,
                OutputPath = outputPath,
                Diagnostics = diagnostics.ToList(),
                LayerCount = ast.Layers.Count,
                StateCount = stateCount,
                TransitionCount = transitionCount,
            };
        }
    }
}
