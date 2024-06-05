using System;

public interface ILocalClone : IDisposable
{
    bool IsActive { get; }
    bool IsValid { get; }
    
    // OnPreRender
    bool PreProcess();
    void RenderForPlayerCam();
    void RenderForUiCulling();
    
    // OnPostRender
    void ResetAfterAllRenders();
}