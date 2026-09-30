using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// 既存の AnimatorController を animscript テキストに変換するインポーター。
    ///
    /// 方針は「とにかく単純に」。1対1対応で書き出すだけで、
    /// or への再構成や for ループへの畳み込みは行わない。
    /// （同じ場所への遷移が複数あっても、そのまま複数行で書き出す。
    /// 　再度ビルドしても同じコントローラに戻る）
    ///
    /// 未対応の要素（BlendTree・サブステートマシン・Trigger 型パラメータ）は
    /// コメントや警告で明示しつつ、無難な形に置き換える。
    /// </summary>
    internal static class ControllerImporter
    {
        /// <summary>コントローラを animscript テキストに変換する。</summary>
        public static string Convert(AnimatorController controller, string scriptAssetPath, List<string> warnings)
        {
            var writer = new ScriptTextWriter();
            var scriptDirectory = AnimScriptPaths.GetDirectoryPath(scriptAssetPath);

            writer.Comment($"このファイルは AnimatorController「{controller.name}」から自動変換されました");
            writer.Comment("編集してからビルドすると、コントローラを再生成できます");
            writer.Line();

            // --- パラメータ ---
            foreach (var parameter in controller.parameters)
                WriteParameter(writer, parameter, warnings);
            if (controller.parameters.Length > 0)
                writer.Line();

            // --- レイヤー ---
            for (var i = 0; i < controller.layers.Length; i++)
            {
                WriteLayer(writer, controller.layers[i], isFirst: i == 0, scriptDirectory, warnings);
                if (i < controller.layers.Length - 1)
                    writer.Line();
            }

            return writer.ToString();
        }

        /// <summary>コントローラと同じフォルダに .animscript を保存する。保存先パスを返す。</summary>
        public static string ConvertAndSave(AnimatorController controller, List<string> warnings)
        {
            var controllerPath = AssetDatabase.GetAssetPath(controller);
            var directory = AnimScriptPaths.GetDirectoryPath(controllerPath);
            var scriptPath = $"{directory}/{controller.name}.animscript";

            var text = Convert(controller, scriptPath, warnings);

            // 日本語コメントを含むので BOM 付き UTF-8 で書く（エディタでの文字化け防止）
            File.WriteAllText(scriptPath, text, new UTF8Encoding(true));
            AssetDatabase.ImportAsset(scriptPath);
            return scriptPath;
        }

        // ================================================================
        // パラメータ
        // ================================================================

        static void WriteParameter(ScriptTextWriter writer, AnimatorControllerParameter parameter, List<string> warnings)
        {
            var name = NameSanitizer.Sanitize(parameter.name, warnings);

            switch (parameter.type)
            {
                case AnimatorControllerParameterType.Float:
                    writer.Line($"param {name} : float = {ScriptTextWriter.Format(parameter.defaultFloat)}");
                    break;
                case AnimatorControllerParameterType.Int:
                    writer.Line($"param {name} : int = {parameter.defaultInt}");
                    break;
                case AnimatorControllerParameterType.Bool:
                    writer.Line($"param {name} : bool = {(parameter.defaultBool ? "true" : "false")}");
                    break;
                default:
                    // Trigger は animscript 未対応。bool として書き出す
                    warnings.Add($"パラメータ「{parameter.name}」は Trigger 型のため、bool として書き出しました");
                    writer.Line($"param {name} : bool = {(parameter.defaultBool ? "true" : "false")}");
                    break;
            }
        }

        // ================================================================
        // レイヤー
        // ================================================================

        static void WriteLayer(ScriptTextWriter writer, AnimatorControllerLayer layer, bool isFirst,
            string scriptDirectory, List<string> warnings)
        {
            var stateMachine = layer.stateMachine;

            // 見やすさのため、画面上の位置順（上→下、左→右）に並べる
            var children = stateMachine.states
                .OrderBy(child => child.position.y)
                .ThenBy(child => child.position.x)
                .ToArray();

            // WriteDefaults がレイヤー内で揃っているか調べる
            var uniformWriteDefaults = children.Length > 0
                && children.All(child => child.state.writeDefaultValues == children[0].state.writeDefaultValues);

            // --- ヘッダ行 ---
            var header = new StringBuilder($"layer \"{layer.name}\"");
            if (isFirst) header.Append(" default");
            if (uniformWriteDefaults)
                header.Append(children[0].state.writeDefaultValues ? " wd on" : " wd off");
            if (layer.defaultWeight != 1f)
                header.Append($" weight {ScriptTextWriter.Format(layer.defaultWeight)}");
            header.Append(" {");
            writer.Line(header.ToString());
            writer.Indent();

            // --- ステート ---
            // 変換後の名前の対応表（遷移の参照で使う）
            var stateNames = new Dictionary<string, string>();
            var usedNames = new HashSet<string>();

            foreach (var child in children)
            {
                var state = child.state;
                var outName = NameSanitizer.SanitizeUnique(state.name, usedNames, warnings);
                stateNames[state.name] = outName;
                WriteState(writer, state, outName, uniformWriteDefaults, scriptDirectory, warnings);
            }

            if (children.Length > 0)
                writer.Line();

            // --- デフォルトステート ---
            if (stateMachine.defaultState != null)
            {
                writer.Line($"entry -> {stateNames[stateMachine.defaultState.name]}");
                writer.Line();
            }

            // --- 遷移（AnyState → 各ステートの順） ---
            foreach (var transition in stateMachine.anyStateTransitions)
                WriteTransition(writer, "any", transition, stateNames, warnings);

            foreach (var child in children)
            {
                foreach (var transition in child.state.transitions)
                    WriteTransition(writer, stateNames[child.state.name], transition, stateNames, warnings);
            }

            writer.Unindent();
            writer.Line("}");
        }

        // ================================================================
        // ステート
        // ================================================================

        static void WriteState(ScriptTextWriter writer, AnimatorState state, string outName,
            bool uniformWriteDefaults, string scriptDirectory, List<string> warnings)
        {
            var clipRef = GetClipReference(state, scriptDirectory, warnings);
            var driverLines = DriverExporter.Export(state, out var localOnly, warnings);

            if (driverLines == null)
            {
                // --- 1行書き ---
                var line = new StringBuilder($"state {outName}");
                if (clipRef != null) line.Append($" = \"{clipRef}\"");
                AppendInlineOptions(line, state, uniformWriteDefaults);
                writer.Line(line.ToString());
                return;
            }

            // --- ブロック書き（driver 付き） ---
            writer.Line($"state {outName} {{");
            writer.Indent();

            writer.Line(clipRef != null ? $"clip \"{clipRef}\"" : "clip none");
            if (state.speed != 1f)
                writer.Line($"speed {ScriptTextWriter.Format(state.speed)}");
            if (!uniformWriteDefaults)
                writer.Line(state.writeDefaultValues ? "wd on" : "wd off");

            writer.Line(localOnly ? "driver localOnly {" : "driver {");
            writer.Indent();
            foreach (var driverLine in driverLines)
                writer.Line(driverLine);
            writer.Unindent();
            writer.Line("}");

            writer.Unindent();
            writer.Line("}");
        }

        /// <summary>speed / wd のインラインオプションを付ける。</summary>
        static void AppendInlineOptions(StringBuilder line, AnimatorState state, bool uniformWriteDefaults)
        {
            if (state.speed != 1f)
                line.Append($" speed {ScriptTextWriter.Format(state.speed)}");
            if (!uniformWriteDefaults)
                line.Append(state.writeDefaultValues ? " wd on" : " wd off");
        }

        /// <summary>
        /// クリップ参照文字列を返す。
        /// スクリプトのフォルダ以下なら相対パス、それ以外は Assets/ からのパス。
        /// motion 無し・未対応のモーション（BlendTree 等）なら null。
        /// </summary>
        static string GetClipReference(AnimatorState state, string scriptDirectory, List<string> warnings)
        {
            var motion = state.motion;
            if (motion == null) return null;

            if (motion is AnimationClip clip)
            {
                var path = AssetDatabase.GetAssetPath(clip);
                if (string.IsNullOrEmpty(path))
                {
                    warnings.Add($"ステート「{state.name}」のクリップはアセットとして保存されていないため、スキップしました");
                    return null;
                }

                var prefix = scriptDirectory + "/";
                return path.StartsWith(prefix) ? path.Substring(prefix.Length) : path;
            }

            warnings.Add($"ステート「{state.name}」のモーション（{motion.GetType().Name}）は animscript 未対応のため、空ステートとして書き出しました");
            return null;
        }

        // ================================================================
        // 遷移
        // ================================================================

        static void WriteTransition(ScriptTextWriter writer, string fromName,
            AnimatorStateTransition transition, Dictionary<string, string> stateNames, List<string> warnings)
        {
            string toName;
            if (transition.isExit)
            {
                toName = "exit";
            }
            else if (transition.destinationState != null)
            {
                toName = stateNames[transition.destinationState.name];
            }
            else
            {
                warnings.Add($"「{fromName}」からステートマシンへの遷移は未対応のため、スキップしました");
                return;
            }

            var line = new StringBuilder($"{fromName} -> {toName}");

            // 条件（1つの遷移の条件は AND なので and でつなぐ。
            // 同じ場所への遷移が複数ある場合＝OR だが、そのまま複数行で書き出す）
            if (transition.conditions.Length > 0)
            {
                var conditions = transition.conditions.Select(FormatCondition);
                line.Append(" when ").Append(string.Join(" and ", conditions));
            }

            if (transition.hasExitTime)
                line.Append($" exitTime {ScriptTextWriter.Format(transition.exitTime)}");

            if (transition.hasFixedDuration)
            {
                // animscript では dur 省略時は Unity 標準（0.25秒）になるので、それ以外だけ書き出す
                if (transition.duration != 0.25f)
                    line.Append($" dur {ScriptTextWriter.Format(transition.duration)}");
            }
            else
            {
                warnings.Add($"「{fromName} -> {toName}」の遷移時間は「割合（%）」指定のため、秒指定には変換されませんでした");
            }

            if (fromName == "any" && transition.canTransitionToSelf)
                line.Append(" self");

            writer.Line(line.ToString());
        }

        static string FormatCondition(AnimatorCondition condition)
        {
            var param = NameSanitizer.SanitizeQuiet(condition.parameter);
            switch (condition.mode)
            {
                case AnimatorConditionMode.If:       return param;
                case AnimatorConditionMode.IfNot:    return "!" + param;
                case AnimatorConditionMode.Greater:  return $"{param} > {ScriptTextWriter.Format(condition.threshold)}";
                case AnimatorConditionMode.Less:     return $"{param} < {ScriptTextWriter.Format(condition.threshold)}";
                case AnimatorConditionMode.Equals:   return $"{param} == {ScriptTextWriter.Format(condition.threshold)}";
                case AnimatorConditionMode.NotEqual: return $"{param} != {ScriptTextWriter.Format(condition.threshold)}";
                default:                             return param;
            }
        }
    }
}
