Shader "Universal Render Pipeline/Custom/BasicWaterShader_URP"
{
    Properties
    {
        _Color ("Background Color", Color) = (0.1, 0.4, 0.8, 0.8)
        _TextureColor ("Texture Color", Color) = (1, 1, 1, 1)
        _MainTex ("Water Texture", 2D) = "white" {}

        _WaveSpeed ("Wave Speed", Float) = 0.5
        _WaveStrength ("Wave Strength", Range(0, 0.1)) = 0.01
        _WaveAmount ("Wave Amount", Float) = 0.1
        _WaveFrequency ("Wave Frequency", Float) = 1
        _TextureDistortion ("Texture Distortion", Range(0, 1)) = 0.5
        _CartoonFactor ("Cartoon Factor", Range(0, 1)) = 0.5

        _TransparencySpeed ("Transparency Animation Speed", Float) = 1.0
        _TransparencyStrength ("Transparency Strength", Range(0, 1)) = 0.5

        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _FoamAmount ("Foam Amount", Range(0, 1)) = 0.1
        _FoamCutoff ("Foam Cutoff", Range(0, 1)) = 0.5
        _FoamSpeed ("Foam Speed", Float) = 0.1
        _FoamNoiseScale ("Foam Noise Scale", Float) = 20
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalRenderPipeline"
            "IgnoreProjector"="True"
        }

        LOD 100

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 worldPos   : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
                float3 normalWS   : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _TextureColor;
                float4 _FoamColor;

                float _WaveSpeed;
                float _WaveStrength;
                float _WaveAmount;
                float _WaveFrequency;
                float _TextureDistortion;
                float _CartoonFactor;

                float _TransparencySpeed;
                float _TransparencyStrength;

                float _FoamAmount;
                float _FoamCutoff;
                float _FoamSpeed;
                float _FoamNoiseScale;

                float4 _MainTex_ST;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // ---- Vertex ----

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldPos = worldPos;

                OUT.uv = IN.uv * _MainTex_ST.xy + _MainTex_ST.zw;

                OUT.positionCS = TransformWorldToHClip(worldPos);
                OUT.screenPos = ComputeScreenPos(OUT.positionCS);

                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);

                return OUT;
            }

            // ---- Noise helpers (same as your original) ----

            float2 random2(float2 st)
            {
                st = float2(
                    dot(st, float2(127.1, 311.7)),
                    dot(st, float2(269.5, 183.3))
                );
                return -1.0 + 2.0 * frac(sin(st) * 43758.5453123);
            }

            float gradientNoise(float2 st)
            {
                float2 i = floor(st);
                float2 f = frac(st);

                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(dot(random2(i + float2(0.0, 0.0)), f - float2(0.0, 0.0)),
                         dot(random2(i + float2(1.0, 0.0)), f - float2(1.0, 0.0)), u.x),
                    lerp(dot(random2(i + float2(0.0, 1.0)), f - float2(0.0, 1.0)),
                         dot(random2(i + float2(1.0, 1.0)), f - float2(1.0, 1.0)), u.x),
                    u.y
                );
            }

            // ---- Fragment ----

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float2 uv = IN.uv;

                // Waves
                float2 waveOffset = float2(
                    gradientNoise(uv * _WaveFrequency + _Time.y * _WaveSpeed),
                    gradientNoise(uv * _WaveFrequency * 1.2 + _Time.y * _WaveSpeed * 1.1)
                ) * _WaveAmount;

                float2 distortedUV = uv + waveOffset * _WaveStrength * _TextureDistortion;

                // Texture with explicit derivatives (equivalent of tex2D with ddx/ddy)
                float2 du = ddx(uv);
                float2 dv = ddy(uv);
                float4 c = SAMPLE_TEXTURE2D_GRAD(_MainTex, sampler_MainTex, distortedUV, du, dv);

                // Apply texture tint
                c *= _TextureColor;

                // Pulsating transparency (texture part)
                float transparencyPulse = (sin(_Time.y * _TransparencySpeed) + 1.0) * 0.5;
                float textureTransparency = lerp(1.0, transparencyPulse, _TransparencyStrength);

                // Foam noise in world space
                float2 foamUV = IN.worldPos.xz * _FoamNoiseScale + _Time.y * _FoamSpeed;
                float foamNoise = gradientNoise(foamUV);

                // Scene depth (requires Depth Texture in URP settings)
                float2 depthUV = GetNormalizedScreenSpaceUV(IN.screenPos);
                float rawDepth = SampleSceneDepth(depthUV);
                float depthEye = LinearEyeDepth(rawDepth, _ZBufferParams);

                float depthSurface01 = IN.positionCS.z / IN.positionCS.w;
                float surfaceEye = LinearEyeDepth(depthSurface01, _ZBufferParams);

                // Foam line near intersection with geometry
                float foamLine = 1.0 - saturate(_FoamAmount * (depthEye - surfaceEye));

                float foam = saturate(foamNoise + foamLine);
                foam = smoothstep(_FoamCutoff, 1.0, foam);

                // Base/background vs texture
                float3 finalColor = lerp(_Color.rgb, c.rgb, c.a * textureTransparency);
                // finalColor = lerp(finalColor, _FoamColor.rgb, foam);

                float alpha = lerp(_Color.a, c.a * _TextureColor.a, c.a * textureTransparency);

                // Unlit output (if you want lighting, we can extend this)
                return half4(finalColor, alpha);
            }

            ENDHLSL
        }
    }

    FallBack Off
}
