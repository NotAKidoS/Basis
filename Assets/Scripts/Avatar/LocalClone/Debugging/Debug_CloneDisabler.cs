using UnityEngine;

public class Debug_CloneDisabler : MonoBehaviour
{
    public bool disableClones;
    
    private bool OnWantsToRenderClone()
        => !disableClones;
    
    #region Unity Methods
    
    private void Start()
    {
        LocalCloneManager.wantsToRenderClone += OnWantsToRenderClone;
    }
    
    private void OnDestroy()
    {
        LocalCloneManager.wantsToRenderClone -= OnWantsToRenderClone;
    }
    
    #endregion
}