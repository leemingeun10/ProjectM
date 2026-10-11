// Flat unlit colour (optionally textured) that still honours RenderSettings fog, so distant
// backdrop elements sink into the mist.
Shader "Game/InkUnlitFog"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.361, 0.400, 0.439, 1)
        _BaseMap ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                return half4(MixFog(color, input.fogFactor), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
