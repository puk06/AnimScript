#nullable enable
using System.IO;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using net.puk06.AnimScript.Editor.Ndmf;
using UnityEditor;
using UnityEngine;

[assembly: ExportsPlugin(typeof(NdmfPlugin))]
namespace net.puk06.AnimScript.Editor.Ndmf
{
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
                    Debug.LogWarning($"[AnimScript] Pucoco 「{pucoco.name}」の ScriptFile が Assets 内に見つかりません", pucoco);
                    continue;
                }

                string source;
                try
                {
                    source = File.ReadAllText(scriptPath);
                }
                catch (System.Exception exception)
                {
                    Debug.LogError($"[AnimScript] 「{scriptPath}」の読み込みに失敗しました: {exception.Message}", pucoco);
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
                    continue;

                var animator = pucoco.gameObject.AddComponent<ModularAvatarMergeAnimator>();
                animator.layerType = pucoco.LayerType;
                animator.pathMode = MergeAnimatorPathMode.Absolute;
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
                Object.DestroyImmediate(component);
            }
        }
    }
}
