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
    /// サブステートマシンは通常ステートへ展開し、境界をコメントで明示する。
    /// その他の未対応要素（Trigger 型パラメータ等）は警告しつつ、無難な形に置き換える。
    /// </summary>
    internal static class ControllerImporter
    {
        /// <summary>コントローラを animscript テキストに変換する。</summary>
        public static string Convert(AnimatorController controller, string scriptAssetPath, List<string> warnings)
        {
            var parameterNameMap = new Dictionary<string, string>();
            foreach (var parameter in controller.parameters)
                parameterNameMap[parameter.name] = NameSanitizer.EscapeParameterName(parameter.name);
            return ConvertInternal(controller, scriptAssetPath, AssetDatabase.GetAssetPath(controller),
                warnings, parameterNameMap, false);
        }

        static string ConvertInternal(AnimatorController controller, string scriptAssetPath,
            string controllerAssetPath, List<string> warnings,
            IReadOnlyDictionary<string, string> parameterNameMap,
            bool exportEmbeddedBlendTrees)
        {
            var writer = new ScriptTextWriter();
            var scriptDirectory = AnimScriptPaths.GetDirectoryPath(scriptAssetPath);

            writer.Comment($"このファイルは AnimatorController「{controller.name}」から自動変換されました");
            writer.Comment("編集してからビルドすると、コントローラを再生成できます");
            writer.Line();

            // --- パラメータ ---
            foreach (var parameter in controller.parameters)
            {
                WriteParameter(writer, parameter, parameterNameMap[parameter.name], warnings);
            }
            if (controller.parameters.Length > 0)
                writer.Line();

            // --- レイヤー ---
            for (var i = 0; i < controller.layers.Length; i++)
            {
                WriteLayer(writer, controller.layers[i], isFirst: i == 0, scriptDirectory,
                    scriptAssetPath, controllerAssetPath, exportEmbeddedBlendTrees,
                    parameterNameMap, warnings);
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

            var parameterNameMap = new Dictionary<string, string>();
            foreach (var parameter in controller.parameters)
                parameterNameMap[parameter.name] = NameSanitizer.EscapeParameterName(parameter.name);
            var text = ConvertInternal(controller, scriptPath, controllerPath, warnings, parameterNameMap, true);

            // 日本語コメントを含むので BOM 付き UTF-8 で書く（エディタでの文字化け防止）
            File.WriteAllText(scriptPath, text, new UTF8Encoding(true));
            AssetDatabase.ImportAsset(scriptPath);
            return scriptPath;
        }

        /// <summary>AnimatorController を .animscript に変換し、内容を返す。</summary>
        public static string Convert(AnimatorController controller, List<string> warnings)
        {
            var controllerPath = AssetDatabase.GetAssetPath(controller);
            var directory = AnimScriptPaths.GetDirectoryPath(controllerPath);
            var scriptPath = $"{directory}/{controller.name}.animscript";

            var parameterNameMap = new Dictionary<string, string>();
            foreach (var parameter in controller.parameters)
                parameterNameMap[parameter.name] = NameSanitizer.EscapeParameterName(parameter.name);

            return ConvertInternal(controller, scriptPath, controllerPath, warnings, parameterNameMap, false);
        }

        // ================================================================
        // パラメータ
        // ================================================================

        static void WriteParameter(ScriptTextWriter writer, AnimatorControllerParameter parameter,
            string name, List<string> warnings)
        {
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
            string scriptDirectory, string scriptAssetPath, string controllerAssetPath,
            bool exportEmbeddedBlendTrees,
            IReadOnlyDictionary<string, string> parameterNameMap,
            List<string> warnings)
        {
            var stateMachine = layer.stateMachine;
            var children = stateMachine.states.ToArray();
            var uniformWriteDefaults = children.Length > 0
                && children.All(child => child.state.writeDefaultValues == children[0].state.writeDefaultValues);

            // --- ヘッダ行 ---
            var header = new StringBuilder($"layer \"{layer.name}\"");
            if (isFirst) header.Append(" default");
            if (uniformWriteDefaults)
                header.Append(children[0].state.writeDefaultValues ? " wd on" : " wd off");
            header.Append(layer.blendingMode == AnimatorLayerBlendingMode.Additive ? " bl additive" : " bl override");
            if (layer.avatarMask != null)
            {
                var maskPath = AssetDatabase.GetAssetPath(layer.avatarMask);
                var maskPrefix = scriptDirectory + "/";
                if (maskPath.StartsWith(maskPrefix)) maskPath = maskPath.Substring(maskPrefix.Length);
                header.Append($" mask \"{maskPath}\"");
            }
            if (layer.defaultWeight != 1f)
                header.Append($" weight {ScriptTextWriter.Format(layer.defaultWeight)}");
            header.Append(" {");
            writer.Line(header.ToString());
            writer.Indent();

            // 先に全ステートへ名前を割り当て、サブステートを通常ステートへ展開する。
            var stateNames = new Dictionary<AnimatorState, string>();
            var usedNames = new HashSet<string>();
            CollectNames(stateMachine, stateNames, usedNames, warnings);
            WriteFlattenedStates(writer, stateMachine, stateMachine, stateNames,
                uniformWriteDefaults, scriptDirectory, scriptAssetPath, controllerAssetPath,
                exportEmbeddedBlendTrees,
                parameterNameMap, warnings);

            var entryState = FindEntryState(stateMachine);
            if (entryState != null)
            {
                writer.Line();
                writer.Line($"entry -> {stateNames[entryState]}");
            }

            WriteFlattenedTransitions(writer, stateMachine, stateMachine, stateNames, parameterNameMap, warnings);

            writer.Unindent();
            writer.Line("}");
        }

        static void CollectNames(AnimatorStateMachine stateMachine,
            Dictionary<AnimatorState, string> stateNames,
            HashSet<string> usedNames, List<string> warnings)
        {
            foreach (var child in stateMachine.states)
            {
                if (child.state != null)
                    stateNames[child.state] = NameSanitizer.SanitizeUnique(child.state.name, usedNames, warnings);
            }

            foreach (var child in stateMachine.stateMachines)
                if (child.stateMachine != null)
                    CollectNames(child.stateMachine, stateNames, usedNames, warnings);
        }

        static void WriteFlattenedStates(ScriptTextWriter writer, AnimatorStateMachine root,
            AnimatorStateMachine current, Dictionary<AnimatorState, string> stateNames,
            bool rootUniformWriteDefaults, string scriptDirectory,
            string scriptAssetPath, string controllerAssetPath, bool exportEmbeddedBlendTrees,
            IReadOnlyDictionary<string, string> parameterNameMap, List<string> warnings)
        {
            foreach (var child in current.states
                         .OrderBy(child => child.position.y)
                         .ThenBy(child => child.position.x))
            {
                var uniform = current == root ? rootUniformWriteDefaults : false;
                WriteState(writer, child.state, stateNames[child.state], uniform, scriptDirectory,
                    scriptAssetPath, controllerAssetPath, exportEmbeddedBlendTrees,
                    parameterNameMap, warnings);
            }

            foreach (var child in current.stateMachines
                         .OrderBy(child => child.position.y)
                         .ThenBy(child => child.position.x))
            {
                writer.Line();
                writer.Comment($"ここから先はサブステート「{child.stateMachine.name}」の内部です");
                WriteFlattenedStates(writer, root, child.stateMachine, stateNames,
                    rootUniformWriteDefaults, scriptDirectory, scriptAssetPath,
                    controllerAssetPath, exportEmbeddedBlendTrees, parameterNameMap, warnings);
                writer.Comment("サブステート終了です");
            }
        }

        static AnimatorState FindEntryState(AnimatorStateMachine stateMachine)
        {
            if (stateMachine.defaultState != null) return stateMachine.defaultState;

            foreach (var transition in stateMachine.entryTransitions)
            {
                if (transition.destinationState != null) return transition.destinationState;
                if (transition.destinationStateMachine != null)
                {
                    var nested = FindEntryState(transition.destinationStateMachine);
                    if (nested != null) return nested;
                }
            }

            return null;
        }

        static void WriteFlattenedTransitions(ScriptTextWriter writer,
            AnimatorStateMachine root, AnimatorStateMachine current,
            Dictionary<AnimatorState, string> stateNames,
            IReadOnlyDictionary<string, string> parameterNameMap, List<string> warnings)
        {
            foreach (var transition in current.anyStateTransitions)
                WriteFlattenedTransition(writer, "any", transition, stateNames, parameterNameMap, warnings);

            foreach (var child in current.states)
            {
                foreach (var transition in child.state.transitions)
                {
                    // サブステート内の Exit は、親のステートマシン遷移で展開する。
                    if (transition.isExit && current != root) continue;
                    WriteFlattenedTransition(writer, stateNames[child.state], transition, stateNames,
                        parameterNameMap, warnings);
                }
            }

            foreach (var child in current.stateMachines)
            {
                foreach (var transition in current.GetStateMachineTransitions(child.stateMachine))
                {
                    var exitStates = new List<AnimatorState>();
                    CollectExitStates(child.stateMachine, exitStates);
                    foreach (var exitState in exitStates)
                        WriteFlattenedMachineTransition(writer, stateNames[exitState], transition,
                            stateNames, parameterNameMap, warnings);
                }

                WriteFlattenedTransitions(writer, root, child.stateMachine, stateNames,
                    parameterNameMap, warnings);
            }
        }

        static void CollectExitStates(AnimatorStateMachine stateMachine, List<AnimatorState> result)
        {
            foreach (var child in stateMachine.states)
                if (child.state.transitions.Any(transition => transition.isExit))
                    result.Add(child.state);

            foreach (var child in stateMachine.stateMachines)
                if (stateMachine.GetStateMachineTransitions(child.stateMachine).Any(transition => transition.isExit))
                    CollectExitStates(child.stateMachine, result);
        }

        // ================================================================
        // ステート
        // ================================================================

        static void WriteState(ScriptTextWriter writer, AnimatorState state, string outName,
            bool uniformWriteDefaults, string scriptDirectory,
            string scriptAssetPath, string controllerAssetPath, bool exportEmbeddedBlendTrees,
            IReadOnlyDictionary<string, string> parameterNameMap, List<string> warnings)
        {
            var clipRef = GetClipReference(state, scriptDirectory, scriptAssetPath,
                controllerAssetPath, exportEmbeddedBlendTrees, warnings);
            var drivers = DriverExporter.ExportAll(state, parameterNameMap, warnings);
            var controlLines = ControlExporter.Export(state, warnings);

            if (drivers.Count == 0 && controlLines.Count == 0)
            {
                // --- 1行書き ---
                var line = new StringBuilder($"state {outName}");
                if (clipRef != null) line.Append($" = \"{clipRef}\"");
                AppendInlineOptions(line, state, uniformWriteDefaults, parameterNameMap);
                writer.Line(line.ToString());
                return;
            }

            // --- ブロック書き（state behaviour 付き） ---
            writer.Line($"state {outName} {{");
            writer.Indent();

            writer.Line(clipRef != null ? $"clip \"{clipRef}\"" : "clip none");
            if (state.speed != 1f)
                writer.Line($"speed {ScriptTextWriter.Format(state.speed)}");
            if (state.speedParameterActive)
                writer.Line($"speedParam {GetParameterName(state.speedParameter, parameterNameMap)}");
            if (state.cycleOffset != 0f)
                writer.Line($"cycleOffset {ScriptTextWriter.Format(state.cycleOffset)}");
            if (state.cycleOffsetParameterActive)
                writer.Line($"cycleOffsetParam {GetParameterName(state.cycleOffsetParameter, parameterNameMap)}");
            if (state.timeParameterActive)
                writer.Line($"time {GetParameterName(state.timeParameter, parameterNameMap)}");
            if (state.mirror)
                writer.Line("mirror on");
            if (state.mirrorParameterActive)
                writer.Line($"mirrorParam {GetParameterName(state.mirrorParameter, parameterNameMap)}");
            if (state.iKOnFeet)
                writer.Line("footIK on");
            if (!uniformWriteDefaults)
                writer.Line(state.writeDefaultValues ? "wd on" : "wd off");

            foreach (var driver in drivers)
            {
                writer.Line(driver.LocalOnly ? "driver localOnly {" : "driver {");
                writer.Indent();
                foreach (var driverLine in driver.Lines) writer.Line(driverLine);
                writer.Unindent();
                writer.Line("}");
            }
            foreach (var controlLine in controlLines)
            {
                if (controlLine == "}") writer.Unindent();
                writer.Line(controlLine);
                if (controlLine.EndsWith("{")) writer.Indent();
            }

            writer.Unindent();
            writer.Line("}");
        }

        /// <summary>state のインラインオプションを付ける。</summary>
        static void AppendInlineOptions(StringBuilder line, AnimatorState state, bool uniformWriteDefaults,
            IReadOnlyDictionary<string, string> parameterNameMap)
        {
            if (state.speedParameterActive)
                line.Append($" speedParam {GetParameterName(state.speedParameter, parameterNameMap)}");
            if (state.speed != 1f)
                line.Append($" speed {ScriptTextWriter.Format(state.speed)}");
            if (state.cycleOffsetParameterActive)
                line.Append($" cycleOffsetParam {GetParameterName(state.cycleOffsetParameter, parameterNameMap)}");
            if (state.cycleOffset != 0f)
                line.Append($" cycleOffset {ScriptTextWriter.Format(state.cycleOffset)}");
            if (state.timeParameterActive)
                line.Append($" time {GetParameterName(state.timeParameter, parameterNameMap)}");
            if (state.mirrorParameterActive)
                line.Append($" mirrorParam {GetParameterName(state.mirrorParameter, parameterNameMap)}");
            if (state.mirror)
                line.Append(" mirror on");
            if (state.iKOnFeet)
                line.Append(" footIK on");
            if (!uniformWriteDefaults)
                line.Append(state.writeDefaultValues ? " wd on" : " wd off");
        }

        /// <summary>
        /// モーション参照文字列を返す。
        /// スクリプトのフォルダ以下なら相対パス、それ以外は Assets/ からのパス。
        /// ConvertAndSave ではコントローラー内蔵の BlendTree をアセット化して参照する。
        /// </summary>
        static string GetClipReference(AnimatorState state, string scriptDirectory, string scriptAssetPath,
            string controllerAssetPath, bool exportEmbeddedBlendTrees, List<string> warnings)
        {
            var motion = state.motion;
            if (motion == null) return null;

            // Controller check only validates script structure. BlendTree は対象外にする。
            if (motion is BlendTree && !exportEmbeddedBlendTrees) return null;

            if (motion is AnimationClip || motion is BlendTree)
            {
                var path = AssetDatabase.GetAssetPath(motion);
                var isEmbeddedBlendTree = motion is BlendTree && path == controllerAssetPath;
                if (string.IsNullOrEmpty(path) || isEmbeddedBlendTree)
                {
                    if (motion is BlendTree embeddedBlendTree && exportEmbeddedBlendTrees)
                        return ExportEmbeddedBlendTree(embeddedBlendTree, state, scriptDirectory, scriptAssetPath, warnings);

                    warnings.Add($"ステート「{state.name}」のクリップはアセットとして保存されていないため、スキップしました");
                    return null;
                }

                var prefix = scriptDirectory + "/";
                return path.StartsWith(prefix) ? path.Substring(prefix.Length) : path;
            }

            warnings.Add($"ステート「{state.name}」のモーション（{motion.GetType().Name}）はアセットパスを取得できないため、空ステートとして書き出しました");
            return null;
        }

        static string ExportEmbeddedBlendTree(BlendTree source, AnimatorState state,
            string scriptDirectory, string scriptAssetPath, List<string> warnings)
        {
            var controllerName = Path.GetFileNameWithoutExtension(scriptAssetPath);
            controllerName = NameSanitizer.SanitizeQuiet(controllerName);
            var stateName = NameSanitizer.SanitizeQuiet(state.name);
            var assetDirectory = GetEmbeddedBlendTreeDirectory(scriptDirectory, controllerName);
            var assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{assetDirectory}/{controllerName}_BlendTree_{stateName}.asset");

            try
            {
                var nestedTrees = new List<BlendTree>();
                var root = CloneBlendTree(source, nestedTrees);
                AssetDatabase.CreateAsset(root, assetPath);
                foreach (var nested in nestedTrees)
                    AssetDatabase.AddObjectToAsset(nested, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(assetPath);

                var prefix = scriptDirectory + "/";
                return assetPath.StartsWith(prefix) ? assetPath.Substring(prefix.Length) : assetPath;
            }
            catch (System.Exception exception)
            {
                warnings.Add($"ステート「{state.name}」の内蔵 BlendTree を「{assetPath}」へ書き出せませんでした: {exception.Message}");
                return null;
            }
        }

        static string GetEmbeddedBlendTreeDirectory(string scriptDirectory, string controllerName)
        {
            var rootDirectory = scriptDirectory + "/AnimScript";
            if (!AssetDatabase.IsValidFolder(rootDirectory))
                AssetDatabase.CreateFolder(scriptDirectory, "AnimScript");

            var controllerDirectory = rootDirectory + "/" + controllerName;
            if (!AssetDatabase.IsValidFolder(controllerDirectory))
                AssetDatabase.CreateFolder(rootDirectory, controllerName);

            return controllerDirectory;
        }

        static BlendTree CloneBlendTree(BlendTree source, List<BlendTree> nestedTrees)
        {
            var clone = new BlendTree();
            clone.name = source.name;
            clone.blendType = source.blendType;
            clone.blendParameter = source.blendParameter;
            clone.blendParameterY = source.blendParameterY;
            clone.minThreshold = source.minThreshold;
            clone.maxThreshold = source.maxThreshold;
            clone.useAutomaticThresholds = source.useAutomaticThresholds;

            var children = source.children;
            for (var i = 0; i < children.Length; i++)
            {
                if (children[i].motion is BlendTree childTree)
                {
                    children[i].motion = CloneBlendTree(childTree, nestedTrees);
                    nestedTrees.Add((BlendTree)children[i].motion);
                }
            }
            clone.children = children;
            return clone;
        }

        // ================================================================
        // 遷移
        // ================================================================

        static void WriteFlattenedTransition(ScriptTextWriter writer, string fromName,
            AnimatorStateTransition transition, Dictionary<AnimatorState, string> stateNames,
            IReadOnlyDictionary<string, string> parameterNameMap, List<string> warnings)
        {
            string toName;
            if (transition.isExit)
            {
                toName = "exit";
            }
            else if (transition.destinationState != null
                     && stateNames.TryGetValue(transition.destinationState, out var stateName))
            {
                toName = stateName;
            }
            else if (transition.destinationStateMachine != null)
            {
                var entryState = FindEntryState(transition.destinationStateMachine);
                if (entryState == null || !stateNames.TryGetValue(entryState, out toName))
                {
                    warnings.Add($"「{fromName}」からのサブステート遷移先を解決できないため、スキップしました");
                    return;
                }
            }
            else
            {
                warnings.Add($"「{fromName}」からの遷移先を解決できないため、スキップしました");
                return;
            }

            var line = new StringBuilder($"{fromName} -> {toName}");
            if (transition.conditions.Length > 0)
                line.Append(" when ").Append(string.Join(" and ",
                    transition.conditions.Select(condition => FormatCondition(condition, parameterNameMap))));
            if (fromName == "any" && transition.canTransitionToSelf)
                line.Append(" self");
            AppendTransitionOptions(line, transition, warnings, fromName, toName);
            writer.Line(line.ToString());
        }

        static void WriteFlattenedMachineTransition(ScriptTextWriter writer, string fromName,
            AnimatorTransition transition, Dictionary<AnimatorState, string> stateNames,
            IReadOnlyDictionary<string, string> parameterNameMap, List<string> warnings)
        {
            string toName;
            if (transition.isExit)
            {
                toName = "exit";
            }
            else if (transition.destinationState != null
                     && stateNames.TryGetValue(transition.destinationState, out var stateName))
            {
                toName = stateName;
            }
            else if (transition.destinationStateMachine != null)
            {
                var entryState = FindEntryState(transition.destinationStateMachine);
                if (entryState == null || !stateNames.TryGetValue(entryState, out toName))
                {
                    warnings.Add($"「{fromName}」からのサブステート遷移先を解決できないため、スキップしました");
                    return;
                }
            }
            else
            {
                warnings.Add($"「{fromName}」からの遷移先を解決できないため、スキップしました");
                return;
            }

            var line = new StringBuilder($"{fromName} -> {toName}");
            if (transition.conditions.Length > 0)
                line.Append(" when ").Append(string.Join(" and ",
                    transition.conditions.Select(condition => FormatCondition(condition, parameterNameMap))));
            writer.Line(line.ToString());
        }

        static void AppendTransitionOptions(StringBuilder line, AnimatorStateTransition transition,
            List<string> warnings, string fromName, string toName)
        {
            if (transition.hasExitTime)
                line.Append($" exitTime {ScriptTextWriter.Format(transition.exitTime)}");

            if (transition.hasFixedDuration)
            {
                if (transition.duration != 0.25f)
                    line.Append($" dur {ScriptTextWriter.Format(transition.duration)}");
            }
            else
            {
                warnings.Add($"「{fromName} -> {toName}」の遷移時間は「割合（%）」指定のため、秒指定には変換されませんでした");
            }

            if (transition.offset != 0f)
                line.Append($" offset {ScriptTextWriter.Format(transition.offset)}");
        }

        static string GetParameterName(string name, IReadOnlyDictionary<string, string> parameterNameMap)
        {
            return parameterNameMap.TryGetValue(name, out var mappedName)
                ? mappedName
                : NameSanitizer.SanitizeQuiet(name);
        }

        static string FormatCondition(AnimatorCondition condition,
            IReadOnlyDictionary<string, string> parameterNameMap)
        {
            var param = GetParameterName(condition.parameter, parameterNameMap);
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
