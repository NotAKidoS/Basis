using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

public class LocalCloneManager : MonoBehaviour
{
    #region Singleton Implementation

    private static LocalCloneManager _instance;
    public static LocalCloneManager Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = new GameObject("NAK.LocalCloneManager").AddComponent<LocalCloneManager>();
            DontDestroyOnLoad(_instance.gameObject);
            return _instance;
        }
    }

    #endregion
    
    public static ComputeShader shader;
    public static Material cullingMaterial;
    
    private const string LocalClonePostfix = "_LocalClone";
    
    // Settings
    private static bool s_UseCloneToCullUi;
    private static bool s_GreedilyCloneAllRenderers;
    
    // Game cameras
    [SerializeField] private Camera s_MainCamera;
    [SerializeField] private Camera s_UiCamera;
    
    // Local Clones
    private readonly List<ILocalClone> s_LocalClones = new();
    public void AddLocalClone(ILocalClone clone) => s_LocalClones.Add(clone);
    
    // Implementation
    private bool _resetAfterThisRender;
    
    // Debugging and Profiling
#if UNITY_EDITOR
    private static readonly ProfilerMarker s_OnPreRenderCallback = new(nameof(LocalCloneManager) + "." + nameof(OnPreRenderCallback));
    private static readonly ProfilerMarker s_OnPostRenderCallback = new(nameof(LocalCloneManager) + "." + nameof(OnPostRenderCallback));
#endif

    #region Unity Events

    private void Awake()
    {
        if (_instance != null
            && _instance != this)
        {
            Destroy(this);
            return;
        }
        
        shader = Resources.Load<ComputeShader>("ShadowClone");
        shader.hideFlags = HideFlags.HideAndDontSave;
        cullingMaterial = new Material(Resources.Load<Shader>("ShadowClone"));
        cullingMaterial.hideFlags = HideFlags.HideAndDontSave;
        
        if (s_MainCamera == null) s_MainCamera = Camera.main; // fallback to main cam 
        if (s_UiCamera == null && s_MainCamera != null) s_UiCamera = s_MainCamera.transform.Find("_UICamera")?.GetComponent<Camera>();
        
        // Camera.onPreRender += OnPreRenderCallback;
        // Camera.onPostRender += OnPostRenderCallback; // i am a very lazy person
        RenderPipelineManager.beginCameraRendering += OnPreRenderCallback;
        RenderPipelineManager.endCameraRendering += OnPostRenderCallback;
    }

    private void OnDestroy()
    {
        // Camera.onPreRender -= OnPreRenderCallback;
        // Camera.onPostRender -= OnPostRenderCallback;
        RenderPipelineManager.beginCameraRendering -= OnPreRenderCallback;
        RenderPipelineManager.endCameraRendering -= OnPostRenderCallback;
    }

    private void OnPreRenderCallback(ScriptableRenderContext _, Camera cam)
    {
#if UNITY_EDITOR
        if (!isActiveAndEnabled) return;
#endif
        
        bool isPlayerCam = cam == s_MainCamera;
        //bool forceUiCulling = s_UseCloneToCullUi && cam == s_UiCamera;
        
        if (!isPlayerCam)
            //&& !forceUiCulling)
            return; // only render for player & ui cams
        
        // check if external listener wants to prevent rendering
        // if (!CheckWantsToRenderClone()) 
        //     return;
        
#if UNITY_EDITOR
        s_OnPreRenderCallback.Begin();
#endif
        
        if (isPlayerCam && !s_UseCloneToCullUi)
            _resetAfterThisRender = true; // reset after player cam render
        //else if (forceUiCulling)
            //_resetAfterThisRender = true; // reset after ui cam render
        
        //Debug.Log("PreProcess");
            
        for (int i = s_LocalClones.Count - 1; i >= 0; i--)
        {
            ILocalClone clone = s_LocalClones[i];
            if (clone is not { IsValid: true })
            {
                clone?.Dispose();
                s_LocalClones.RemoveAt(i);
                continue; // invalid or dead
            }
        
            if (!clone.PreProcess()) continue; // not ready yet or disabled
            
            if (isPlayerCam)
                clone.RenderForPlayerCam(); // second-to-last cam to render
            else
                clone.RenderForUiCulling(); // last cam to render
        }
        
#if UNITY_EDITOR
        s_OnPreRenderCallback.End();
#endif
        
    }
    
    private void OnPostRenderCallback(ScriptableRenderContext _, Camera cam)
    {
#if UNITY_EDITOR
        if (!isActiveAndEnabled) return;
#endif
        
        if (!_resetAfterThisRender) 
            return;
        
#if UNITY_EDITOR
        s_OnPostRenderCallback.Begin();
#endif
        
        for (int i = s_LocalClones.Count - 1; i >= 0; i--)
        {
            ILocalClone clone = s_LocalClones[i];
            if (clone is not { IsValid: true })
            {
                clone?.Dispose();
                s_LocalClones.RemoveAt(i);
                continue; // invalid or dead
            }
            
            clone.ResetAfterAllRenders();
        }
        
        _resetAfterThisRender = false;
        
#if UNITY_EDITOR
        s_OnPostRenderCallback.End();
#endif
    }

    #endregion

    #region Delegate Methods

    /// <summary>
    /// Return false to prevent the local clone from being rendered.
    /// </summary>
    public static WantsToRenderCloneDelegate wantsToRenderClone;
    public delegate bool WantsToRenderCloneDelegate();
    
    private static bool CheckWantsToRenderClone()
    {
        if (wantsToRenderClone == null) return true;
        
        foreach (Delegate @delegate in wantsToRenderClone.GetInvocationList())
        {
            WantsToRenderCloneDelegate method = (WantsToRenderCloneDelegate)@delegate;
            if (!method()) return false;
        }
        
        return true;
    }


    #endregion
    
    #region Static Helpers
    
    internal static ILocalClone CreateLocalClone(Renderer renderer, IReadOnlyDictionary<Transform, FPRExclusion> exclusions)
    {
        switch (renderer)
        {
            case MeshRenderer meshRenderer:
                
                if (!s_GreedilyCloneAllRenderers // check if the mesh renderer is excluded (on head)
                    && !exclusions.ContainsKey(meshRenderer.transform)) 
                    break;
                
                MeshFilter _mainMeshFilter = meshRenderer.GetComponent<MeshFilter>();
                if (meshRenderer.sharedMaterials != null
                    && meshRenderer.sharedMaterials.Length != 0
                    && _mainMeshFilter != null
                    && _mainMeshFilter.sharedMesh != null
                    && _mainMeshFilter.sharedMesh.vertexCount != 0)
                {
                    ConfigureRenderer(meshRenderer); // generic optimizations 
                    return new MeshLocalClone(meshRenderer);
                }
                break;
            case SkinnedMeshRenderer skinnedMeshRenderer:

                if (!s_GreedilyCloneAllRenderers)
                {
                    for (int i = skinnedMeshRenderer.bones.Length - 1; i >= 0; i--)
                    {
                        // check if the bone is excluded
                        if (exclusions.ContainsKey(skinnedMeshRenderer.bones[i])) 
                            break;
                        
                        if (i != 0) continue; // if nothing is excluded, check transform itself
                        if (!exclusions.ContainsKey(skinnedMeshRenderer.transform))
                            return null; // no exclusions
                    }
                }
                
                if (skinnedMeshRenderer.sharedMesh != null
                    && skinnedMeshRenderer.sharedMaterials != null
                    && skinnedMeshRenderer.sharedMaterials.Length != 0)
                {
                    ConfigureRenderer(skinnedMeshRenderer); // generic optimizations
                    
                    var output = SkinnedLocalClone.FindExclusionVertList(skinnedMeshRenderer, exclusions);

                    return new SkinnedLocalClone(skinnedMeshRenderer, output);
                }
                break;
        }

        // invalid renderer or no mesh/materials
        return null;
    }
    
    internal static (MeshRenderer, Mesh) InstantiateLocalClone(SkinnedMeshRenderer meshRenderer)
    {
        GameObject localClone = new (meshRenderer.name + LocalClonePostfix) { layer = 9 };
        localClone.transform.SetParent(meshRenderer.transform, false);
        localClone.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        localClone.transform.localScale = Vector3.one;
        
        MeshRenderer newMesh = localClone.AddComponent<MeshRenderer>();
        MeshFilter newMeshFilter = localClone.AddComponent<MeshFilter>();

        ConfigureRenderer(newMesh);
        
        // nuke culling for local clones
        const float halfMax = float.MaxValue / 2; // leaving headroom just in case nan'd transform is within bounds
        newMesh.bounds = new Bounds(localClone.transform.position, new Vector3(halfMax,halfMax,halfMax));

        // copy mesh and materials
        Mesh sharedMesh = meshRenderer.sharedMesh;
        newMeshFilter.sharedMesh = Instantiate(sharedMesh); // clone mesh so on destroy we don't lose the original
        newMesh.sharedMaterials = meshRenderer.sharedMaterials;
        
        Mesh mesh = newMeshFilter.sharedMesh;
        
        // copy probe options
        newMesh.probeAnchor = meshRenderer.probeAnchor;
        newMesh.lightProbeUsage = meshRenderer.lightProbeUsage;
        newMesh.reflectionProbeUsage = meshRenderer.reflectionProbeUsage;
        newMesh.lightProbeProxyVolumeOverride = meshRenderer.lightProbeProxyVolumeOverride;
        
        // local clone should not cast shadows
        newMesh.shadowCastingMode = ShadowCastingMode.Off;
        
        return (newMesh, mesh);
    }

    private static void AdjustTangentsAsIfSkinned(Mesh mesh, Transform rootBone)
    {
        if (!mesh.isReadable) 
            return; // can't adjust tangents if mesh is not readable
        
        if (rootBone == null)
            return; // unity does not touch tangents if root bone is null ?
        
        // unity skins tangents, so when converting a skinned mesh to a non-skinned mesh,
        // we need to replicate the tangent transformation that unity does

        var tangents = mesh.tangents;
        var bindPoses = mesh.bindposes;
        var boneWeights = mesh.boneWeights;
        
        for (int i = 0; i < tangents.Length; i++)
        {
            Vector3 tangent = tangents[i];
            
            BoneWeight weight = boneWeights[i];
            Vector3 transformedTangent = Vector3.zero;
            
            // transform by bind pose
            transformedTangent += bindPoses[weight.boneIndex0].MultiplyVector(tangent) /* weight.weight0 */;
            transformedTangent += bindPoses[weight.boneIndex1].MultiplyVector(tangent) /* weight.weight1 */;
            transformedTangent += bindPoses[weight.boneIndex2].MultiplyVector(tangent) /* weight.weight2 */;
            transformedTangent += bindPoses[weight.boneIndex3].MultiplyVector(tangent) /* weight.weight3 */;
            
            // feel like there is a missing step here? joints turn out poorly and arms are fucked
            // including weight in calculation also super fucks it...

            transformedTangent.Normalize();
            tangents[i].x = transformedTangent.x;
            tangents[i].y = transformedTangent.y;
            tangents[i].z = transformedTangent.z;
            // leave w as-is
        }
        
        mesh.tangents = tangents;
    }
    
    internal static (MeshRenderer, MeshFilter) InstantiateLocalClone(MeshRenderer meshRenderer)
    {
        GameObject LocalClone = new (meshRenderer.name + LocalClonePostfix) { layer = 9 };
        LocalClone.transform.SetParent(meshRenderer.transform, false);
        LocalClone.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        LocalClone.transform.localScale = Vector3.one;
        
        MeshRenderer newMesh = LocalClone.AddComponent<MeshRenderer>();
        MeshFilter newMeshFilter = LocalClone.AddComponent<MeshFilter>();

        ConfigureRenderer(newMesh);
        
        // copy mesh and materials
        newMeshFilter.sharedMesh = meshRenderer.GetComponent<MeshFilter>().sharedMesh;
        newMesh.sharedMaterials = meshRenderer.sharedMaterials;
        
        // copy probe options
        newMesh.probeAnchor = meshRenderer.probeAnchor;
        newMesh.lightProbeUsage = meshRenderer.lightProbeUsage;
        newMesh.reflectionProbeUsage = meshRenderer.reflectionProbeUsage;
        newMesh.lightProbeProxyVolumeOverride = meshRenderer.lightProbeProxyVolumeOverride;
        
        // local clone should not cast shadows
        newMesh.shadowCastingMode = ShadowCastingMode.Off;

        return (newMesh, newMeshFilter);
    }


    private static void ConfigureRenderer(Renderer renderer)
    {
        // generic optimizations
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        
        // don't let visual/Local mesh cull in weird worlds
        renderer.allowOcclusionWhenDynamic = false;
        
        if (renderer is not SkinnedMeshRenderer skinnedMeshRenderer) 
            return;
        
        // skin mesh renderer optimizations
        skinnedMeshRenderer.skinnedMotionVectors = false;
        skinnedMeshRenderer.forceMatrixRecalculationPerRender = false; // expensive
        skinnedMeshRenderer.quality = SkinQuality.Bone4;
    }
    
    #endregion

    #region Custom Editor
#if UNITY_EDITOR
    
    [UnityEditor.CustomEditor(typeof(LocalCloneManager))]
    public class LocalCloneManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            LocalCloneManager manager = (LocalCloneManager)target;
            if (manager == null) return;

            GUILayout.Label("Assets / Read-Only", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUI.BeginDisabledGroup(true);
            UnityEditor.EditorGUILayout.ObjectField("Main Camera", manager.s_MainCamera, typeof(Camera), true);
            UnityEditor.EditorGUILayout.ObjectField("UI Camera", manager.s_UiCamera, typeof(Camera), true);
            UnityEditor.EditorGUILayout.ObjectField("Compute Shader", LocalCloneManager.shader, typeof(ComputeShader), true);
            UnityEditor.EditorGUILayout.ObjectField("Culling Material", LocalCloneManager.cullingMaterial, typeof(Material), true);
            UnityEditor.EditorGUI.EndDisabledGroup();
            GUILayout.Space(10);
            
            GUILayout.Label("Settings", UnityEditor.EditorStyles.boldLabel);
            s_UseCloneToCullUi = UnityEditor.EditorGUILayout.Toggle("Use Clone to Cull UI", s_UseCloneToCullUi);
            UnityEditor.EditorGUILayout.HelpBox("Experimental feature to use local clones to cull UI. Requires UI Camera to be set.", UnityEditor.MessageType.Info);
            s_GreedilyCloneAllRenderers = UnityEditor.EditorGUILayout.Toggle("Greedily Clone All Renderers", s_GreedilyCloneAllRenderers);
            UnityEditor.EditorGUILayout.HelpBox("Greedily clone all renderers for the purpose of UI culling. Requires UI Camera to be set and an avatar reload.", UnityEditor.MessageType.Info);
            GUILayout.Space(10);
            
            int cloneCount = manager.s_LocalClones.Count;
            if (cloneCount == 0)
            {
                GUILayout.Label("No Local Clones", UnityEditor.EditorStyles.boldLabel);
                return;
            }
            
            GUILayout.Label($"Local Clones ({cloneCount})", UnityEditor.EditorStyles.boldLabel);
            foreach (ILocalClone clone in manager.s_LocalClones)
            {
                GUILayout.BeginHorizontal("box");
                
                GUILayout.Label(clone.ToString());
                GUILayout.FlexibleSpace();
                
                GUILayout.Label(clone.IsValid ? "Valid" : "Invalid");
                GUILayout.FlexibleSpace();

                if (clone is SkinnedLocalClone skinnedLocalClone)
                {
                    // display flag field for skinnedLocalClone.HiddenVertexMask
                    skinnedLocalClone.HiddenVertexMask = UnityEditor.EditorGUILayout.MaskField(skinnedLocalClone.HiddenVertexMask, _hiddenVertexMaskNames);
                    skinnedLocalClone.HiddenVertexMask = UnityEditor.EditorGUILayout.IntField(skinnedLocalClone.HiddenVertexMask);
                }
                GUILayout.FlexibleSpace();
                
                GUILayout.EndHorizontal();
            }
        }

        private readonly string[] _hiddenVertexMaskNames =
        {
            "Body",
            "Head",
            "Additional 1",
            "Additional 2",
            "Additional 3",
            "Additional 4",
            "Additional 5",
            "Additional 6",
            "Additional 7",
            "Additional 8",
        }; 
    }
    
#endif
    #endregion
}
