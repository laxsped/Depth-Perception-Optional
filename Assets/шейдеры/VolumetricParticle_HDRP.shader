Shader "Custom/VolumetricParticle_HDRP"
{
    Properties
    {
        _MainTex            ("Particle Texture", 2D)              = "white" {}
        _Color              ("Base Color", Color)                  = (1, 1, 1, 1)
        _Brightness         ("Brightness", Range(0, 5))            = 0.5

        _SoftFactor         ("Soft Particle Factor", Range(0.01, 20)) = 3.0

        _ScatterColor       ("Scatter Color", Color)               = (1, 1, 1, 1)
        _ScatterStrength    ("Scatter Strength", Range(0, 3))      = 0.5
        _Anisotropy         ("Anisotropy (g)", Range(-0.99, 0.99)) = 0.3

        _EdgeSoftness       ("Edge Softness", Range(0.01, 4))      = 1.2
        _InnerGlow          ("Inner Glow", Range(0, 1))            = 0.0
        _GlowFalloff        ("Glow Falloff", Range(0.5, 8))        = 2.0

        _DensityScale       ("Density Scale", Range(0.01, 2))      = 0.4
        _AbsorptionColor    ("Absorption Color", Color)            = (0.05, 0.05, 0.05, 1)

        _AmbientStrength    ("Ambient Strength", Range(0, 2))      = 0.4

        _DepthFadeStart     ("Depth Fade Start", Range(0, 100))    = 0.0
        _DepthFadeEnd       ("Depth Fade End",   Range(0, 100))    = 30.0

        [Enum(Premultiply,0,Alpha,1,Additive,2)]
        _BlendMode          ("Blend Mode", Float)                  = 0

        [HideInInspector] _SrcBlend ("__src", Float) = 5.0
        [HideInInspector] _DstBlend ("__dst", Float) = 10.0
        [HideInInspector] _ZWrite   ("__zw",  Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"  = "HDRenderPipeline"
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull Off

            HLSLPROGRAM

            #pragma target 4.5
            #pragma only_renderers d3d11 vulkan metal
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float  _Brightness;
                float  _SoftFactor;
                float4 _ScatterColor;
                float  _ScatterStrength;
                float  _Anisotropy;
                float  _EdgeSoftness;
                float  _InnerGlow;
                float  _GlowFalloff;
                float  _DensityScale;
                float4 _AbsorptionColor;
                float  _AmbientStrength;
                float  _DepthFadeStart;
                float  _DepthFadeEnd;
                float  _BlendMode;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 viewDirWS  : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float HenyeyGreenstein(float cosTheta, float g)
            {
                float g2 = g * g;
                return (1.0 - g2) / (4.0 * PI * pow(abs(1.0 + g2 - 2.0 * g * cosTheta) + 1e-5, 1.5));
            }
            float HGNormalized(float cosTheta, float g)
            {
                return saturate(HenyeyGreenstein(cosTheta, g) / (HenyeyGreenstein(1.0, g) + 1e-5));
            }
            float3 BeerLambert(float3 absorb, float density)
            {
                return exp(-absorb * density);
            }

            // Затухание point/spot по дистанции (HDRP-style: rangeAttenuationScale/Bias)
            float PunctualAttenuation(float distSq, float rangeAttenuationScale, float rangeAttenuationBias)
            {
                float factor = distSq * rangeAttenuationScale + rangeAttenuationBias;
                float smoothFactor = saturate(1.0 - factor * factor);
                return (1.0 / max(distSq, 1e-4)) * smoothFactor * smoothFactor;
            }

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float3 posWS   = TransformObjectToWorld(IN.positionOS.xyz);
                float4 posCS   = TransformWorldToHClip(posWS);
                OUT.positionCS = posCS;
                OUT.positionWS = posWS;
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color      = IN.color;
                OUT.viewDirWS  = normalize(GetCurrentViewPosition() - posWS);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                texColor       *= _Color * IN.color;

                float2 cUV      = IN.uv * 2.0 - 1.0;
                float  r        = length(cUV);
                float  edgeFade = pow(saturate(1.0 - r), _EdgeSoftness);
                float  glow     = pow(saturate(1.0 - r), _GlowFalloff) * _InnerGlow;

                // Soft Particles
                uint2  px          = uint2(IN.positionCS.xy);
                float  rawDepth    = LOAD_TEXTURE2D_X(_CameraDepthTexture, px).r;
                float  sceneLinear = LinearEyeDepth(rawDepth, _ZBufferParams);
                float  softFade    = saturate((sceneLinear - IN.positionCS.w) / max(_SoftFactor, 0.001));

                float3 viewDir     = normalize(IN.viewDirWS);
                float3 totalLight  = 0;

                // --- Directional lights ---
                uint dirCount = _DirectionalLightCount;
                for (uint d = 0; d < dirCount; d++)
                {
                    float3 lDir   = normalize(-_DirectionalLightDatas[d].forward.xyz);
                    float3 lColor = _DirectionalLightDatas[d].color.rgb;
                    float  cosT   = dot(-viewDir, lDir);
                    float  phase  = HGNormalized(cosT, _Anisotropy);
                    float  diff   = abs(dot(viewDir, lDir)) * 0.5 + 0.5;
                    totalLight   += lColor * (phase * _ScatterStrength + diff);
                }

                // --- Punctual lights (point + spot) ---
                uint punctualCount = _PunctualLightCount;
                for (uint p = 0; p < punctualCount; p++)
                {
                    float3 lPos   = _LightDatas[p].positionRWS.xyz + _WorldSpaceCameraPos.xyz;
                    float3 toL    = lPos - IN.positionWS;
                    float  distSq = dot(toL, toL);
                    float3 lDir   = normalize(toL);
                    float3 lColor = _LightDatas[p].color.rgb;

                    float  atten  = PunctualAttenuation(
                                        distSq,
                                        _LightDatas[p].rangeAttenuationScale,
                                        _LightDatas[p].rangeAttenuationBias);

                    // Spot cone
                    float  cosSpot   = dot(-lDir, _LightDatas[p].forward.xyz);
                    float  spotAtten = saturate(cosSpot * _LightDatas[p].angleScale +
                                                          _LightDatas[p].angleOffset);
                    spotAtten = spotAtten * spotAtten;

                    float  cosT  = dot(-viewDir, lDir);
                    float  phase = HGNormalized(cosT, _Anisotropy);
                    float  diff  = abs(dot(viewDir, lDir)) * 0.5 + 0.5;

                    totalLight  += lColor * atten * spotAtten * (phase * _ScatterStrength + diff);
                }

                // Ambient
                float3 ambient = float3(_AmbientStrength, _AmbientStrength, _AmbientStrength);

                // Beer–Lambert
                float  density = texColor.a * edgeFade * _DensityScale;
                float3 absorb  = BeerLambert(_AbsorptionColor.rgb * 5.0, density);

                // Distance fade
                float  camDist = length(IN.positionWS - GetCurrentViewPosition());
                float  distFade = 1.0 - saturate((camDist - _DepthFadeStart) /
                                  max(_DepthFadeEnd - _DepthFadeStart, 0.001));

                float3 finalColor = (texColor.rgb * absorb * (totalLight + ambient)
                                   + glow * (totalLight + ambient)) * _Brightness;

                float  finalAlpha = texColor.a * edgeFade * softFade * distFade;

                if (_BlendMode < 0.5)
                    finalColor *= finalAlpha;

                return float4(finalColor, finalAlpha);
            }

            ENDHLSL
        }

        Pass
        {
            Name "TransparentDepthPrepass"
            Tags { "LightMode" = "TransparentDepthPrepass" }
            ZWrite On
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 vulkan metal
            #pragma vertex   vertD
            #pragma fragment fragD
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            struct AttrD { float4 pos : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VaryD { float4 pos : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO  };

            VaryD vertD(AttrD IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                VaryD OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.pos = TransformObjectToHClip(IN.pos.xyz);
                return OUT;
            }
            void fragD(VaryD IN) {}
            ENDHLSL
        }
    }
}
