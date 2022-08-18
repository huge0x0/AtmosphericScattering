using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AtmosphericScattering {
    [Serializable]
    public class AtmosphericScatteringData {
        public float PlanetaryRadius = 63;//km
        public float ScaledHeightRayleigh = 1.2f;
        public float ScaledHeightMie = 0.2f;
        public Vector3 ScatteringRateRayleigh = new Vector3(5.8f, 13.5f, 33);
        public Vector3 ScatteringRateMie = new Vector3(4, 4, 4);
        public Color SunColor = Color.white;
        [Range(0, 5)]
        public float SunIntensity = 1000;

        [Min(0)]
        public float CameraHeight = 0;
        [Range(0, 360)]
        public float SunHorizontalAngle = 0;
        [Range(-90, 90)]
        public float SunVerticalAngle = 30;
        [Range(0, 0.1f)]
        public float SunViewRange = 0.05f;
        public Color GroundColor = Color.gray;
        [Range(0.75f, 0.99f)]
        public float MieG = 0.98f;
    }
    class AtmosphericScatteringRenderPass : ScriptableRenderPass {
        public AtmosphericScatteringRenderPass() {
            renderPassEvent = RenderPassEvent.BeforeRenderingSkybox;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor) {
        }

        public override void FrameCleanup(CommandBuffer cmd) {
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
            CommandBuffer cmd = CommandBufferPool.Get("AtmosphericScattering");
            UpdateComputeShaderData(cmd, TransmittanceLutCS);
            UpdateComputeShaderData(cmd, AtmosphericScatteringCS);
            UpdateTransmittanceLUT(cmd);
            UpdateSkyView(cmd);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        #region Transmittance

        public ComputeShader TransmittanceLutCS;
        private int mKernelUpdateTransmittance = -1;
        private Vector3Int mKernelUpdateTransmittanceGroupSize = Vector3Int.one;
        private RenderTexture mTransmittanceRT;
        private Vector2Int mTransmittanceRTSize = new Vector2Int(256, 256);
        private void UpdateTransmittanceLUT(CommandBuffer cmd) {
            if (mKernelUpdateTransmittance < 0) {
                if (TransmittanceLutCS != null) {
                    mKernelUpdateTransmittance = TransmittanceLutCS.FindKernel("UpdateTransmittance");
                    TransmittanceLutCS.GetKernelThreadGroupSizes(mKernelUpdateTransmittance,
                        out var groupSizeX, out var groupSizeY, out var groupSizeZ);
                    mKernelUpdateTransmittanceGroupSize = new Vector3Int((int)groupSizeX, (int)groupSizeY, (int)groupSizeZ);
                }
                else {
                    return;
                }
            }
            
            if (mTransmittanceRT == null) {
                mTransmittanceRT = new RenderTexture(mTransmittanceRTSize.x, mTransmittanceRTSize.y, 1,
                    RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear) {
                    enableRandomWrite = true,
                    name = "_OpticalDepthLUT",
                };
                mTransmittanceRT.Create();
            }
            
            cmd.SetComputeTextureParam(TransmittanceLutCS, mKernelUpdateTransmittance, ShaderPropertyID._OpticalDepthLUTW, mTransmittanceRT);
            cmd.SetComputeVectorParam(TransmittanceLutCS, ShaderPropertyID._LutSize_InvSize,
                new Vector4(mTransmittanceRTSize.x, mTransmittanceRTSize.y, 1f / mTransmittanceRTSize.x,
                    1f / mTransmittanceRTSize.y));
            cmd.DispatchCompute(TransmittanceLutCS, mKernelUpdateTransmittance, 
                mTransmittanceRTSize.x / mKernelUpdateTransmittanceGroupSize.x, mTransmittanceRTSize.y / mKernelUpdateTransmittanceGroupSize.y, 1);
            cmd.SetGlobalTexture(ShaderPropertyID._OpticalDepthLUT, mTransmittanceRT);
        }

        #endregion

        #region Data

        private AtmosphericScatteringData mData = new AtmosphericScatteringData();
        private bool mNeedUpdateData = true;
        public void SetData(AtmosphericScatteringData data) {
            mData = data;
            MarkDataChanged();
        }

        public void MarkDataChanged() {
            mNeedUpdateData = true;
        }
        
        private void UpdateComputeShaderData(CommandBuffer cmd, ComputeShader computeShader) {
            if (mNeedUpdateData && mData != null) {
                cmd.SetComputeFloatParam(computeShader, ShaderPropertyID._PlanetaryRadius, mData.PlanetaryRadius);
                cmd.SetComputeFloatParam(computeShader, ShaderPropertyID._ScaledHeightRayleigh, mData.ScaledHeightRayleigh);
                cmd.SetComputeFloatParam(computeShader, ShaderPropertyID._ScaledHeightMie, mData.ScaledHeightMie);
                cmd.SetComputeFloatParam(computeShader, ShaderPropertyID._AtmosphericMaxHeight, mData.ScaledHeightRayleigh * 8);
                
                cmd.SetComputeFloatParam(computeShader, ShaderPropertyID._CameraHeight, mData.CameraHeight);
                Quaternion sunRotation = Quaternion.Euler(mData.SunVerticalAngle, mData.SunHorizontalAngle, 0);
                Vector3 sunDir = sunRotation * Vector3.forward;
                cmd.SetComputeVectorParam(computeShader, ShaderPropertyID._SunDir, sunDir.normalized);
                cmd.SetComputeVectorParam(computeShader, ShaderPropertyID._SunInscattering, mData.SunColor * Mathf.Pow(10, mData.SunIntensity));
                cmd.SetComputeVectorParam(computeShader, ShaderPropertyID._ScatteringRateRayleigh, mData.ScatteringRateRayleigh * 10e-6f);
                cmd.SetComputeVectorParam(computeShader, ShaderPropertyID._ScatteringRateMie, mData.ScatteringRateMie * 10e-6f);
                cmd.SetComputeVectorParam(computeShader, ShaderPropertyID._GroundColor, mData.GroundColor);
                cmd.SetComputeFloatParam(computeShader, ShaderPropertyID._SunVisibilityRange, mData.SunViewRange);
                cmd.SetComputeVectorParam(computeShader, ShaderPropertyID._MieG, new Vector2(mData.MieG, mData.MieG * mData.MieG));
            }
        }
        

        #endregion

        #region SkyView
        public ComputeShader AtmosphericScatteringCS;
        public Material SkyBoxMat;
        private int mKernelUpdateSkyView = -1;
        private Vector3Int mKernelUpdateSkyViewGroupSize = Vector3Int.one;
        private RenderTexture mSkyViewRT;
        private Vector2Int mSkyViewRTSize = new Vector2Int(512, 512);
        public void UpdateSkyView(CommandBuffer cmd) {
            if (mKernelUpdateSkyView < 0) {
                if (AtmosphericScatteringCS != null) {
                    mKernelUpdateSkyView = AtmosphericScatteringCS.FindKernel("IntegrateInScattering");
                    AtmosphericScatteringCS.GetKernelThreadGroupSizes(mKernelUpdateSkyView,
                        out var groupSizeX, out var groupSizeY, out var groupSizeZ);
                    mKernelUpdateSkyViewGroupSize = new Vector3Int((int)groupSizeX, (int)groupSizeY, (int)groupSizeZ);
                }
                else {
                    return;
                }
            }
            
            if (mSkyViewRT == null) {
                mSkyViewRT = new RenderTexture(mSkyViewRTSize.x, mSkyViewRTSize.y, 1,
                    RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) {
                    enableRandomWrite = true,
                    name = "_SkyViewTex",
                };
                mSkyViewRT.Create();
            }

            cmd.SetComputeTextureParam(AtmosphericScatteringCS, mKernelUpdateSkyView, ShaderPropertyID._SkyViewTexW,
                mSkyViewRT);
            cmd.SetComputeTextureParam(AtmosphericScatteringCS, mKernelUpdateSkyView, ShaderPropertyID._SkyViewTexW,
                mSkyViewRT);
            cmd.SetComputeVectorParam(AtmosphericScatteringCS, ShaderPropertyID._SkyViewSize_InvSize,
                new Vector4(mSkyViewRTSize.x, mSkyViewRTSize.y, 1f / mSkyViewRTSize.x,
                    1f / mSkyViewRTSize.y));
            cmd.DispatchCompute(AtmosphericScatteringCS, mKernelUpdateSkyView, 
                mSkyViewRTSize.x / mKernelUpdateSkyViewGroupSize.x, mSkyViewRTSize.y / mKernelUpdateSkyViewGroupSize.y, 1);
            if (SkyBoxMat != null) {
                SkyBoxMat.SetTexture(ShaderPropertyID._SkyViewTex, mSkyViewRT);
                RenderSettings.skybox = SkyBoxMat;
            }
        }

        #endregion

        public void CleanUp() {
            mKernelUpdateTransmittance = -1;
            mKernelUpdateSkyView = -1;
            mNeedUpdateData = true;
            mSkyViewRT.Release();
            CoreUtils.Destroy(mSkyViewRT);
            mTransmittanceRT.Release();
            CoreUtils.Destroy(mTransmittanceRT);
        }
        
        private static class ShaderPropertyID {
            public static readonly int _PlanetaryRadius = Shader.PropertyToID("_PlanetaryRadius");
            public static readonly int _ScaledHeightRayleigh = Shader.PropertyToID("_ScaledHeightRayleigh");
            public static readonly int _ScaledHeightMie = Shader.PropertyToID("_ScaledHeightMie");
            public static readonly int _AtmosphericMaxHeight = Shader.PropertyToID("_AtmosphericMaxHeight");
            
            public static readonly int _OpticalDepthLUTW = Shader.PropertyToID("_OpticalDepthLUTW");
            public static readonly int _LutSize_InvSize = Shader.PropertyToID("_LutSize_InvSize");
            
            public static readonly int _OpticalDepthLUT = Shader.PropertyToID("_OpticalDepthLUT");
            public static readonly int _SkyViewTexW = Shader.PropertyToID("_SkyViewTexW");
            public static readonly int _SkyViewSize_InvSize = Shader.PropertyToID("_SkyViewSize_InvSize");
            public static readonly int _CameraHeight = Shader.PropertyToID("_CameraHeight");
            public static readonly int _SunDir = Shader.PropertyToID("_SunDir");
            public static readonly int _SunInscattering = Shader.PropertyToID("_SunInscattering");
            public static readonly int _ScatteringRateMie = Shader.PropertyToID("_ScatteringRateMie");
            public static readonly int _ScatteringRateRayleigh = Shader.PropertyToID("_ScatteringRateRayleigh");
            public static readonly int _GroundColor = Shader.PropertyToID("_GroundColor");
            public static readonly int _SunVisibilityRange = Shader.PropertyToID("_SunVisibilityRange");
            public static readonly int _MieG = Shader.PropertyToID("_MieG");
            
            public static readonly int _SkyViewTex = Shader.PropertyToID("_SkyViewTex");
        }
    }

}