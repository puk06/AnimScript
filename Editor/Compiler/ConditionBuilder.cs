using UnityEditor.Animations;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// animscript の条件演算子 → Unity の AnimatorConditionMode への変換。
    /// </summary>
    internal static class ConditionBuilder
    {
        public static AnimatorConditionMode ToMode(CondOp op)
        {
            switch (op)
            {
                case CondOp.If:        return AnimatorConditionMode.If;
                case CondOp.IfNot:     return AnimatorConditionMode.IfNot;
                case CondOp.Greater:   return AnimatorConditionMode.Greater;
                case CondOp.Less:      return AnimatorConditionMode.Less;
                case CondOp.Equals:    return AnimatorConditionMode.Equals;
                case CondOp.NotEquals: return AnimatorConditionMode.NotEqual;
                default:               return AnimatorConditionMode.If;
            }
        }
    }
}
