# unity-motion-jpeg
## About
Create a Motion JPEG in AVI in Unity. A script to record the screen is included in the package.

## Requirement
Unity 2019.4 or newer.
It must also be a platform that supports AsyncGPUReadback.

## How to use
### AutoRecorder
This is a script that automatically records the screen. When deployed, it will record from the start, and when finished, it outputs the video file to Application.persistentDataPath.
### EndlessRecorder
Pressing the Record button (F11) will output the number of frames set from that point on to the Application.persistentDataPath.
## Build your own recording system.
```Example.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KA.UnityMotionJpeg;
using Random = UnityEngine.Random;

public class Example : MonoBehaviour
{
    void Start()
    {
        var rec = new Recorder();
        var filename = DateTime.Now.ToString("yyyy-MM-dd-T-HH-mm-ss") + ".avi";
        rec.BeginRecording(filename, 320, 240, 30);

        var tex2d = new Texture2D(320, 240);
        for (var i = 0; i < 90; i++)
        {
            for (var j = 0; j < 1000; j++)
            {
                var color = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f));
                var x = Random.Range(0, 320);
                var y = Random.Range(0, 240);
                tex2d.SetPixel(x, y, color);
            }
            tex2d.Apply();
            rec.RecordFrame(tex2d, 50);
        }
        rec.EndRecording();
    }
}
```