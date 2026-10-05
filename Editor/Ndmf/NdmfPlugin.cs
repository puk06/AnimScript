#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.localization;
using net.puk06.AnimScript.Editor.Ndmf;
using UnityEditor;
using UnityEngine;

[assembly: ExportsPlugin(typeof(NdmfPlugin))]
namespace net.puk06.AnimScript.Editor.Ndmf
{
    internal static class NdmfLocalizer
    {
        private static Dictionary<string, Dictionary<string, string>> _localizations = new()
        {
            {
                "ja",
                new()
                {
                    { "Error.ScriptFileNotFound", "コンポーネント '{0}' でエラーが発生しました\n\nエラー内容: スクリプトファイルがAssets内に見つかりません。\nスクリプトファイル: {1}" },
                    { "Error.ScriptFileNotValid", "コンポーネント '{0}' でエラーが発生しました\n\nエラー内容: スクリプトファイルが .animscript ファイルではありません。\nスクリプトファイル: {1}" },
                    { "Error.ScriptFileReadFailed", "コンポーネント '{0}' でエラーが発生しました\n\nエラー内容: スクリプトファイルの読み込みに失敗しました\nスクリプトパス: {1}\n\n--- エラー ---\n{2}" },
                    { "Error.ScriptFileCompileFailed", "コンポーネント '{0}' でエラーが発生しました\n\nエラー内容: スクリプトファイルのコンパイルに失敗しました\nスクリプトパス: {1}\n\n--- エラー ---\n{2}" }
                }
            },
            {
                "en",
                new()
                {
                    { "Error.ScriptFileNotFound", "An error occurred in component {0}\nError: Script file not found in Assets.\nScript file: {1}" },
                    { "Error.ScriptFileNotValid", "An error occurred in component {0}\nError: Script file is not a .animscript file.\nScript file: {1}" },
                    { "Error.ScriptFileReadFailed", "An error occurred in component {0}\nFailed to read script file\nScript path: {1}\n\n--- Error ---\n{2}" },
                    { "Error.ScriptFileCompileFailed", "An error occurred in component {0}\nFailed to compile script file\nScript path: {1}\n\n--- Error ---\n{2}" }
                }
            }
        };
        
        internal static readonly Localizer Localizer = new("en", () =>
        {
            return new()
            {
                ("en", key => _localizations["en"].TryGetValue(key, out var value) ? value : key),
                ("ja", key => _localizations["ja"].TryGetValue(key, out var value) ? value : key)
            };
        });
    }

    internal class NdmfPlugin : Plugin<NdmfPlugin>
    {
        public override string QualifiedName => "net.puk06.animscript";
        public override string DisplayName => "AnimScript";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run(BuildAnimators.Instance).Then
                .Run(RemoveComponents.Instance);
        }
    }

    [DependsOnContext(typeof(VirtualControllerContext))]
    internal class BuildAnimators : Pass<BuildAnimators>
    {
        protected override void Execute(BuildContext context)
        {
            var avatar = context.AvatarRootObject;
            var components = avatar.GetComponentsInChildren<Pucoco>(false);
            if (components.Length == 0) return;

            var vctx = context.Extension<VirtualControllerContext>();
            var cloneContext = vctx.CloneContext;

            foreach (var pucoco in components)
            {
                if (pucoco.ScriptFile == null) continue;

                var scriptPath = AssetDatabase.GetAssetPath(pucoco.ScriptFile);
                if (string.IsNullOrEmpty(scriptPath))
                {
                    ErrorReport.ReportError(
                        NdmfLocalizer.Localizer,
                        ErrorSeverity.NonFatal,
                        "Error.ScriptFileNotFound",
                        pucoco,
                        pucoco.ScriptFile.name
                    );
                    continue;
                }

                if (!scriptPath.EndsWith(".animscript", StringComparison.OrdinalIgnoreCase))
                {
                    ErrorReport.ReportError(
                        NdmfLocalizer.Localizer,
                        ErrorSeverity.NonFatal,
                        "Error.ScriptFileNotAnimScript",
                        pucoco,
                        scriptPath
                    );
                    continue;
                }

                string source;
                try
                {
                    source = File.ReadAllText(scriptPath);
                }
                catch (Exception exception)
                {
                    ErrorReport.ReportError(
                        NdmfLocalizer.Localizer,
                        ErrorSeverity.NonFatal,
                        "Error.ScriptFileReadFailed",
                        pucoco,
                        scriptPath,
                        exception
                    );
                    continue;
                }

                var diagnostics = new DiagnosticBag();
                var controller = VirtualAnimatorController.Create(cloneContext, $"AnimScript_{pucoco.name}");

                AnimatorCompiler.BuildInto(source, scriptPath, controller, cloneContext, diagnostics);

                foreach (var diagnostic in diagnostics.ToList())
                {
                    var message = $"[AnimScript] {scriptPath}({diagnostic.Location.Line}): {diagnostic.Message}";
                    switch (diagnostic.Severity)
                    {
                        case DiagnosticSeverity.Error:
                            Debug.LogError(message, pucoco);
                            break;
                        case DiagnosticSeverity.Warning:
                            Debug.LogWarning(message, pucoco);
                            break;
                        default:
                            Debug.Log(message, pucoco);
                            break;
                    }
                }

                if (diagnostics.HasErrors)
                {
                    var fileName = Path.GetFileName(scriptPath);
                    ErrorReport.ReportError(
                        NdmfLocalizer.Localizer,
                        ErrorSeverity.NonFatal,
                        "Error.ScriptFileCompileFailed",
                        pucoco,
                        scriptPath,
                        string.Join('\n',
                            diagnostics
                                .ToList()
                                .Where(i => i.Severity == DiagnosticSeverity.Error)
                                .Select(i => $"{i.Location.Line} : {i.Message}")
                        )
                    );
                    continue;
                }

                var animator = pucoco.gameObject.AddComponent<ModularAvatarMergeAnimator>();
                animator.layerType = pucoco.LayerType;
                animator.pathMode = pucoco.pathMode;
                animator.relativePathRoot = pucoco.relativePathRoot;
                animator.layerPriority = pucoco.LayerPriority;
                animator.mergeAnimatorMode = pucoco.MergeAnimatorMode;
                animator.matchAvatarWriteDefaults = pucoco.MatchAvatarWriteDefaults;

                vctx.Controllers[animator] = controller;
            }
        }
    }

    public class RemoveComponents : Pass<RemoveComponents>
    {
        protected override void Execute(BuildContext buildContext)
        {
            var avatar = buildContext.AvatarRootObject;

            var components = avatar.GetComponentsInChildren<Pucoco>(true);
            DeleteAllComponents(components);
        }

        private void DeleteAllComponents(Pucoco[] components)
        {
            foreach (var component in components)
            {
                if (component == null) continue;
                UnityEngine.Object.DestroyImmediate(component);
            }
        }
    }
}
