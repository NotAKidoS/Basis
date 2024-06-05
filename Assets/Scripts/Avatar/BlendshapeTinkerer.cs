using UnityEngine;

public class BlendshapeTinkerer : MonoBehaviour
{
    private SkinnedMeshRenderer skinnedMeshRenderer;
    private int[] blendShapes = new int[10];
    
    private void Start()
    {
        // find skinned mesh renderer
        skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        
        // find blendshapes
        for (int i = 0; i < 10; i++)
            blendShapes[i] = skinnedMeshRenderer.sharedMesh.blendShapeCount > i ? i : -1;
    }
    
    private void Update()
    {
        // tinker with blendshapes
        for (int i = 0; i < 10; i++)
        {
            if (blendShapes[i] == -1) continue;
            float value = Mathf.Sin(Time.time + i);
            skinnedMeshRenderer.SetBlendShapeWeight(blendShapes[i], value * 100);
        }
    }
}
