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
}
