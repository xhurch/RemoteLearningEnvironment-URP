#if LIV_UNIVERSAL_RENDER
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;

namespace LIV.SDK.Unity
{
    public partial class SDKRender : System.IDisposable
    {
        // Renders the clip plane in the foreground texture
        private SDKPass _clipPlanePass = null;
        // Renders the clipped opaque content in to the foreground texture alpha
        private SDKPass _addAlphaPass = null;
        // Captures background texture before post-effects
        private SDKPass _captureBackgroundPass = null;
        // Captures foreground texture before post-effects
        private SDKPass _captureForegroundPass = null;
        // Renders captured background texture to the final background alpha
        private SDKPass _applyBackgroundPass = null;
        // Renders captured foreground texture to the final foreground alpha
        private SDKPass _applyForegroundPass = null;

        private RenderPassEvent _clipPlaneEvent = RenderPassEvent.AfterRenderingOpaques;
        private RenderPassEvent _addAlphaEvent = RenderPassEvent.AfterRenderingPostProcessing;
        private RenderPassEvent _captureBackgroundEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        private RenderPassEvent _captureForegroundEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        private RenderPassEvent _applyBackgroundEvent = RenderPassEvent.AfterRenderingPostProcessing;
        private RenderPassEvent _applyForegroundEvent = RenderPassEvent.AfterRenderingPostProcessing;

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

        private UniversalAdditionalCameraData _universalAdditionalCameraData = null;
        private RenderTargetIdentifier _cameraColorTextureIdentifier = new RenderTargetIdentifier("_CameraColorTexture");

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
#if UNITY_EDITOR
                tempRenderTexture.name = "LIV.TemporaryRenderTexture";
#endif

                _captureBackgroundPass.commandBuffer.Blit(BuiltinRenderTextureType.CurrentActive, tempRenderTexture);
                _applyBackgroundPass.commandBuffer.Blit(tempRenderTexture, BuiltinRenderTextureType.CurrentActive);

                SDKUniversalRenderFeature.AddPass(_captureBackgroundPass);
                SDKUniversalRenderFeature.AddPass(_applyBackgroundPass);                
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
                _captureBackgroundPass.commandBuffer.Clear();
                _applyBackgroundPass.commandBuffer.Clear();
                RenderTexture.ReleaseTemporary(tempRenderTexture);
            }

            SDKUniversalRenderFeature.ClearPasses();
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
#if UNITY_EDITOR
            capturedAlphaRenderTexture.name = "LIV.CapturedAlphaRenderTexture";
#endif

            RenderTexture tempRenderTexture = null;

            if (renderComplexClipPlane)
            {
                clipPlaneMaterial = debugClipPlane ? _clipPlaneComplexDebugMaterial : _clipPlaneComplexMaterial;
                clipPlaneMaterial.SetTexture(SDKShaders.LIV_CLIP_PLANE_HEIGHT_MAP_PROPERTY, _complexClipPlaneRenderTexture);
                clipPlaneMaterial.SetFloat(SDKShaders.LIV_TESSELLATION_PROPERTY, _inputFrame.clipPlane.tesselation);
            }

            // Render opaque pixels into alpha channel
            _clipPlanePass.commandBuffer.DrawMesh(_clipPlaneMesh, Matrix4x4.identity, _clipPlaneFixAlphaMaterial, 0, 0);
            // Render clip plane
            _clipPlanePass.commandBuffer.DrawMesh(_clipPlaneMesh, clipPlaneTransform, clipPlaneMaterial, 0, 0);
            // Render ground clip plane
            if (renderGroundClipPlane) _clipPlanePass.commandBuffer.DrawMesh(_clipPlaneMesh, groundClipPlaneTransform, groundClipPlaneMaterial, 0, 0);
            // Copy alpha in to texture
            _clipPlanePass.commandBuffer.Blit(BuiltinRenderTextureType.CurrentActive, capturedAlphaRenderTexture);
            _clipPlanePass.commandBuffer.SetRenderTarget(_cameraColorTextureIdentifier);
            _addAlphaPass.commandBuffer.Blit(capturedAlphaRenderTexture, BuiltinRenderTextureType.CurrentActive, _addAlphaMaterial);

            if (overridePostProcessing || fixPostEffectsAlpha)
            {
                tempRenderTexture = RenderTexture.GetTemporary(_foregroundRenderTexture.width, _foregroundRenderTexture.height, 0, _foregroundRenderTexture.format);
#if UNITY_EDITOR
                tempRenderTexture.name = "LIV.TemporaryRenderTexture";
#endif
                _captureForegroundPass.commandBuffer.Blit(BuiltinRenderTextureType.CurrentActive, tempRenderTexture);
                if (fixPostEffectsAlpha)
                {
                    _applyForegroundPass.commandBuffer.Blit(tempRenderTexture, BuiltinRenderTextureType.CurrentActive, _writeAlphaMaterial);
                }
                else
                {
                    _applyForegroundPass.commandBuffer.Blit(tempRenderTexture, BuiltinRenderTextureType.CurrentActive);
                }

                SDKUniversalRenderFeature.AddPass(_captureForegroundPass);
                SDKUniversalRenderFeature.AddPass(_applyForegroundPass);                
            }

            SDKUniversalRenderFeature.AddPass(_clipPlanePass);
            SDKUniversalRenderFeature.AddPass(_addAlphaPass);

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
                _captureForegroundPass.commandBuffer.Clear();
                _applyForegroundPass.commandBuffer.Clear();

                RenderTexture.ReleaseTemporary(tempRenderTexture);
            }

            RenderTexture.ReleaseTemporary(capturedAlphaRenderTexture);

            _clipPlanePass.commandBuffer.Clear();
            _addAlphaPass.commandBuffer.Clear();

            SDKUniversalRenderFeature.ClearPasses();

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
            _universalAdditionalCameraData = _cameraInstance.GetComponent<UniversalAdditionalCameraData>();

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

            _clipPlanePass = new SDKPass();
            _clipPlanePass.renderPassEvent = _clipPlaneEvent;
            _clipPlanePass.commandBuffer = new CommandBuffer();
            _clipPlanePass.commandBuffer.name = "LIV.foregroundClipPlane";

            _addAlphaPass = new SDKPass();
            _addAlphaPass.renderPassEvent = _addAlphaEvent;
            _addAlphaPass.commandBuffer = new CommandBuffer();
            _addAlphaPass.commandBuffer.name = "LIV.foregroundAddAlpha";

            _captureBackgroundPass = new SDKPass();
            _captureBackgroundPass.renderPassEvent = _captureBackgroundEvent;
            _captureBackgroundPass.commandBuffer = new CommandBuffer();
            _captureBackgroundPass.commandBuffer.name = "LIV.captureBackground";

            _captureForegroundPass = new SDKPass();
            _captureForegroundPass.renderPassEvent = _captureForegroundEvent;
            _captureForegroundPass.commandBuffer = new CommandBuffer();
            _captureForegroundPass.commandBuffer.name = "LIV.captureForeground";

            _applyBackgroundPass = new SDKPass();
            _applyBackgroundPass.renderPassEvent = _applyBackgroundEvent;
            _applyBackgroundPass.commandBuffer = new CommandBuffer();
            _applyBackgroundPass.commandBuffer.name = "LIV.applyBackground";

            _applyForegroundPass = new SDKPass();
            _applyForegroundPass.renderPassEvent = _applyForegroundEvent;
            _applyForegroundPass.commandBuffer = new CommandBuffer();
            _applyForegroundPass.commandBuffer.name = "LIV.applyForeground";

            _universalAdditionalCameraData.antialiasing = AntialiasingMode.None;
            _universalAdditionalCameraData.antialiasingQuality = AntialiasingQuality.Low;
            _universalAdditionalCameraData.dithering = false;
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

            SDKUtils.DisposeObject<CommandBuffer>(ref _clipPlanePass.commandBuffer);
            SDKUtils.DisposeObject<CommandBuffer>(ref _addAlphaPass.commandBuffer);
            SDKUtils.DisposeObject<CommandBuffer>(ref _captureBackgroundPass.commandBuffer);
            SDKUtils.DisposeObject<CommandBuffer>(ref _captureForegroundPass.commandBuffer);
            SDKUtils.DisposeObject<CommandBuffer>(ref _applyBackgroundPass.commandBuffer);
            SDKUtils.DisposeObject<CommandBuffer>(ref _applyForegroundPass.commandBuffer);
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