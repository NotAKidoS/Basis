using System.Collections.Generic;
using System.Linq;
using Avatar.ShadowClone;
using UnityEngine;

public class LegacyHider : MonoBehaviour
{
    #region Processing

    public static void Setup(GameObject avatarRoot)
    {
        Animator animator = avatarRoot.GetComponent<Animator>();
        if (animator == null || animator.avatar == null || !animator.avatar.isHuman) return;

        Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        if (headBone == null) return;

        var renderers = avatarRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        var transformsToHideSet = headBone.GetComponentsInChildren<Transform>(true).ToHashSet();
        
        Transform headZeroCopy = new GameObject(headBone.name + "_zero").transform;
        headZeroCopy.SetParent(headBone.parent, false);
        headZeroCopy.localScale = Vector3.zero;

        foreach (Renderer renderer in renderers)
        {
            ShadowCloneUtils.ConfigureRenderer(renderer);
            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer && skinnedMeshRenderer.bones.Length > 0)
                ProcessSkinnedMeshRenderer(skinnedMeshRenderer, transformsToHideSet, headZeroCopy);
        }
    }


    private static void ProcessSkinnedMeshRenderer(SkinnedMeshRenderer skinnedMeshRenderer,
        ICollection<Transform> transformsToHideSet, Transform headZeroCopy)
    {
        var originalBones = skinnedMeshRenderer.bones;

        if (!originalBones.Any(transformsToHideSet.Contains))
            return; // newer unity just includes all bones needlessly idk ( https://github.com/NotAKidOnSteam/UnusedBoneRefCleaner )

        var zeroBones = originalBones.Select(it => transformsToHideSet.Contains(it) ? headZeroCopy : it).ToArray();

        LegacyHider hider = skinnedMeshRenderer.gameObject.AddComponent<LegacyHider>();
        hider.myShowBones = originalBones;
        hider.myHideBones = zeroBones;
        hider.mySkinnedMesh = skinnedMeshRenderer;
    }

    #endregion

    public bool forceMatrixRecalculationPerRender = true;

    private SkinnedMeshRenderer mySkinnedMesh;
    private Transform[] myShowBones;
    private Transform[] myHideBones;

    private void LateUpdate()
    {
        // debugging
        mySkinnedMesh.forceMatrixRecalculationPerRender = forceMatrixRecalculationPerRender;
    }

    private void OnEnable()
    {
        Camera.onPreRender += MyOnPreRender;
        Camera.onPostRender += MyOnPostRender;
    }

    private void OnDisable()
    {
        Camera.onPreRender -= MyOnPreRender;
        Camera.onPostRender -= MyOnPostRender;

        if (mySkinnedMesh != null) // reset
            mySkinnedMesh.bones = myShowBones;
    }

    private static bool IsMainCamera(Component current)
        => current.CompareTag("MainCamera");

    private void MyOnPreRender(Camera current)
    {
        if (!IsMainCamera(current))
            return;

        if (mySkinnedMesh == null) return;
        mySkinnedMesh.bones = myHideBones;
    }

    private void MyOnPostRender(Camera current)
    {
        if (!IsMainCamera(current))
            return;

        if (mySkinnedMesh == null) return;
        mySkinnedMesh.bones = myShowBones;
    }
}