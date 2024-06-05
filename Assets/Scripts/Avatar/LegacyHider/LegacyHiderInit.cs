using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LegacyHiderInit : MonoBehaviour
{
    void Start()
    {
        LegacyHider.Setup(gameObject);
    }
}