using UnityEngine;
using UnityEngine.Rendering;

public class MeshLocalClone : ILocalClone
{
    // main mesh
    private readonly MeshRenderer _mainMesh;
    private readonly ShadowCastingMode _originalShadowMode;
    
    public MeshLocalClone(MeshRenderer renderer)
    {
        _mainMesh = renderer;
        _originalShadowMode = _mainMesh.shadowCastingMode;
        // todo: only make a clone if using avatar overrender ui
    }
    
    #region ILocalClone Implementation
    
    public bool IsActive { get; } = true;
    public bool IsValid { get; } = true;
    
    public bool PreProcess()
    {
        bool shouldRender = _mainMesh.enabled && _mainMesh.gameObject.activeInHierarchy;
        return shouldRender;
    }

    public void RenderForPlayerCam()
    {
        //(_mainMesh.enabled, _isEnabled) = (false, _mainMesh.enabled);
        _mainMesh.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
    }

    public void RenderForUiCulling()
    {
        // not needed
    }

    public void ResetAfterAllRenders()
    {
        //(_mainMesh.enabled, _isEnabled) = (true, _isEnabled);
        _mainMesh.shadowCastingMode = _originalShadowMode;
    }
    
    public void Dispose()
    {
        // not needed
    }
    
    #endregion
}