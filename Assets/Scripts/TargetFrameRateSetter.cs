using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetFrameRateSetter : MonoBehaviour
{
    [SerializeField]
    private int m_TargetFrameRate = 30;

    void Start()
    {
        Application.targetFrameRate = m_TargetFrameRate;
    }
}
