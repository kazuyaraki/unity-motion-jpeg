using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FrameCounter : MonoBehaviour
{
    private Text m_Text = null;

    void Start()
    {
        m_Text = GetComponent<Text>();
    }

    void Update()
    {
        m_Text.text = Time.frameCount.ToString("N0");
    }
}
