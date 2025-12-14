using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#endif

namespace Normal.Realtime.Examples {
    /// <summary>
    /// Disables the Screen Space Ambient Occlusion (SSAO) Renderer Feature,
    /// (which is enabled by default in Unity's Universal 3D template) to make the scene look better.
    /// The change is applied temporarily while this component/scene is loaded.
    /// </summary>
    /// <remarks>
    /// Uses [ExecuteInEditMode] to run in edit mode (for the scene view) as well as in Play mode and builds.
    /// </remarks>
    [ExecuteInEditMode]
    public class DisableSSAO : MonoBehaviour {
#if UNITY_EDITOR
        private const string __shouldRestoreSSAOKey = "Normcore.ShouldRestoreSSAO";

        private static bool _shouldRestoreSSAO {
            get => EditorPrefs.GetBool(__shouldRestoreSSAOKey);
            set => EditorPrefs.SetBool(__shouldRestoreSSAOKey, value);
        }

        [InitializeOnLoadMethod]
        private static void Initialize() {
            // Avoids baking our changes into the build
            BuildEventListener.OnPreBuild  += RestoreSSAOIfNeeded;
            BuildEventListener.OnPostBuild += DisableSSAOIfNeeded;

            // Handle the edge case where Unity didn't exit gracefully (and _shouldRestoreSSAO persisted in EditorPrefs).
            RestoreSSAOIfNeeded();
        }
#else
        private static bool _shouldRestoreSSAO { get; set; }
#endif

        private void Awake() {
            DisableSSAOIfNeeded();
        }

        private void OnDestroy() {
            RestoreSSAOIfNeeded();
        }

        private static void DisableSSAOIfNeeded() {
            if (TryGetRenderFeature(out var feature, SSAOFilter) && feature.isActive) {
                Debug.Log("Normcore: Temporarily disabled the Screen Space Ambient Occlusion (SSAO) Renderer Feature");
                _shouldRestoreSSAO = true;
                feature.SetActive(false);
            }
        }

        private static void RestoreSSAOIfNeeded() {
            if (_shouldRestoreSSAO) {
                _shouldRestoreSSAO = false;

                if (TryGetRenderFeature(out var feature, SSAOFilter)) {
                    feature.SetActive(true);
                }
            }
        }

        private static bool TryGetRenderFeature(out ScriptableRendererFeature feature, Func<ScriptableRendererFeature, bool> filter) {
            if (UniversalRenderPipeline.asset == null) {
                feature = default;
                return false;
            }

            if (TryGetRendererDataList(UniversalRenderPipeline.asset, out ScriptableRendererData[] rendererDataList) == false) {
                feature = default;
                return false;
            }

            foreach (ScriptableRendererData rendererData in rendererDataList) {
                if (rendererData == null) {
                    continue;
                }

                foreach (ScriptableRendererFeature entry in rendererData.rendererFeatures) {
                    if (entry == null) {
                        continue;
                    }

                    if (filter.Invoke(entry)) {
                        feature = entry;
                        return true;
                    }
                }
            }

            feature = default;
            return false;
        }

        private static bool SSAOFilter(ScriptableRendererFeature feature) {
            // Query by string because older URP versions don't include SSAO
            const string query = "ScreenSpaceAmbientOcclusion";
            return feature.name.Contains(query) || feature.GetType().Name.Contains(query);
        }

#region Reflection

        private static FieldInfo _rendererDataListFieldInfo;

        private static bool TryGetRendererDataList(UniversalRenderPipelineAsset asset, out ScriptableRendererData[] list) {
            if (_rendererDataListFieldInfo == null) {
                // Get the private instance field m_RendererDataList
                // (RendererDataList is only public in URP 17.0+)
                _rendererDataListFieldInfo = typeof(UniversalRenderPipelineAsset)
                    .GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            if (_rendererDataListFieldInfo != null) {
                list = _rendererDataListFieldInfo.GetValue(asset) as ScriptableRendererData[];
                return true;
            }

            list = default;
            return false;
        }

#endregion

#if UNITY_EDITOR
        /// <summary>
        /// Exposes hooks for reverting our changes to avoid baking them into the build.
        /// </summary>
        private class BuildEventListener : IPreprocessBuildWithReport, IPostprocessBuildWithReport {
            public static event Action OnPreBuild;
            public static event Action OnPostBuild;

            public int callbackOrder => 0;

            public void OnPreprocessBuild(BuildReport report) {
                OnPreBuild?.Invoke();
            }

            public void OnPostprocessBuild(BuildReport report) {
                OnPostBuild?.Invoke();
            }
        }
#endif
    }
}
