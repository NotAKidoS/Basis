using UnityEngine;

public class Debug_InitLocalClone : MonoBehaviour
{
    private void Start() => LocalCloneHelper.SetupAvatar(gameObject);
}