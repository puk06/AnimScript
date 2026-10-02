using System.Collections.Generic;
using UnityEditor.Animations;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// `layer "Locomotion" default wd on { ... }` のようなレイヤー定義。
    ///
    /// Items … Parser が書き込む「そのままの」並び（ループ等を含む）
    /// States / Transitions … AstExpander が展開後に書き込む平坦な結果。
    ///
    /// WriteDefaults / Loop が null の場合は「指定なし」で、
    /// ステート側の指定 → Unity標準の既定値、という順にフォールバックする。
    /// </summary>
    internal sealed class LayerAst : IBlockItem
    {
        public string Name { get; set; }

        /// <summary>default 指定。コントローラの先頭レイヤーに配置する。</summary>
        public bool IsDefault { get; set; }

        /// <summary>レイヤー内ステートの WriteDefaults の既定値（wd on/off）。</summary>
        public bool? WriteDefaults { get; set; }

        /// <summary>レイヤー内クリップの loopTime の既定値（loop on/off）。</summary>
        public bool? Loop { get; set; }

        /// <summary>レイヤーのブレンディングモード（bl additive / override）。</summary>
        public AnimatorLayerBlendingMode? BlendingMode { get; set; }

        /// <summary>レイヤーウェイト（weight）。null なら 1。</summary>
        public ExprAst Weight { get; set; }

        /// <summary>レイヤーに適用する AvatarMask のパス。</summary>
        public string AvatarMaskPath { get; set; }

        public List<IBlockItem> Items { get; } = new List<IBlockItem>();

        public List<StateAst> States { get; } = new List<StateAst>();
        public List<TransitionAst> Transitions { get; } = new List<TransitionAst>();

        public SourceLocation Location { get; set; }
    }
}
