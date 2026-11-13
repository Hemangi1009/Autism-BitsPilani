using BitsPilani.Module1.Playground;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NatSuite.Recorders;
using NatSuite.Recorders.Clocks;
using NatSuite.Recorders.Inputs;

namespace BitsPilani.Module4 {
    // A mono camera renders normally through URP into a full-frame texture. This avoids
    // manually rendering the tracked stereo camera or recording both XR eyes side by side.
    public sealed class Module4CameraCapture : MonoBehaviour, IDisposable {
        Camera source, capture;
        RenderTexture target;
        ITextureInput textureInput;
        RealtimeClock clock;
        float frameInterval, nextFrame;
        public int FramesWritten { get; private set; }
        public Action FirstFrameCaptured;

        public void Initialize(Camera camera, IMediaRecorder recorder, RealtimeClock sessionClock, int width, int height, int frameRate) {
            source = camera; clock = sessionClock; frameInterval = 1f / Mathf.Max(1, frameRate);
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Module4 recording framebuffer" };
            target.Create();
            capture = gameObject.AddComponent<Camera>();
            var data = gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.allowXRRendering = false;
            var sourceData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (sourceData) { data.renderPostProcessing = sourceData.renderPostProcessing; data.antialiasing = sourceData.antialiasing; data.volumeLayerMask = sourceData.volumeLayerMask; data.volumeTrigger = camera.transform; }
            textureInput = SystemInfo.supportsAsyncGPUReadback ? (ITextureInput)new AsyncTextureInput(recorder) : new TextureInput(recorder);
            RenderPipelineManager.endCameraRendering += OnCameraRendered;
            Camera.onPostRender += OnBuiltInRendered;
            LateUpdate();
        }
        bool suspended;
        public void Suspend(){suspended=true;if(capture)capture.enabled=false;}
        public void BindSource(Camera camera) {
            source=camera;suspended=false;
            // The previous scene's volume trigger was destroyed. Do not retain it.
            var data=capture?capture.GetComponent<UniversalAdditionalCameraData>():null;
            if(data&&camera)data.volumeTrigger=camera.transform;
        }
        void LateUpdate() {
            if (!capture || !target) return;
            if (suspended || !source) { capture.enabled = false; return; }
            // Schedule the render itself, not just the encoder submission. Otherwise
            // VR renders this entire scene again at headset refresh rate, even though
            // most of those frames are immediately discarded by a 30-fps recording.
            if (Time.unscaledTime + .0001f < nextFrame) { capture.enabled = false; return; }
            capture.CopyFrom(source);
            capture.targetTexture = target; capture.rect = new Rect(0, 0, 1, 1);
            capture.aspect = (float)target.width / target.height;
            capture.enabled = true; capture.depth = source.depth - 1;
            transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        }
        void OnCameraRendered(ScriptableRenderContext context, Camera camera) { if (camera == capture) AppendFrame(); }
        void OnBuiltInRendered(Camera camera) { if (GraphicsSettings.currentRenderPipeline == null && camera == capture) AppendFrame(); }
        void AppendFrame() {
            if (textureInput == null) return;
            // Keep a stable cadence when display refresh is not a multiple of the
            // recording frame rate; never try to render a backlog after a slow frame.
            if (nextFrame <= 0) nextFrame = Time.unscaledTime;
            nextFrame += frameInterval;
            if (nextFrame < Time.unscaledTime) nextFrame = Time.unscaledTime + frameInterval;
            textureInput.CommitFrame(target, clock.timestamp);
            FramesWritten++;
            if (FramesWritten == 1) FirstFrameCaptured?.Invoke();
        }
        public void Dispose() {
            RenderPipelineManager.endCameraRendering -= OnCameraRendered; Camera.onPostRender -= OnBuiltInRendered;
            if (capture) capture.enabled = false;
            if (textureInput != null && SystemInfo.supportsAsyncGPUReadback) AsyncGPUReadback.WaitAllRequests();
            textureInput?.Dispose(); textureInput = null;
            if (target) { target.Release(); Destroy(target); target = null; }
        }
        void OnDestroy() { Dispose(); }
    }
}
