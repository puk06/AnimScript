using System.Collections.Generic;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// animscript 1ファイル分のASTルート。
    ///
    /// Items … Parser が書き込む「そのままの」並び（ループや変数宣言を含む）
    /// Parameters / Layers … AstExpander がループ展開後に書き込む平坦な結果。
    /// 　　　　　　　　　　　 Validator / 生成器はこちらだけを見ればよい。
    /// </summary>
    internal sealed class AnimScriptAst
    {
        public List<IBlockItem> Items { get; } = new List<IBlockItem>();

        public List<ParameterAst> Parameters { get; } = new List<ParameterAst>();
        public List<LayerAst> Layers { get; } = new List<LayerAst>();
    }
}
