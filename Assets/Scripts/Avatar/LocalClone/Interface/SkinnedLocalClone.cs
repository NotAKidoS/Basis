using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public class SkinnedLocalClone : ILocalClone
{
    private static readonly int s_SourceBufferId = Shader.PropertyToID("_sourceBuffer");
    private static readonly int s_TargetBufferId = Shader.PropertyToID("_targetBuffer");
    private static readonly int s_HiddenVerticiesId = Shader.PropertyToID("_hiddenVertices");
    private static readonly int s_HiddenVerticiesMaskId = Shader.PropertyToID("_hiddenVerticesMask");
    private static readonly int s_HiddenVertexPos = Shader.PropertyToID("_hiddenVertexPos");
    private static readonly int s_SourceBufferLayoutId = Shader.PropertyToID("_sourceBufferLayout");
    private static readonly int s_SourceRootMatrix = Shader.PropertyToID("_rootBoneMatrix");
    
    // public properties

    /// <summary>
    /// 32-bit mask of hidden vertex groups.
    /// Each bit represents a group of vertices to hide defined by the FPRExclusion component.
    /// </summary>
    public int HiddenVertexMask { get; set; } = 1022; // random number, this dont work properly
    
    // main mesh
    private readonly SkinnedMeshRenderer _mainMesh;
    private readonly Transform _rootBone;
    
    // local mesh
    private readonly MeshRenderer _localMesh;
    private readonly Mesh _localMeshMesh;
    private readonly Transform _localMeshTransform;
    
    // skinning & blendshape copying
    private GraphicsBuffer _sourceBuffer;
    private GraphicsBuffer _targetBuffer;
    private ComputeBuffer _computeBuffer;
    private readonly int _threadGroups;
    private readonly int _bufferLayout;
    
    // material property copying
    private readonly MaterialPropertyBlock _localMaterialBlock;
    
    // material comparison & copy
    private readonly List<Material> _mainMaterialsComp;
    private readonly List<Material> _localMaterialsComp;
    private readonly Material[] _mainMaterials;
    
    // avatar overrender ui
    private readonly Material[] _cullingMaterials;
    
    internal SkinnedLocalClone(SkinnedMeshRenderer renderer, int[] hiddenVerts)
    {
        _mainMesh = renderer;
        
        (_localMesh, _localMeshMesh) = LocalCloneManager.InstantiateLocalClone(_mainMesh);
        _localMeshTransform = _localMesh.transform;
        
        _rootBone = _mainMesh.rootBone;
        _rootBone ??= _mainMesh.transform; // fallback to transform if no root bone
        
        // material property copying
        _localMaterialBlock = new MaterialPropertyBlock();
        
        // material comparison
        _mainMaterialsComp = new List<Material>();
        _localMaterialsComp = new List<Material>();
        
        // material copying
        int materialCount = _mainMesh.sharedMaterials.Length;
        _mainMaterials =  new Material[materialCount];
        
        // culling materials
        _cullingMaterials = new Material[materialCount];
        for (var index = 0; index < _cullingMaterials.Length; index++)
            _cullingMaterials[index] = LocalCloneManager.cullingMaterial;
        
        // buffer creation
        
        Mesh mesh = _mainMesh.sharedMesh;
        
        _bufferLayout = 0; // get buffer layout
        if (mesh.HasVertexAttribute(VertexAttribute.Position)) _bufferLayout += 3;
        if (mesh.HasVertexAttribute(VertexAttribute.Normal)) _bufferLayout += 3;
        if (mesh.HasVertexAttribute(VertexAttribute.Tangent)) _bufferLayout += 4;
        _bufferLayout *= 4; // 4 bytes per float
        
        const float xThreadGroups = 64f; // get thread groups
        _threadGroups = Mathf.CeilToInt(mesh.vertexCount / xThreadGroups);
        
        // set buffer targets
        _mainMesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
        _localMeshMesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
        
        // get clone mesh buffer
        _targetBuffer = _localMeshMesh.GetVertexBuffer(0);
        
        // create head-hiding buffer
        _computeBuffer = new ComputeBuffer(hiddenVerts.Length, sizeof(int));
        _computeBuffer.SetData(hiddenVerts);
    }

    #region ILocalClone Implementation
    
    public bool IsValid => _mainMesh != null && _localMesh != null && _rootBone != null;
    
    public bool IsActive { get; set; } = true;
    
    public bool PreProcess()
    {
        bool shouldRender = _mainMesh.enabled && _mainMesh.gameObject.activeInHierarchy;
        return shouldRender;
    }

    public void RenderForPlayerCam()
    {
        CopyMaterialsAndProperties();
        PrepareForPlayerCam();
        RenderLocalClone();
    }

    public void RenderForUiCulling()
    {
        PrepareForUiCulling();
        // already rendered in player cam
    }

    public void ResetAfterAllRenders()
    {
        ResetLocalClone();
    }
    
    public void Dispose()
    {
        if (_localMeshMesh != null) 
            Object.Destroy(_localMeshMesh);
        
        _sourceBuffer?.Dispose();
        _sourceBuffer = null;
        _targetBuffer?.Dispose();
        _targetBuffer = null;
        _computeBuffer?.Dispose();
        _computeBuffer = null;
    }
    
    #endregion

    #region Private Methods
    
    private void CopyMaterialsAndProperties()
    {
        // copy *shared* materials (accounts for material changes without leaking memory)
        //_localMesh.sharedMaterials = _mainMesh.sharedMaterials; // gc alloc
        
        _mainMesh.GetSharedMaterials(_mainMaterialsComp);
        _localMesh.GetSharedMaterials(_localMaterialsComp);

        // if the materials are different, copy them
        if (!_mainMaterialsComp.SequenceEqual(_localMaterialsComp))
        {
            _mainMaterialsComp.CopyTo(_mainMaterials); // prevent gc alloc
            _localMesh.sharedMaterials = _mainMaterials;
        }

        // copy material properties
        _mainMesh.GetPropertyBlock(_localMaterialBlock);
        _localMesh.SetPropertyBlock(_localMaterialBlock);
    }
    
    private void PrepareForPlayerCam()
    {
        // hide the main mesh
        _mainMesh.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        
        // un-nan the transform to render
#if UNITY_EDITOR // newer unity editor complains otherwise
        _localMeshTransform.localScale = Vector3.one;
#else
        _localMeshTransform.localPosition = Vector3.zero;
#endif
    }

    private void PrepareForUiCulling()
    {
        _localMesh.sharedMaterials = _cullingMaterials;
    }
    
    private void RenderLocalClone()
    {
        // thanks sdraw, i suck at matrix math
        Matrix4x4 rootMatrix = _mainMesh.localToWorldMatrix.inverse * Matrix4x4.TRS(_rootBone.position, _rootBone.rotation, Vector3.one);
        
        _sourceBuffer = _mainMesh.GetVertexBuffer(); // gc alloc
        //if (_sourceBuffer == null) return; // uh oh
        
        LocalCloneManager.shader.SetMatrix(s_SourceRootMatrix, rootMatrix);
        LocalCloneManager.shader.SetBuffer(0, s_SourceBufferId, _sourceBuffer); // this may be null on first frame if Game window is not open
        LocalCloneManager.shader.SetBuffer(0, s_TargetBufferId, _targetBuffer);
        
        LocalCloneManager.shader.SetBuffer(0, s_HiddenVerticiesId, _computeBuffer);
        LocalCloneManager.shader.SetInt(s_HiddenVerticiesMaskId, HiddenVertexMask); // temp value
        LocalCloneManager.shader.SetVector(s_HiddenVertexPos, Vector4.positiveInfinity); // temp value
        
        LocalCloneManager.shader.SetInt(s_SourceBufferLayoutId, _bufferLayout);
        LocalCloneManager.shader.Dispatch(0, _threadGroups, 1, 1);
        
        _sourceBuffer.Release();
    }
    
    private void ResetLocalClone()
    {
        // reassign the shared materials after culling
        _localMesh.sharedMaterials = _mainMaterials;
        
        // un-hide the main mesh
        _mainMesh.shadowCastingMode = ShadowCastingMode.On;
        
        // nan the transform to prevent rendering
        // there is no other good way to do this...
        
#if UNITY_EDITOR // newer unity editor complains otherwise
        _localMeshTransform.localScale = Vector3.zero; 
#else
        _localMeshTransform.position = Vector3.positiveInfinity;
#endif
    }

    #endregion

    #region Head Hiding Methods
    
    public static int[] FindExclusionVertList(SkinnedMeshRenderer renderer, IReadOnlyDictionary<Transform, FPRExclusion> exclusions) 
    {
        Mesh sharedMesh = renderer.sharedMesh;
        var boneWeights = sharedMesh.boneWeights;
        int[] vertexIndices = new int[sharedMesh.vertexCount];
    
        // Pre-map bone transforms to their exclusion ids if applicable
        Dictionary<int, int> boneIndexToExclusionId = new();
        for (int i = 0; i < renderer.bones.Length; i++)
            if (exclusions.TryGetValue(renderer.bones[i], out FPRExclusion exclusion))
                boneIndexToExclusionId[i] = 1 << exclusion.id; // Store pre-shifted exclusion ID for efficiency

        const float minWeightThreshold = 0.2f;
        for (int i = 0; i < boneWeights.Length; i++) 
        {
            BoneWeight weight = boneWeights[i];

            // Check each bone index against the map, break early if a matching bone is found
            if (weight.weight0 > minWeightThreshold && boneIndexToExclusionId.TryGetValue(weight.boneIndex0, out var exclusionMask) ||
                weight.weight1 > minWeightThreshold && boneIndexToExclusionId.TryGetValue(weight.boneIndex1, out exclusionMask) ||
                weight.weight2 > minWeightThreshold && boneIndexToExclusionId.TryGetValue(weight.boneIndex2, out exclusionMask) ||
                weight.weight3 > minWeightThreshold && boneIndexToExclusionId.TryGetValue(weight.boneIndex3, out exclusionMask))
            {
                vertexIndices[i] = exclusionMask;
            }
        }
    
        return vertexIndices;
    }

    
    #endregion
}