using System.Collections.Generic;

namespace net.puk06.AnimScript
{
    internal sealed class PlayableLayerControlAst : IBlockItem
    {
        public string Layer { get; set; }
        public ExprAst GoalWeight { get; set; }
        public ExprAst BlendDuration { get; set; }
        public SourceLocation Location { get; set; }
    }

    internal sealed class AnimatorTrackingControlAst : IBlockItem
    {
        public Dictionary<string, string> Settings { get; } = new Dictionary<string, string>();
        public SourceLocation Location { get; set; }
    }

    internal sealed class AnimatorLayerControlAst : IBlockItem
    {
        public string Playable { get; set; }
        public ExprAst Layer { get; set; }
        public ExprAst GoalWeight { get; set; }
        public ExprAst BlendDuration { get; set; }
        public SourceLocation Location { get; set; }
    }

    internal sealed class AnimatorLocomotionControlAst : IBlockItem
    {
        public bool DisableLocomotion { get; set; }
        public SourceLocation Location { get; set; }
    }

    internal sealed class AnimatorTemporaryPoseSpaceAst : IBlockItem
    {
        public bool EnterPoseSpace { get; set; }
        public bool FixedDelay { get; set; }
        public ExprAst Delay { get; set; }
        public SourceLocation Location { get; set; }
    }
}
