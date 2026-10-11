// Vertical two-colour gradient skybox for the ink-wash battle backdrop.
Shader "Game/InkSkyGradient"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.910, 0.894, 0.851, 1)
        _HorizonColor ("Horizon Color", Color) = (0.949, 0.871, 0.769, 1)
        _Exponent ("Blend Exponent", Range(0.1, 4)) = 0.6
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half _Exponent;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Below the horizon stays at the horizon colour, like blank paper under the mist.
                half y = saturate(normalize(input.direction).y);
                half t = pow(y, _Exponent);
                return lerp(_HorizonColor, _TopColor, t);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
