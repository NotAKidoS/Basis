﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Experiment.NewHider
{
    public class NewHiderInit : MonoBehaviour
    {
        private void Start()
        {
            if (TransformHiderManager.shader == null)
                TransformHiderManager.shader = Resources.Load<ComputeShader>("BoneHider");
            
            SetupAvatar(gameObject);
        }
        
        public static void SetupAvatar(GameObject avatar)
        {
            Animator animator = avatar.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || animator.avatar.isHuman == false)
            {
                return;
            }
            
            Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            if (headBone == null)
            {
                return;
            }
            
            var renderers = avatar.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }
            
            Transform headZeroCopy = new GameObject(headBone.name + "_zerocopy").transform;
            headZeroCopy.SetParent(headBone.parent, false);
            headZeroCopy.localScale = Vector3.zero;
            
            TransformHiderManager.headZeroCopy = headZeroCopy;
            
            ProcessRenderers(renderers, avatar.transform, headBone);
        }
    
        private static void ProcessRenderers(IEnumerable<Renderer> renderers, Transform root, Transform headBone)
        {
            IReadOnlyDictionary<Transform, FPRExclusion2> exclusions = CollectTransformToExclusionMap(root, headBone);

            
            foreach (Renderer renderer in renderers)
            {
                ConfigureRenderer(renderer);
                
                ITransformHider hider = TransformHiderManager.CreateTransformHider(renderer, exclusions);
                if (hider != null) TransformHiderManager.Instance.AddTransformHider(hider);
                
                // add BlendshapeTinkerer
                if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
                    skinnedMeshRenderer.gameObject.AddComponent<BlendshapeTinkerer>();
            }
        }
        
        internal static void ConfigureRenderer(Renderer renderer, bool isShadowClone = false)
        {
            // generic optimizations
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        
            // don't let visual/shadow mesh cull in weird worlds
            renderer.allowOcclusionWhenDynamic = false; // (third person stripped local player naked when camera was slightly occluded)
        
            // shadow clone optimizations (always MeshRenderer)
            if (isShadowClone)
            {
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                return;
            }
        
            if (renderer is not SkinnedMeshRenderer skinnedMeshRenderer) 
                return;

            // GraphicsBuffer becomes stale randomly otherwise ???
            //skinnedMeshRenderer.updateWhenOffscreen = true;
        
            // skin mesh renderer optimizations
            skinnedMeshRenderer.skinnedMotionVectors = false;
            skinnedMeshRenderer.forceMatrixRecalculationPerRender = false; // expensive
            skinnedMeshRenderer.quality = SkinQuality.Bone4;
        }
        
    private static Dictionary<Transform, FPRExclusion2> CollectTransformToExclusionMap(Component root, Transform headBone)
    {
        // add an fpr exclusion to the head bone
        headBone.gameObject.AddComponent<FPRExclusion2>().target = headBone;
        
        // get all FPRExclusion2s
        var FPRExclusion2s = root.GetComponentsInChildren<FPRExclusion2>(true).ToList();

        // get all valid exclusion targets, and destroy invalid exclusions
        Dictionary<Transform, FPRExclusion2> exclusionTargets = new();
        for (int i = FPRExclusion2s.Count - 1; i >= 0; i--)
        {
            FPRExclusion2 exclusion = FPRExclusion2s[i];
            if (exclusion.target == null)
            {
                Object.Destroy(exclusion);
                continue;
            }
            
            // first to add wins
            exclusionTargets.TryAdd(exclusion.target, exclusion);
        }

        // process each FPRExclusion2 (recursive)
        foreach (FPRExclusion2 exclusion in FPRExclusion2s)
            ProcessExclusion(exclusion, exclusion.target);
        
        // log totals
        return exclusionTargets;

        void ProcessExclusion(FPRExclusion2 exclusion, Transform transform)
        {
            if (exclusionTargets.ContainsKey(transform)
                && exclusionTargets[transform] != exclusion) return; // found other exclusion root
            
            exclusion.affectedChildren.Add(transform); // associate with the exclusion
            exclusionTargets.TryAdd(transform, exclusion); // add to the dictionary (yes its wasteful)
            
            foreach (Transform child in transform)
                ProcessExclusion(exclusion, child); // process children
        }
    }
    }
}