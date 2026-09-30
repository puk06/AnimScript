using UnityEngine;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// ParameterAst → AnimatorControllerParameter への変換。
    /// </summary>
    internal static class ParameterBuilder
    {
        public static AnimatorControllerParameter Build(ParameterAst ast)
        {
            var parameter = new AnimatorControllerParameter
            {
                name = ast.Name,
                type = ToType(ast.Kind),
            };

            var defaultValue = ast.DefaultValue?.Const() ?? 0;
            switch (ast.Kind)
            {
                case ParamKind.Float:
                    parameter.defaultFloat = (float)defaultValue;
                    break;
                case ParamKind.Int:
                    parameter.defaultInt = (int)defaultValue;
                    break;
                case ParamKind.Bool:
                    parameter.defaultBool = defaultValue != 0;
                    break;
            }

            return parameter;
        }

        static AnimatorControllerParameterType ToType(ParamKind kind)
        {
            switch (kind)
            {
                case ParamKind.Float: return AnimatorControllerParameterType.Float;
                case ParamKind.Int:   return AnimatorControllerParameterType.Int;
                case ParamKind.Bool:  return AnimatorControllerParameterType.Bool;
                default:              return AnimatorControllerParameterType.Float;
            }
        }
    }
}
