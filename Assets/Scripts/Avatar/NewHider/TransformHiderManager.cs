using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Built on top of Koneko's BoneHider but to mimic the ShadowCloneManager

public class TransformHiderManager : MonoBehaviour
{
    public static ComputeShader shader;
    
    #region Singleton Implementation

    private static TransformHiderManager _instance;
    public static TransformHiderManager Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = new GameObject("Koneko.TransformHiderManager").AddComponent<TransformHiderManager>();
            DontDestroyOnLoad(_instance.gameObject);
            return _instance;
        }
    }

    #endregion
    
    [Range(5, 120)]
    public float targetFrameRate = 90;
    
    // Game cameras
    private static Camera s_MainCamera;
    private static Camera s_UiCamera;
    
    // Settings
    internal static bool s_DebugHeadHide;
    internal static bool s_DisallowFprExclusions = true;
    
    // Implementation
    internal static Transform headZeroCopy;
    private bool _hasRenderedThisFrame;
    
    // Shadow Clones
    private readonly List<ITransformHider> s_TransformHider = new();
    public void AddTransformHider(ITransformHider clone)
        => s_TransformHider.Add(clone);
    
    // Debug
    private bool _debugHeadHiderProcessingTime;
    private Stopwatch _stopWatch = new();
    
    #region Unity Events

    private void Start()
    {
        if (Instance != null
            && Instance != this)
        {
            Destroy(this);
            return;
        }
        
        s_MainCamera = Camera.main;
    }

    private void OnEnable()
    {
        //Camera.onPreCull += MyOnPreRender;
        Camera.onPreRender += MyOnPreRender;
        Camera.onPostRender += MyOnPostRender;
    }

    private void OnDisable()
    {
        //Camera.onPreCull -= MyOnPreRender;
        Camera.onPreRender -= MyOnPreRender;
        Camera.onPostRender -= MyOnPostRender;
    }

    private void OnDestroy()
    {
        OnAvatarCleared();
    }

    #endregion
    
    #region Transform Hider Managment

    private void Update()
    {
        _hasRenderedThisFrame = false;
        
        if (Math.Abs(Application.targetFrameRate - targetFrameRate) > float.Epsilon)
            Application.targetFrameRate = (int) targetFrameRate;
    }
    
    private void MyOnPreRender(Camera cam)
    {
        if (_hasRenderedThisFrame) 
            return; // can only hide head once per frame
        
        if (cam != s_MainCamera // only hide in player cam, or if debug is on
            && !s_DebugHeadHide)
            return;
        
        _hasRenderedThisFrame = true;
        
        _stopWatch.Start();
        
        for (int i = s_TransformHider.Count - 1; i >= 0; i--)
        {
            ITransformHider hider = s_TransformHider[i];
            if (hider is not { IsValid: true })
            {
                hider?.Dispose();
                s_TransformHider.RemoveAt(i);
                continue; // invalid or dead
            }
        
            if (!hider.Process()) continue; // not ready yet or disabled

            hider.HideTransform(s_DisallowFprExclusions);
        }
        
        _stopWatch.Stop();
        if (_debugHeadHiderProcessingTime) Debug.Log($"TransformHiderManager.MyOnPreRender({s_DebugHeadHide}) took {_stopWatch.ElapsedMilliseconds}ms");
    }

    private void MyOnPostRender(Camera cam)
    {
        if (cam != s_UiCamera) return; // ui camera is expected to render last
        
        for (int i = s_TransformHider.Count - 1; i >= 0; i--)
        {
            ITransformHider hider = s_TransformHider[i];
            if (hider is not { IsValid: true })
            {
                hider?.Dispose();
                s_TransformHider.RemoveAt(i);
                continue; // invalid or dead
            }
        
            if (!hider.PostProcess()) continue; // does not need post processing

            hider.ShowTransform();
        }
    }

    #endregion

    #region Game Events

    public void OnAvatarCleared()
    {
        // Dispose all shadow clones BEFORE game unloads avatar
        // Otherwise we memory leak the shadow clones mesh & material instances!!!
        foreach (ITransformHider hider in s_TransformHider)
            hider.Dispose();
        s_TransformHider.Clear();
    }

    #endregion


    #region Static Helpers
    
    internal static bool IsLegacyFPRExcluded(Component renderer)
        => renderer.gameObject.name.Contains("[FPR]");
    
    internal static ITransformHider CreateTransformHider(Component renderer, IReadOnlyDictionary<Transform, FPRExclusion2> exclusions)
    {
        if (IsLegacyFPRExcluded(renderer))
            return null;
        
        return renderer switch
        {
            SkinnedMeshRenderer skinnedMeshRenderer => new SkinnedTransformHider(skinnedMeshRenderer, exclusions),
            MeshRenderer meshRenderer => new MeshTransformHider(meshRenderer, exclusions),
            _ => null
        };
    }

    #endregion
}