using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace KA.UnityMotionJpeg
{
    [RequireComponent(typeof(Camera))]
    public class EndlessRecorder : MonoBehaviour
    {
        [SerializeField]
        private int m_FrameRate = 30;
        [SerializeField]
        private int m_MaxWidthOrHeight = 160;
        [SerializeField]
        private int m_Quality = 50;
        [SerializeField]
        private KeyCode m_CaptureKey = KeyCode.F11;

        private ScreenRecorder m_ScreenRecorder = null;

        private void OnEnable()
        {
            m_ScreenRecorder = gameObject.AddComponent<ScreenRecorder>();

            var camera = gameObject.GetComponent<Camera>();
            var width = 0;
            var height = 0;
            if (camera.pixelWidth > camera.pixelHeight)
            {
                var ratio = (float)m_MaxWidthOrHeight / camera.pixelWidth;
                width = m_MaxWidthOrHeight;
                height = (int)(camera.pixelHeight * ratio);
            }
            else
            {
                var ratio = (float)m_MaxWidthOrHeight / camera.pixelHeight;
                width = (int)(camera.pixelWidth * ratio);
                height = m_MaxWidthOrHeight;
            }

            if (!m_ScreenRecorder.BeginEndlessRecoding(width, height, m_FrameRate, m_Quality))
            {
                enabled = false;
            }
        }

        private void Update()
        {
            if (Input.GetKey(m_CaptureKey))
            {
                var filename = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".avi";
                var path = Path.Combine(Application.persistentDataPath, filename);
                m_ScreenRecorder.SaveEndlessEncodingFrames(path);
            }
        }

        private void OnDisable()
        {
            m_ScreenRecorder.EndRecoding();
        }
    }
}