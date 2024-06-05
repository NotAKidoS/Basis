using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

public static class LocalCloneHelper
{
    #region Avatar Setup
    
    public static void SetupAvatar(GameObject avatar)
    {
        Animator animator = avatar.GetComponent<Animator>();
        if (animator == null || animator.avatar == null || animator.avatar.isHuman == false)
        {
            //LocalCloneMod.Logger.Warning("Avatar is not humanoid!");
            Debug.LogWarning("Avatar is not humanoid!");
            return;
        }
        
        Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        if (headBone == null)
        {
            //LocalCloneMod.Logger.Warning("Head bone not found!");
            Debug.LogWarning("Head bone not found!");
            return;
        }
        
        var renderers = avatar.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            //LocalCloneMod.Logger.Warning("No renderers found!");
            Debug.LogWarning("No renderers found!");
            return;
        }
        
        //LocalCloneMod.Logger.Msg($"Found {renderers.Length} renderers. Processing...");
        Debug.Log($"Found {renderers.Length} renderers. Processing...");
        
        // create Local clones
        ProcessRenderers(renderers, avatar.transform, headBone);
    }
    
    private static void ProcessRenderers(IEnumerable<Renderer> renderers, Transform root, Transform headBone)
    {
        Stopwatch sw = Stopwatch.StartNew();
        
        IReadOnlyDictionary<Transform, FPRExclusion> exclusions = CollectTransformToExclusionMap(root, headBone);
        
        //todo: pass to unity job system to create final array
        
        // log current time
        //LocalCloneMod.Logger.Msg($"CollectTransformToExclusionMap in {sw.ElapsedMilliseconds}ms");
        Debug.Log($"CollectTransformToExclusionMap in {sw.ElapsedMilliseconds}ms");
        
        foreach (Renderer renderer in renderers)
        {
            ILocalClone clone = LocalCloneManager.CreateLocalClone(renderer, exclusions);
            if (clone != null) LocalCloneManager.Instance.AddLocalClone(clone);
        }
        
        sw.Stop();
        
        // log current time
        Debug.Log($"ProcessRenderers in {sw.ElapsedMilliseconds}ms");
        //LocalCloneMod.Logger.Msg($"ProcessRenderers in {sw.ElapsedMilliseconds}ms");
    }
    
    #endregion
    
    #region FPR Exclusion Processing
    
    private static Dictionary<Transform, FPRExclusion> CollectTransformToExclusionMap(Component root, Transform headBone)
    {
        // add an fpr exclusion to the head bone
        headBone.gameObject.AddComponent<FPRExclusion>().target = headBone;
        
        // get all FPRExclusions
        var fprExclusions = root.GetComponentsInChildren<FPRExclusion>(true).ToList();

        // get all valid exclusion targets, and destroy invalid exclusions
        Dictionary<Transform, FPRExclusion> exclusionTargets = new();
        for (int i = fprExclusions.Count - 1; i >= 0; i--)
        {
            FPRExclusion exclusion = fprExclusions[i];
            if (exclusion.target == null)
            {
                Object.Destroy(exclusion);
                continue;
            }
            
            // assign an id to the exclusion
            exclusion.id = i; // first to add wins
            
            // first to add wins
            exclusionTargets.TryAdd(exclusion.target, exclusion);
        }

        // process each FPRExclusion (recursive)
        foreach (FPRExclusion exclusion in fprExclusions)
            ProcessExclusion(exclusion, exclusion.target);
        
        // log totals
        //LocalCloneMod.Logger.Msg($"Exclusions: {fprExclusions.Count}");
        return exclusionTargets;

        void ProcessExclusion(FPRExclusion exclusion, Transform transform)
        {
            if (exclusionTargets.ContainsKey(transform)
                && exclusionTargets[transform] != exclusion) return; // found other exclusion root
            
            //exclusion.affectedChildren.Add(transform); // associate with the exclusion
            exclusionTargets.TryAdd(transform, exclusion); // add to the dictionary (yes its wasteful)
            
            foreach (Transform child in transform)
                ProcessExclusion(exclusion, child); // process children
        }
    }

    #endregion
}