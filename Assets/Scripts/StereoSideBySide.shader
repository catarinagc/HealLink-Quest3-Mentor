Shader "Custom/StereoSideBySide"
{
    Properties
    {
        _MainTex ("Side-by-side texture (L|R)", 2D) = "white" {}
        _FlipY ("Flip Y (0/1)", Float) = 0
        _Ipd ("Receiver IPD (m)", Float) = 0.063
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Cull Off
        Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float _FlipY;
            float _Ipd;

            //old code, with the billboard
  //          v2f vert (appdata v)
    //        {
      //          v2f o;
        //        UNITY_SETUP_INSTANCE_ID(v);
          //      UNITY_INITIALIZE_OUTPUT(v2f, o);
            //    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
              //  o.pos = UnityObjectToClipPos(v.vertex);
                //o.uv = v.uv;
                //return o;
            //}
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 camRight = normalize(UNITY_MATRIX_V[0].xyz);
                wp += camRight * (unity_StereoEyeIndex - 0.5) * _Ipd;   // left eye -ipd/2, right +ipd/2

                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1));
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float eye = unity_StereoEyeIndex;      // 0 = left eye, 1 = right eye
                float2 uv = i.uv;
                if (_FlipY > 0.5) uv.y = 1.0 - uv.y;
                uv.x = uv.x * 0.5 + eye * 0.5;         // pick the left or right half
                //return tex2D(_MainTex, uv);
                fixed4 col = tex2D(_MainTex, uv);
                float2 c = i.uv - 0.5;
                float edge = smoothstep(0.5, 0.38, max(abs(c.x), abs(c.y)));
                return fixed4(col.rgb * edge, 1);
            }
            ENDCG
        }
    }
}