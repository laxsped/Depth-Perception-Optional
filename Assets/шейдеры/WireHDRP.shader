Shader "Custom/HDRP/ProceduralWire"
{
    Properties
    {
        [HDR] _WireColor    ("Wire Color",      Color)  = (0.08, 0.08, 0.08, 1)
        _WireThickness      ("Wire Thickness",  Float)  = 0.025
        _Sag                ("Sag (0-1)",       Float)  = 0.25
        _SwayAmount         ("Sway Amount",     Float)  = 0.018
        _SwaySpeed          ("Sway Speed",      Float)  = 1.2
        _SwayFreq           ("Sway Frequency",  Float)  = 2.8
        _AAWidth            ("AA Edge Width",   Float)  = 0.006
    }

    SubShader
    {
        // ── HDRP теги ──────────────────────────────────────────────────────────
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
        }

        // ── Глубина (нужна HDRP для корректной сортировки прозрачных объектов) ─
        Pass
        {
            Name "DepthForwardOnly"
            Tags { "LightMode" = "DepthForwardOnly" }

            ZWrite On
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert_depth
            #pragma fragment frag_depth

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            struct AttrD { float3 posOS : POSITION; float2 uv : TEXCOORD0; };
            struct VaryD { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; };

            float _WireThickness, _Sag, _SwayAmount, _SwaySpeed, _SwayFreq;

            VaryD vert_depth(AttrD i)
            {
                VaryD o;
                o.posCS = TransformWorldToHClip(TransformObjectToWorld(i.posOS));
                o.uv    = i.uv;
                return o;
            }

            void frag_depth(VaryD i)
            {
                float u      = i.uv.x;
                float wireV  = 1.0 - _Sag * 4.0 * u * (1.0 - u)
                             + _SwayAmount * sin(_Time.y * _SwaySpeed + u * _SwayFreq);
                float dist   = abs(i.uv.y - wireV);
                if (dist > _WireThickness) discard;
            }
            ENDHLSL
        }

        // ── Основной Unlit Forward Pass ────────────────────────────────────────
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            float4 _WireColor;
            float  _WireThickness;
            float  _Sag;
            float  _SwayAmount;
            float  _SwaySpeed;
            float  _SwayFreq;
            float  _AAWidth;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformWorldToHClip(TransformObjectToWorld(i.positionOS));
                o.uv = i.uv;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float u = i.uv.x;
                float v = i.uv.y;

                // ── Кривая провода ─────────────────────────────────────────────
                // Параболический провис: верхний-левый → верхний-правый, прогиб вниз
                float sway  = _SwayAmount * sin(_Time.y * _SwaySpeed + u * _SwayFreq);
                float wireV = 1.0 - _Sag * 4.0 * u * (1.0 - u) + sway;

                // ── Расстояние и AA ────────────────────────────────────────────
                float dist  = abs(v - wireV);
                float half  = _WireThickness * 0.5;
                float alpha = 1.0 - smoothstep(half - _AAWidth, half, dist);

                // ── Небольшой градиент толщины по U (визуальная глубина) ────────
                // Делает концы чуть тоньше — как настоящий провод в перспективе
                float taper = 1.0 - 0.25 * (1.0 - sin(u * 3.14159));
                alpha *= taper;

                clip(alpha - 0.001);

                return float4(_WireColor.rgb, _WireColor.a * alpha);
            }
            ENDHLSL
        }
    }

    // Fallback — на случай если HDRP не найден (например в Editor без пакета)
    FallBack "Hidden/InternalErrorShader"
}
