Shader "Custom/HDRP/LiquidOnly"
{
    Properties
    {
        [HDR]_LiquidColor ("Liquid Color", Color) = (0.12,0.55,1.0,1)
        [HDR]_DeepColor   ("Deep Color", Color)   = (0.03,0.20,0.62,1)
        [HDR]_FoamColor   ("Foam Color", Color)   = (1,1,1,1)

        _FillAmount       ("Fill Amount", Range(0,1)) = 0.5
        _WobbleX          ("Wobble X", Range(-1,1)) = 0
        _WobbleZ          ("Wobble Z", Range(-1,1)) = 0
        _SloshScale       ("Slosh Scale", Range(0,10)) = 6.0
        _FoamWidth        ("Foam Width", Range(0.001,0.08)) = 0.015

        _FillLevelWS      ("Fill Level WS", Float) = 0
        _PivotWS          ("Pivot WS", Vector) = (0,0,0,0)
        _DepthSpanWS      ("Depth Span WS", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="HDRenderPipeline"
            "Queue"="AlphaTest"
            "RenderType"="TransparentCutout"
        }

        Pass
        {
            Name "Liquid"
            Tags { "LightMode"="ForwardOnly" }

            Cull Back
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            float4 _LiquidColor;
            float4 _DeepColor;
            float4 _FoamColor;
            float _WobbleX;
            float _WobbleZ;
            float _SloshScale;
            float _FoamWidth;
            float _FillLevelWS;
            float4 _PivotWS;
            float _DepthSpanWS;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS);
                o.positionCS = TransformWorldToHClip(ws);
                o.positionWS = ws;
                o.normalWS = normalize(TransformObjectToWorldNormal(v.normalOS));
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 dXZ = i.positionWS.xz - _PivotWS.xz;
                float sloshY = _FillLevelWS + (dXZ.x * _WobbleX + dXZ.y * _WobbleZ) * _SloshScale;

                // Hard cut by world-space liquid surface.
                clip(sloshY - i.positionWS.y);

                float span = max(0.0001, _DepthSpanWS);
                float depth01 = saturate((sloshY - i.positionWS.y) / span);
                float3 col = lerp(_LiquidColor.rgb, _DeepColor.rgb, depth01);

                float distToSurface = abs(sloshY - i.positionWS.y);
                float foam = 1.0 - saturate(distToSurface / max(0.0005, _FoamWidth));
                col = lerp(col, _FoamColor.rgb, foam);

                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}
