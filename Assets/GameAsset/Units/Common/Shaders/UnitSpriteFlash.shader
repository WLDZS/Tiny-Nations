Shader "Game/Units/Sprite Flash"
{
    Properties
    {
        _MainTex("Sprite Texture", 2D) = "white" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        // SpriteRenderer legacy properties are retained for its internal data path.
        _Color("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] PixelSnap("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor("Renderer Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0

        _FlashColor("Flash Color", Color) = (1, 1, 1, 1)
        _FlashAmount("Flash Amount", Range(0, 1)) = 0
        _SelectionOutline("Selection Outline", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite [_ZWrite]
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex SpriteFlashVertex
            #pragma fragment SpriteFlashFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _FlashColor;
                half _FlashAmount;
                half _SelectionOutline;
            CBUFFER_END

            float4 _MainTex_TexelSize;

            Varyings SpriteFlashVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half4 SpriteFlashFragment(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                color.rgb = lerp(color.rgb, _FlashColor.rgb, saturate(_FlashAmount));

                if (_SelectionOutline > 0.5h && color.a > 0.05h)
                {
                    float2 pixel = _MainTex_TexelSize.xy;
                    half leftAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                        input.uv + float2(-pixel.x, 0)).a;
                    half rightAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                        input.uv + float2(pixel.x, 0)).a;
                    half bottomAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                        input.uv + float2(0, -pixel.y)).a;
                    half topAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                        input.uv + float2(0, pixel.y)).a;
                    if (min(min(leftAlpha, rightAlpha), min(bottomAlpha, topAlpha)) < 0.05h)
                        color.rgb = half3(1, 1, 1);
                }

                return color;
            }
            ENDHLSL
        }
    }
}
