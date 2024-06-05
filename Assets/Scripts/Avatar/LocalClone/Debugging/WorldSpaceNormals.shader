Shader "Unlit/WorldSpaceNormals"
{
    Properties
    {
        [IntRange] _DebugMode ("Debug display", Range (0, 3)) = 1
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float4 color : COLOR;
                uint vertexID : SV_VertexID;
            };

            struct v2f {
                half3 worldNormal : TEXCOORD0;
                float vertexID : TEXCOORD1;
                float4 tangent : TEXCOORD2;
                float4 color : TEXCOORD3;
                float4 pos : SV_POSITION;
            };

            float _DebugMode;

            v2f vert (appdata i)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(i.vertex);
                o.worldNormal = UnityObjectToWorldNormal(i.normal);
                o.tangent = i.tangent;
                o.vertexID = i.vertexID;
                o.color = i.color;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                switch(_DebugMode){
                    case(0):
                    return float4(i.worldNormal,1);
                    case(1):
                    return i.tangent;
                    case(2):
                    return sin(i.vertexID/10.0);
                    case(3):
                    return i.color;
                }
                return 0;
            }
            ENDCG
        }
    }
}