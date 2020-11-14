#if !LIV_UNIVERSAL_RENDER
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

namespace LIV.SDK.Unity
{
    public partial class SDKRender : System.IDisposable
    {        
        // Renders the clip plane in the foreground texture
        private CommandBuffer _clipPlaneCommandBuffer = null;
        // Renders the clipped opaque content in to the foreground texture alpha
        private CommandBuffer _addAlphaCommandBuffer = null;
        // Captures background texture before post-effects
        private CommandBuffer _captureBackgroundCommandBuffer = null;
        // Captures foreground texture before post-effects
        private CommandBuffer _captureForegroundCommandBuffer = null;
        // Renders captured background texture to the final background alpha
        private CommandBuffer _applyBackgroundCommandBuffer = null;
        // Renders captured foreground texture to the final foreground alpha
        private CommandBuffer _applyForegroundCommandBuffer = null;

        private CameraEvent _clipPlaneCameraEvent = CameraEvent.AfterForwardOpaque;
        private CameraEvent _clipPlaneFixAlphaCameraEvent = CameraEvent.AfterEverything;
        private CameraEvent _captureBackgroundEvent = CameraEvent.BeforeImageEffects;
        private CameraEvent _captureForegroundEvent = CameraEvent.BeforeImageEffects;
        private CameraEvent _applyBackgroundEvent = CameraEvent.AfterEverything;
        private CameraEvent _applyForegroundEvent = CameraEvent.AfterEverything;

        // Tessellated quad
        private Mesh _clipPlaneMesh = null;
        // Clear material
        private Material _clipPlaneSimpleMaterial = null;
        // Transparent material for visual debugging
        private Material _clipPlaneSimpleDebugMaterial = null;
        // Tessellated height map clear material
        private Material _clipPlaneComplexMaterial = null;
        // Tessellated height map clear material for visual debugging
        private Material _clipPlaneComplexDebugMaterial = null;
        private Material _clipPlaneFixAlphaMaterial = null;
        private Material _addAlphaMaterial = null;
        private Material _writeAlphaMaterial = null;
        private Material _forceForwardRenderingMaterial = null;

        private RenderTexture _backgroundRenderTexture = null;
        private RenderTexture _foregroundRenderTexture = null;
        private RenderTexture _complexClipPlaneRenderTexture = null;

        public SDKRender(LIV liv)
        {
            _liv = liv;
            CreateAssets();
        }

        public void Render()
        {
            UpdateBridgeInputFrame();
            SDKUtils.ApplyUserSpaceTransform(this);
            UpdateTextures();
            SDKUtils.CreateBridgeOutputFrame(this);
            InvokePreRender();
            RenderBackground();
            RenderForeground();
            IvokePostRender();
            SDKBridge.IssuePluginEvent();
        }

        private void RenderBackground()
        {
            if (!SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.BACKGROUND_RENDER)) return;

            SDKUtils.SetCamera(_cameraInstance, _inputFrame, localToWorldMatrix, spectatorLayerMask);
            _cameraInstance.targetTexture = _backgroundRenderTexture;

            bool overridePostProcessing = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.OVERRIDE_POST_PROCESSING);

            RenderTexture tempRenderTexture = null;
            
            if (overridePostProcessing)
            {                
                tempRenderTexture = RenderTexture.GetTemporary(_backgroundRenderTexture.width, _backgroundRenderTexture.height, 0, _backgroundRenderTexture.format);
                tempRenderTexture.name = "LIV.TemporaryRenderTexture";

                _captureBackgroundCommandBuffer.Blit(BuiltinRenderTextureType.CurrentActive, tempRenderTexture);
                _applyBackgroundCommandBuffer.Blit(tempRenderTexture, BuiltinRenderTextureType.CurrentActive);

                _cameraInstance.AddCommandBuffer(_captureBackgroundEvent, _captureBackgroundCommandBuffer);
                _cameraInstance.AddCommandBuffer(_applyBackgroundEvent, _applyBackgroundCommandBuffer);
            }

            SDKShaders.StartRendering();
            SDKShaders.StartBackgroundRendering();
            InvokePreRenderBackground();
            SendBackgroundTextureToBridge();
            _cameraInstance.Render();
            InvokePostRenderBackground();
            _cameraInstance.targetTexture = null;
            SDKShaders.StopBackgroundRendering();
            SDKShaders.StopRendering();

            if (overridePostProcessing)
            {
                _cameraInstance.RemoveCommandBuffer(_captureBackgroundEvent, _captureBackgroundCommandBuffer);
                _cameraInstance.RemoveCommandBuffer(_applyBackgroundEvent, _applyBackgroundCommandBuffer);

                _captureBackgroundCommandBuffer.Clear();
                _applyBackgroundCommandBuffer.Clear();

                RenderTexture.ReleaseTemporary(tempRenderTexture);
            }
        }

        private void RenderForeground()
        {
            if (!SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.FOREGROUND_RENDER)) return;

            bool debugClipPlane = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.DEBUG_CLIP_PLANE);
            bool renderComplexClipPlane = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.COMPLEX_CLIP_PLANE);
            bool renderGroundClipPlane = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.GROUND_CLIP_PLANE);
            bool overridePostProcessing = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.OVERRIDE_POST_PROCESSING);
            bool fixPostEffectsAlpha = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.FIX_FOREGROUND_ALPHA) | _liv.fixPostEffectsAlpha;

            // Disable standard assets if required.
            MonoBehaviour[] behaviours = null;
            bool[] wasBehaviourEnabled = null;
            if (disableStandardAssets)
            {
                behaviours = _cameraInstance.gameObject.GetComponents<MonoBehaviour>();
                wasBehaviourEnabled = new bool[behaviours.Length];
                for (var i = 0; i < behaviours.Length; i++)
                {
                    var behaviour = behaviours[i];
                    // generates garbage
                    if (behaviour.enabled && behaviour.GetType().ToString().StartsWith("UnityStandardAssets."))
                    {
                        behaviour.enabled = false;
                        wasBehaviourEnabled[i] = true;
                    }
                }
            }

            CameraClearFlags clearFlags = _cameraInstance.clearFlags;
            Color bgColor = _cameraInstance.backgroundColor;
            Color fogColor = RenderSettings.fogColor;
            RenderSettings.fogColor = new Color(fogColor.r, fogColor.g, fogColor.b, 0f);

            SDKUtils.SetCamera(_cameraInstance, _inputFrame, localToWorldMatrix, spectatorLayerMask);
            _cameraInstance.clearFlags = CameraClearFlags.Color;
            _cameraInstance.backgroundColor = Color.clear;
            _cameraInstance.targetTexture = _foregroundRenderTexture;

            Matrix4x4 clipPlaneTransform = localToWorldMatrix * (Matrix4x4)_inputFrame.clipPlane.transform;
            Matrix4x4 groundClipPlaneTransform = localToWorldMatrix * (Matrix4x4)_inputFrame.groundClipPlane.transform;
            Material clipPlaneMaterial = debugClipPlane ? _clipPlaneSimpleDebugMaterial : _clipPlaneSimpleMaterial;
            Material groundClipPlaneMaterial = clipPlaneMaterial;

            RenderTexture capturedAlphaRenderTexture = RenderTexture.GetTemporary(_foregroundRenderTexture.width, _foregroundRenderTexture.height, 0, _foregroundRenderTexture.format);
            capturedAlphaRenderTexture.name = "LIV.CapturedAlphaRenderTexture";

            RenderTexture tempRenderTexture = null;

            if (renderComplexClipPlane)
            {
                clipPlaneMaterial = debugClipPlane ? _clipPlaneComplexDebugMaterial : _clipPlaneComplexMaterial;
                clipPlaneMaterial.SetTexture(SDKShaders.LIV_CLIP_PLANE_HEIGHT_MAP_PROPERTY, _complexClipPlaneRenderTexture);
                clipPlaneMaterial.SetFloat(SDKShaders.LIV_TESSELLATION_PROPERTY, _inputFrame.clipPlane.tesselation);
            }

            // Render opaque pixels into alpha channel
            _clipPlaneCommandBuffer.DrawMesh(_clipPlaneMesh, Matrix4x4.identity, _clipPlaneFixAlphaMaterial, 0, 0);
            // Render clip plane
            _clipPlaneCommandBuffer.DrawMesh(_clipPlaneMesh, clipPlaneTransform, clipPlaneMaterial, 0, 0);
            // Render ground clip plane
            if (renderGroundClipPlane) _clipPlaneCommandBuffer.DrawMesh(_clipPlaneMesh, groundClipPlaneTransform, groundClipPlaneMaterial, 0, 0);
            // Copy alpha in to texture
            _clipPlaneCommandBuffer.Blit(BuiltinRenderTextureType.CurrentActive, capturedAlphaRenderTexture);
            _addAlphaCommandBuffer.Blit(capturedAlphaRenderTexture, BuiltinRenderTextureType.CurrentActive, _addAlphaMaterial);
            _cameraInstance.AddCommandBuffer(_clipPlaneFixAlphaCameraEvent, _addAlphaCommandBuffer);

            _cameraInstance.AddCommandBuffer(_clipPlaneCameraEvent, _clipPlaneCommandBuffer);

            if (overridePostProcessing || fixPostEffectsAlpha)
            {
                tempRenderTexture = RenderTexture.GetTemporary(_foregroundRenderTexture.width, _foregroundRenderTexture.height, 0, _foregroundRenderTexture.format);
                tempRenderTexture.name = "LIV.TemporaryRenderTexture";

                _captureForegroundCommandBuffer.Blit(BuiltinRenderTextureType.CurrentActive, tempRenderTexture);
                if (fixPostEffectsAlpha)
                {
                    _applyForegroundCommandBuffer.Blit(tempRenderTexture, BuiltinRenderTextureType.CurrentActive, _writeAlphaMaterial);
                }
                else
                {
                    _applyForegroundCommandBuffer.Blit(tempRenderTexture, BuiltinRenderTextureType.CurrentActive);
                }

                _cameraInstance.AddCommandBuffer(_captureForegroundEvent, _captureForegroundCommandBuffer);
                _cameraInstance.AddCommandBuffer(_applyForegroundEvent, _applyForegroundCommandBuffer);
            }

            // Force forward rendering
            if (_cameraInstance.actualRenderingPath == RenderingPath.DeferredLighting ||
                _cameraInstance.actualRenderingPath == RenderingPath.DeferredShading)
            {
                Matrix4x4 forceForwardRenderingMatrix = _cameraInstance.transform.localToWorldMatrix * Matrix4x4.TRS(Vector3.forward * (_cameraInstance.nearClipPlane + 0.1f), Quaternion.identity, Vector3.one);
                Graphics.DrawMesh(_clipPlaneMesh, forceForwardRenderingMatrix, _forceForwardRenderingMaterial, 0, _cameraInstance, 0, new MaterialPropertyBlock(), false, false, false);
            }

            SDKShaders.StartRendering();
            SDKShaders.StartForegroundRendering();
            InvokePreRenderForeground();
            SendForegroundTextureToBridge();
            _cameraInstance.Render();
            InvokePostRenderForeground();
            _cameraInstance.targetTexture = null;
            SDKShaders.StopForegroundRendering();
            SDKShaders.StopRendering();

            if (overridePostProcessing || fixPostEffectsAlpha)
            {
                _cameraInstance.RemoveCommandBuffer(_captureForegroundEvent, _captureForegroundCommandBuffer);
                _cameraInstance.RemoveCommandBuffer(_applyForegroundEvent, _applyForegroundCommandBuffer);

                _captureForegroundCommandBuffer.Clear();
                _applyForegroundCommandBuffer.Clear();

                RenderTexture.ReleaseTemporary(tempRenderTexture);
            }

            _cameraInstance.RemoveCommandBuffer(_clipPlaneCameraEvent, _clipPlaneCommandBuffer);
            _cameraInstance.RemoveCommandBuffer(_clipPlaneFixAlphaCameraEvent, _addAlphaCommandBuffer);

            RenderTexture.ReleaseTemporary(capturedAlphaRenderTexture);            

            _clipPlaneCommandBuffer.Clear();
            _addAlphaCommandBuffer.Clear();

            _cameraInstance.clearFlags = clearFlags;
            _cameraInstance.backgroundColor = bgColor;
            RenderSettings.fogColor = fogColor;

            // Restore disabled behaviours.
            if (behaviours != null)
                for (var i = 0; i < behaviours.Length; i++)
                    if (wasBehaviourEnabled[i])
                        behaviours[i].enabled = true;
        }

        private void CreateAssets()
        {
            bool cameraReferenceEnabled = cameraReference.enabled;
            if (cameraReferenceEnabled)
            {
                cameraReference.enabled = false;
            }
            bool cameraReferenceActive = cameraReference.gameObject.activeSelf;
            if (cameraReferenceActive)
            {
                cameraReference.gameObject.SetActive(false);
            }

            GameObject cloneGO = (GameObject)Object.Instantiate(cameraReference.gameObject, _liv.stage);
            _cameraInstance = (Camera)cloneGO.GetComponent("Camera");

            SDKUtils.CleanCameraBehaviours(_cameraInstance, _liv.excludeBehaviours);

            if (cameraReferenceActive != cameraReference.gameObject.activeSelf)
            {
                cameraReference.gameObject.SetActive(cameraReferenceActive);
            }
            if (cameraReferenceEnabled != cameraReference.enabled)
            {
                cameraReference.enabled = cameraReferenceEnabled;
            }

            _cameraInstance.name = "LIV Camera";
            if (_cameraInstance.tag == "MainCamera")
            {
                _cameraInstance.tag = "Untagged";
            }

            _cameraInstance.transform.localScale = Vector3.one;
            _cameraInstance.rect = new Rect(0, 0, 1, 1);
            _cameraInstance.depth = 0;
#if UNITY_5_4_OR_NEWER
            _cameraInstance.stereoTargetEye = StereoTargetEyeMask.None;
#endif
#if UNITY_5_6_OR_NEWER
            _cameraInstance.allowMSAA = false;
#endif
            _cameraInstance.enabled = false;
            _cameraInstance.gameObject.SetActive(true);

            _clipPlaneMesh = new Mesh();
            SDKUtils.CreateClipPlane(_clipPlaneMesh, 10, 10, true, 1000f);
            _clipPlaneSimpleMaterial = new Material(Shader.Find(SDKShaders.LIV_CLIP_PLANE_SIMPLE_SHADER));
            _clipPlaneSimpleDebugMaterial = new Material(Shader.Find(SDKShaders.LIV_CLIP_PLANE_SIMPLE_DEBUG_SHADER));
            _clipPlaneComplexMaterial = new Material(Shader.Find(SDKShaders.LIV_CLIP_PLANE_COMPLEX_SHADER));
            _clipPlaneComplexDebugMaterial = new Material(Shader.Find(SDKShaders.LIV_CLIP_PLANE_COMPLEX_DEBUG_SHADER));
            _clipPlaneFixAlphaMaterial = new Material(Shader.Find(SDKShaders.LIV_CLIP_PLANE_FIX_ALPHA_SHADER));
            _addAlphaMaterial = new Material(Shader.Find(SDKShaders.LIV_ADD_ALPHA_SHADER));
            _writeAlphaMaterial = new Material(Shader.Find(SDKShaders.LIV_WRITE_ALPHA_SHADER));
            _forceForwardRenderingMaterial = new Material(Shader.Find(SDKShaders.LIV_FORCE_FORWARD_RENDERING_SHADER));

            _clipPlaneCommandBuffer = new CommandBuffer();
            _clipPlaneCommandBuffer.name = "LIV.foregroundClipPlane";

            _addAlphaCommandBuffer = new CommandBuffer();
            _addAlphaCommandBuffer.name = "LIV.foregroundAddAlpha";

            _captureBackgroundCommandBuffer = new CommandBuffer();
            _captureBackgroundCommandBuffer.name = "LIV.captureBackground";
            _captureForegroundCommandBuffer = new CommandBuffer();
            _captureForegroundCommandBuffer.name = "LIV.captureForeground";

            _applyBackgroundCommandBuffer = new CommandBuffer();
            _applyBackgroundCommandBuffer.name = "LIV.applyBackground";
            _applyForegroundCommandBuffer = new CommandBuffer();
            _applyForegroundCommandBuffer.name = "LIV.applyForeground";
        }

        private void DestroyAssets()
        {
            if (_cameraInstance != null)
            {
                Object.Destroy(_cameraInstance.gameObject);
                _cameraInstance = null;
            }

            SDKUtils.DestroyObject<Mesh>(ref _clipPlaneMesh);
            SDKUtils.DestroyObject<Material>(ref _clipPlaneSimpleMaterial);
            SDKUtils.DestroyObject<Material>(ref _clipPlaneSimpleDebugMaterial);
            SDKUtils.DestroyObject<Material>(ref _clipPlaneComplexMaterial);
            SDKUtils.DestroyObject<Material>(ref _clipPlaneComplexDebugMaterial);
            SDKUtils.DestroyObject<Material>(ref _clipPlaneFixAlphaMaterial);
            SDKUtils.DestroyObject<Material>(ref _addAlphaMaterial);
            SDKUtils.DestroyObject<Material>(ref _writeAlphaMaterial);
            SDKUtils.DestroyObject<Material>(ref _forceForwardRenderingMaterial);

            SDKUtils.DisposeObject<CommandBuffer>(ref _clipPlaneCommandBuffer);
            SDKUtils.DisposeObject<CommandBuffer>(ref _addAlphaCommandBuffer);
            SDKUtils.DisposeObject<CommandBuffer>(ref _captureBackgroundCommandBuffer);
            SDKUtils.DisposeObject<CommandBuffer>(ref _captureForegroundCommandBuffer);
            SDKUtils.DisposeObject<CommandBuffer>(ref _applyBackgroundCommandBuffer);
            SDKUtils.DisposeObject<CommandBuffer>(ref _applyForegroundCommandBuffer);
        }

        public void Dispose()
        {
            ReleaseBridgePoseControl();
            DestroyAssets();
            SDKUtils.DestroyTexture(ref _backgroundRenderTexture);
            SDKUtils.DestroyTexture(ref _foregroundRenderTexture);
            SDKUtils.DestroyTexture(ref _complexClipPlaneRenderTexture);
        }
    }
}
#endif