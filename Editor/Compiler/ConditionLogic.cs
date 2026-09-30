using System.Collections.Generic;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// 条件式の論理演算ユーティリティ。
    ///
    /// Unity の遷移条件は AND のリストしか持てない（OR が無い）ため、
    /// or を含む条件式は「AND のかたまりの OR」（DNF: 選言標準形）に変形し、
    /// かたまり1つにつき1本の遷移を生成する、という方式を取る。
    ///
    /// 例: (A or B) and (C or D)
    ///   → [A and C] / [A and D] / [B and C] / [B and D] の4本の遷移
    /// </summary>
    internal static class ConditionLogic
    {
        /// <summary>1行の遷移から生成される遷移の最大本数（暴走防止）。</summary>
        public const int MaxDnfTerms = 16;

        /// <summary>
        /// DNF に展開したときの項の数（＝生成される遷移の本数）を数える。
        /// or は和、and は積になる。
        /// </summary>
        public static int CountTerms(ConditionExprAst condition)
        {
            switch (condition)
            {
                case null:
                    return 1;
                case ConditionAst _:
                    return 1;
                case ConditionBinaryAst binary:
                    var left = CountTerms(binary.Left);
                    var right = CountTerms(binary.Right);
                    return binary.IsOr ? left + right : left * right;
                default:
                    return 1;
            }
        }

        /// <summary>
        /// 条件式を DNF（AND のかたまりのリスト）に展開する。
        /// 無条件（null）のときは「無条件の遷移1本分」として空のかたまり1つを返す。
        /// </summary>
        public static List<List<ConditionAst>> ToDnf(ConditionExprAst condition)
        {
            switch (condition)
            {
                case null:
                    return new List<List<ConditionAst>> { new List<ConditionAst>() };

                case ConditionAst term:
                    return new List<List<ConditionAst>> { new List<ConditionAst> { term } };

                case ConditionBinaryAst binary:
                    var leftDnf = ToDnf(binary.Left);
                    var rightDnf = ToDnf(binary.Right);

                    if (binary.IsOr)
                    {
                        // OR: 両方のかたまりをそのまま連結
                        leftDnf.AddRange(rightDnf);
                        return leftDnf;
                    }

                    // AND: 直積（左の各かたまり × 右の各かたまり）
                    var product = new List<List<ConditionAst>>(leftDnf.Count * rightDnf.Count);
                    foreach (var leftTerm in leftDnf)
                    foreach (var rightTerm in rightDnf)
                    {
                        var merged = new List<ConditionAst>(leftTerm);
                        merged.AddRange(rightTerm);
                        product.Add(merged);
                    }
                    return product;

                default:
                    return new List<List<ConditionAst>> { new List<ConditionAst>() };
            }
        }

        /// <summary>式に含まれる全ての項を列挙する（パラメータの存在チェック用）。</summary>
        public static IEnumerable<ConditionAst> EnumerateTerms(ConditionExprAst condition)
        {
            switch (condition)
            {
                case null:
                    yield break;
                case ConditionAst term:
                    yield return term;
                    break;
                case ConditionBinaryAst binary:
                    foreach (var term in EnumerateTerms(binary.Left)) yield return term;
                    foreach (var term in EnumerateTerms(binary.Right)) yield return term;
                    break;
            }
        }
    }
}
