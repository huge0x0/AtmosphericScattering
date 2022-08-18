using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AtmosphericScattering {
    public class AtmosphericScatteringRenderFeature : ScriptableRendererFeature {

        public bool UseCustomResLoader = false;
        public ComputeShader UpdateOpticalDepthCS;
        public ComputeShader UpdateSkyViewCS;
        public Material SkyboxMat;
        public AtmosphericScatteringData Data;
        public static Func<ComputeShader> GetUpdateOpticalDepthCS;
        public static Func<ComputeShader> GetUpdateSkyViewCS;
        public static Func<Material> GetSkyboxMat;
        public static Func<AtmosphericScatteringData> GetData;
        
        private AtmosphericScatteringRenderPass m_ScriptablePass;

        public override void Create() {
            m_ScriptablePass = new AtmosphericScatteringRenderPass();
        }

        private void OnDisable() {
            m_ScriptablePass.CleanUp();
        }

        // Here you can inject one or multiple render passes in the renderer.
        // This method is called when setting up the renderer once per-camera.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            if (UseCustomResLoader) {
                if (UpdateOpticalDepthCS == null && GetUpdateOpticalDepthCS != null) {
                    UpdateOpticalDepthCS = GetUpdateOpticalDepthCS();
                }
                if (UpdateSkyViewCS == null && GetUpdateSkyViewCS != null) {
                    UpdateSkyViewCS = GetUpdateSkyViewCS();
                }
                if (SkyboxMat == null && GetUpdateOpticalDepthCS != null) {
                    SkyboxMat = GetSkyboxMat();
                }
                if (Data == null && GetData != null) {
                    Data = GetData();
                }
            }
            if (UpdateSkyViewCS != null && UpdateOpticalDepthCS != null && SkyboxMat != null && Data != null) {
                m_ScriptablePass.AtmosphericScatteringCS = UpdateSkyViewCS;
                m_ScriptablePass.TransmittanceLutCS = UpdateOpticalDepthCS;
                m_ScriptablePass.SkyBoxMat = SkyboxMat;
                m_ScriptablePass.SetData(Data);
                renderer.EnqueuePass(m_ScriptablePass);
            }
        }
    }



}