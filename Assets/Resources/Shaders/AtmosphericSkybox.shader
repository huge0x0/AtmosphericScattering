Shader "AtomsphericScattering/AtmosphericSkybox"
{
    Properties
    {
        _SkyViewTex ("_SkyViewTex", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float3 viewRayWS : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            TEXTURE2D(_SkyViewTex);
            SAMPLER(_MyLinearClampSampler);
            
            #define PI_2 6.28319
            
            v2f vert (appdata v)
            {
                v2f o;
                VertexPositionInputs inputs = GetVertexPositionInputs(v.vertex.xyz);
                o.vertex = inputs.positionCS;
                o.viewRayWS = inputs.positionWS  - _WorldSpaceCameraPos;
                return o;
            }
            
            half4 frag (v2f i) : SV_Target
            {
                float3 viewDir = normalize(i.viewRayWS);
                float2 uv;
                uv.y = viewDir.y * 0.5 + 0.5;
                float2 horizontalDir = normalize(viewDir.xz);
                uv.x = acos(horizontalDir.x);
                uv.x = horizontalDir.y > 0 ? uv.x : (PI_2 - uv.x);
                uv.x /= PI_2;
                half3 col = SAMPLE_TEXTURE2D(_SkyViewTex, _MyLinearClampSampler, uv).xyz;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}