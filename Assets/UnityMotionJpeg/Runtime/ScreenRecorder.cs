using System.Collections.Generic;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
using System.Threading.Tasks;
using System.Collections;

namespace KA.UnityMotionJpeg
{
    public class ScreenRecorder : MonoBehaviour
    {
        private Queue<AsyncGPUReadbackRequest> m_ReadbackRequests = new Queue<AsyncGPUReadbackRequest>();
        private Queue<RenderTexture> m_UsingRTs = new Queue<RenderTexture>();
        private Queue<RenderTexture> m_UnusedRTs = new Queue<RenderTexture>();
        private Queue<NativeArray<byte>> m_EndlessJpegs = new Queue<NativeArray<byte>>();
        private Queue<NativeArray<byte>> m_RawImages = new Queue<NativeArray<byte>>();

        private RenderTexture m_TemporaryScreenTexture;
        private CancellationTokenSource m_Cancellation;
        private Recorder m_Recorder = null;
        private int m_Width = -1;
        private int m_Height = -1;
        private int m_FrameRate = 30;
        private bool m_IsRecoding = false;
        private int m_Quality = 50;
        private int m_EndlessFrameCount = -1;
        private bool IsEndless { get { return m_EndlessFrameCount > 0; } }

        public bool BeginRecoding(string filePath, int width, int height, int frameRate, int quality = 50)
        {
            if (m_IsRecoding)
            {
                Debug.LogError("Currentry recoding.");
                return false;
            }

            if (!SystemInfo.supportsAsyncGPUReadback)
            {
                Debug.LogError("AsyncGPUReadback is not supported.");
                return false;
            }

            m_Width = width;
            m_Height = height;
            m_FrameRate = frameRate;
            m_Quality = quality;

            m_Recorder = new Recorder();
            m_Recorder.BeginRecording(filePath, width, height, frameRate);

            m_IsRecoding = true;
            JpegEncodeTask();
            StartCoroutine(Capture());

            return true;
        }

        public bool BeginEndlessRecoding(int width, int height, int frameRate, int quality = 50, int frameCount = 300)
        {
            if (m_IsRecoding)
            {
                Debug.LogError("Currentry recoding.");
                return false;
            }

            if (!SystemInfo.supportsAsyncGPUReadback)
            {
                Debug.LogError("AsyncGPUReadback is not supported.");
                return false;
            }

            m_Width = width;
            m_Height = height;
            m_FrameRate = frameRate;
            m_Quality = quality;
            m_EndlessFrameCount = frameCount;

            m_IsRecoding = true;
            JpegEncodeTask();
            StartCoroutine(Capture());

            return true;
        }

        private IEnumerator Capture()
        {
            var waitForEndOfFrame = new WaitForEndOfFrame();
            yield return waitForEndOfFrame;
            m_TemporaryScreenTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);

            while (m_IsRecoding)
            {
                RenderTexture renderTexture = null;
                if (m_UnusedRTs.Count > 0)
                {
                    renderTexture = m_UnusedRTs.Dequeue();
                }
                else
                {
                    renderTexture = new RenderTexture(m_Width, m_Height, 0, RenderTextureFormat.ARGB32);
                }
                m_UsingRTs.Enqueue(renderTexture);

                ScreenCapture.CaptureScreenshotIntoRenderTexture(m_TemporaryScreenTexture);
                Graphics.Blit(m_TemporaryScreenTexture, renderTexture);

                AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(renderTexture, 0, TextureFormat.RGBA32);
                m_ReadbackRequests.Enqueue(request);

                yield return waitForEndOfFrame;
            }
        }

        public void EndRecoding()
        {
            if (!m_IsRecoding)
            {
                Debug.LogError("Not currentry recoding.");
                return;
            }

            if (m_Recorder != null)
            {
                m_Recorder.EndRecording();
            }

            // Release.
            for (var i = 0; i < m_UsingRTs.Count; i++)
            {
                var renderTexture = m_UsingRTs.Dequeue();
                Destroy(renderTexture);
            }
            for (var i = 0; i < m_UnusedRTs.Count; i++)
            {
                var renderTexture = m_UnusedRTs.Dequeue();
                Destroy(renderTexture);
            }
            Destroy(m_TemporaryScreenTexture);

            m_IsRecoding = false;
            m_Cancellation.Cancel();
        }

        public void SaveEndlessEncodingFrames(string filePath)
        {
            if (!IsEndless)
            {
                return;
            }

            var recorder = new Recorder();
            recorder.BeginRecording(filePath, m_Width, m_Height, m_FrameRate);
            for (var i = 0; i < m_EndlessJpegs.Count; i++)
            {
                NativeArray<byte> jpeg = m_EndlessJpegs.Dequeue();
                recorder.RecordFrame(jpeg);
                m_EndlessJpegs.Enqueue(jpeg);
            }
            recorder.EndRecording();
        }

        private void JpegEncodeTask()
        {
            m_Cancellation = new CancellationTokenSource();
            var mainThreadContext = SynchronizationContext.Current;

            Task.Run(() =>
            {
                while (true)
                {
                    if (m_Cancellation.IsCancellationRequested)
                    {
                        return;
                    }

                    var hasRawImage = false;
                    NativeArray<byte> colors = new NativeArray<byte>();
                    lock (m_RawImages)
                    {
                        if (m_RawImages.Count > 0)
                        {
                            colors = m_RawImages.Dequeue();
                            hasRawImage = true;
                        }
                    }

                    if (hasRawImage)
                    {
                        NativeArray<byte> jpeg = ImageConversion.EncodeNativeArrayToJPG<byte>(colors, GraphicsFormat.R8G8B8A8_SRGB, (uint)m_Width, (uint)m_Height, 0, m_Quality);
                        if (IsEndless)
                        {
                            lock (m_EndlessJpegs)
                            {
                                m_EndlessJpegs.Enqueue(jpeg);
                                if (m_EndlessJpegs.Count > m_EndlessFrameCount)
                                {
                                    NativeArray<byte> unusedBytes = m_EndlessJpegs.Dequeue();
                                    unusedBytes.Dispose();
                                }
                            }
                        }
                        else
                        {
                            lock (m_Recorder)
                            {
                                mainThreadContext.Post(RecordFrame, jpeg);
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
            }, m_Cancellation.Token);
        }

        private void RecordFrame(object jpegObj)
        {
            NativeArray<byte> jpeg = (NativeArray<byte>)jpegObj;
            if (m_IsRecoding)
            {
                m_Recorder.RecordFrame(jpeg);
            }
            jpeg.Dispose();
        }

        private void Update()
        {
            if (!m_IsRecoding)
            {
                return;
            }

            while (m_ReadbackRequests.Count > 0)
            {
                var request = m_ReadbackRequests.Peek();
                if (request.hasError)
                {
                    RenderTexture temp = m_UsingRTs.Dequeue();
                    m_UnusedRTs.Enqueue(temp);

                    m_ReadbackRequests.Dequeue();

                    Debug.LogError("Readback Error.");
                    continue;
                }

                if (!request.done)
                {
                    break;
                }

                NativeArray<byte> colors = request.GetData<byte>();
                if (SystemInfo.graphicsUVStartsAtTop)
                {
                    var arrayBackup = new NativeArray<byte>(m_Width * 4, Allocator.Temp);
                    for (var i = 0; i < m_Height / 2; i++)
                    {
                        var upArray = colors.GetSubArray(m_Width * 4 * i, m_Width * 4);
                        var bottomArray = colors.GetSubArray(m_Width * 4 * (m_Height - i - 1), m_Width * 4);
                        arrayBackup.CopyFrom(bottomArray);
                        bottomArray.CopyFrom(upArray);
                        upArray.CopyFrom(arrayBackup);
                    }
                }

                lock (m_RawImages)
                {
                    m_RawImages.Enqueue(colors);
                }

                RenderTexture tempRT = m_UsingRTs.Dequeue();
                m_UnusedRTs.Enqueue(tempRT);

                m_ReadbackRequests.Dequeue();
            }
        }
    }
}