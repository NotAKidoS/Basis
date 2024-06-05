using UnityEngine;
using UnityEngine.Rendering;

namespace Avatar.ShadowClone
{
    public static class ShadowCloneUtils
    {
        // public static bool UseShadowToCullUi;
        // public static Material shadowMaterial;
        // public static ComputeShader shadowShader;
    
        private const int ShadowCloneLayer = 9;
        private const string ShadowClonePostfix = "_ShadowClone";
        
        #region SkinnedMeshRenderer Setup
        
        internal static void CreateShadowClones(SkinnedMeshRenderer[] skinnedMeshRenderers, out SkinnedMeshRenderer[] shadowRenderers)
		{
			shadowRenderers = new SkinnedMeshRenderer[skinnedMeshRenderers.Length];
			for (int i = 0; i < skinnedMeshRenderers.Length; i++)
				shadowRenderers[i] = InstantiateSkinnedShadowClone(skinnedMeshRenderers[i]);
		}
        
        private static SkinnedMeshRenderer InstantiateSkinnedShadowClone(SkinnedMeshRenderer sourceRenderer)
		{
			SkinnedMeshRenderer shadowRenderer = InitializeShadowCloneGameObject(sourceRenderer).AddComponent<SkinnedMeshRenderer>();
			shadowRenderer.sharedMesh = sourceRenderer.sharedMesh;
			CopyRendererProperties(sourceRenderer, shadowRenderer);
			ConfigureRenderer(shadowRenderer, true);
			
			// we love skinning :) 
			// sourceRenderer skinning for every camera + shadowRenderer skinning at least once
			// lovely performance
			
			shadowRenderer.rootBone = sourceRenderer.rootBone;
			shadowRenderer.bones = sourceRenderer.bones;
			shadowRenderer.localBounds = sourceRenderer.localBounds;
			
			return shadowRenderer;
		}

		private static GameObject InitializeShadowCloneGameObject(Renderer originalRenderer)
		{
			GameObject gameObject = new(originalRenderer.name + ShadowClonePostfix) { layer = ShadowCloneLayer };
			gameObject.transform.SetParent(originalRenderer.transform, false);
			gameObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			gameObject.transform.localScale = Vector3.one;
			return gameObject;
		}

		private static void CopyRendererProperties(Renderer source, Renderer destination)
		{
			destination.sharedMaterials = source.sharedMaterials; // dont ever touch .materials, you'll leak :)
			destination.probeAnchor = source.probeAnchor;
			destination.lightProbeUsage = source.lightProbeUsage;
			destination.reflectionProbeUsage = source.reflectionProbeUsage;
			destination.lightProbeProxyVolumeOverride = source.lightProbeProxyVolumeOverride;
			destination.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
		}

        #endregion
        
        #region Generic Optimizations

        public static void ConfigureRenderer(Renderer renderer, bool isShadowClone = false)
        {
	        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
	        renderer.allowOcclusionWhenDynamic = false; // fixes culling at weird angles in third person
	        
	        if (isShadowClone)
	        {
		        renderer.receiveShadows = false;
		        renderer.lightProbeUsage = LightProbeUsage.Off;
		        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
	        }
	        
	        SkinnedMeshRenderer skinnedMeshRenderer = renderer as SkinnedMeshRenderer;
	        if (skinnedMeshRenderer == null)
		        return;
	        
	        skinnedMeshRenderer.updateWhenOffscreen = false;
	        skinnedMeshRenderer.skinnedMotionVectors = false; // fixes head hiding technique causing motion blur artifacts
	        skinnedMeshRenderer.forceMatrixRecalculationPerRender = false; // fuck this setting (eats perf, https://github.com/NotAKidOnSteam/UnusedBoneRefCleaner/)
	        skinnedMeshRenderer.quality = SkinQuality.Bone4; // no one will notice
        }

        #endregion
        
    }
}