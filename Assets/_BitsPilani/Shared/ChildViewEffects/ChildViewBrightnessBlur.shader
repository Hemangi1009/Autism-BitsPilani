Shader "BitsPilani/Child View Brightness Blur" {
    Properties { _Pulse("Exposure EV, white wash, blur pixels",Vector)=(0,0,0,0) _FlashColor("Flash Color",Color)=(1,1,1,1) }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always
        Pass {
            Name "ChildViewPulse"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _Pulse;
            float4 _ChildViewTexelSize;
            half4 _FlashColor;
            half4 ReadScene(float2 uv){return SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,saturate(uv));}
            half4 Frag(Varyings input):SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=input.texcoord;half4 color=ReadScene(uv);
                if(_Pulse.z>.001){
                    float2 d=_ChildViewTexelSize.xy*_Pulse.z;
                    color=ReadScene(uv)*.25;
                    color+=(ReadScene(uv+float2(d.x,0))+ReadScene(uv-float2(d.x,0))+ReadScene(uv+float2(0,d.y))+ReadScene(uv-float2(0,d.y)))*.125;
                    color+=(ReadScene(uv+d)+ReadScene(uv-d)+ReadScene(uv+float2(d.x,-d.y))+ReadScene(uv+float2(-d.x,d.y)))*.0625;
                }
                color.rgb*=exp2(_Pulse.x);color.rgb=lerp(color.rgb,_FlashColor.rgb,saturate(_Pulse.y));return color;
            }
            ENDHLSL
        }
    }
}
