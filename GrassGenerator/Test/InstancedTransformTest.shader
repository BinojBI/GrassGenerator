Shader "Hidden/InstancedTransformTest"
{
    SubShader
    {
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"

            StructuredBuffer<float4x4> _TransformMatrices;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : NORMAL;
            };

            v2f vert (appdata v, uint instanceID : SV_InstanceID)
            {
                v2f o;
                float4x4 mat = _TransformMatrices[instanceID];
                float4 worldPos = mul(mat, v.vertex); // transform vertex by instance matrix
                o.pos = UnityObjectToClipPos(worldPos); // transform to clip space using Unity helper
                // If you want lighting: transform normal by mat (ignore scale for test)
                float3x3 mat3 = (float3x3)mat;
                o.normal = mul(mat3, v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(0.2, 0.8, 0.2, 1); // simple green
            }
            ENDHLSL
        }
    }
}
