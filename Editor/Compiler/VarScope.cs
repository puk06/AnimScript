using System.Collections.Generic;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// ビルド時変数のスコープ（入れ子可）。
    ///
    /// ・ブロック（レイヤー・ループの本体）ごとに子スコープが作られる
    /// ・var での宣言は今のスコープに、代入は一番近い宣言を書き換える
    /// ・外側のスコープの変数は内側から読める（トップレベルで宣言した変数をレイヤー内で使える）
    /// </summary>
    internal sealed class VarScope
    {
        readonly Dictionary<string, double> _values = new Dictionary<string, double>();
        readonly VarScope _parent;

        public VarScope(VarScope parent)
        {
            _parent = parent;
        }

        /// <summary>変数を読む。見つからなければ false。</summary>
        public bool TryGet(string name, out double value)
        {
            if (_values.TryGetValue(name, out value)) return true;
            if (_parent != null) return _parent.TryGet(name, out value);
            value = 0;
            return false;
        }

        /// <summary>var 宣言。同じブロックでの二重宣言はエラー。</summary>
        public void Declare(string name, double value, SourceLocation location, DiagnosticBag diagnostics)
        {
            if (_values.ContainsKey(name))
            {
                diagnostics.Error(location, $"変数「{name}」はこのブロックで既に宣言されています");
                return;
            }
            _values[name] = value;
        }

        /// <summary>代入。一番近いスコープの宣言を書き換える。未宣言ならエラー。</summary>
        public void Assign(string name, double value, SourceLocation location, DiagnosticBag diagnostics)
        {
            var scope = FindScope(name);
            if (scope == null)
            {
                diagnostics.Error(location, $"変数「{name}」は宣言されていません。先に「var {name} = 0」のように宣言してください");
                return;
            }
            scope._values[name] = value;
        }

        VarScope FindScope(string name)
        {
            if (_values.ContainsKey(name)) return this;
            return _parent?.FindScope(name);
        }
    }
}
