Shader "Custom/GrassLit"
{
    Properties
    {
        _BaseColorTop("Top Color", Color) = (0.4, 0.8, 0.3, 1)
        _BaseColorBottom("Bottom Color", Color) = (0.1, 0.4, 0.1, 1)
        _WindStrength("Wind Strength", Range(0, 1)) = 0.2
        _WindSpeed("Wind Speed", Range(0.1, 5)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 300
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 4.5

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            StructuredBuffer<float4x4> _TransformMatrices;

            float4 _BaseColorTop;
            float4 _BaseColorBottom;
            float _WindStrength;
            float _WindSpeed;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 colorLerp : COLOR;
            };

            // Simple hash for random variation per instance
            float Hash(uint n)
            {
                n = (n << 13U) ^ n;
                return (1.0 - ((n * (n * n * 15731U + 789221U) + 1376312589U) & 0x7fffffffU) / 1073741824.0);
            }

            float3 RotateAroundAxis(float3 v, float3 axis, float angle)
            {
                axis = normalize(axis);
                float cosA = cos(angle);
                float sinA = sin(angle);
                return v * cosA + cross(axis, v) * sinA + axis * dot(axis, v) * (1 - cosA);
            }

            v2f vert(appdata v, uint instanceID : SV_InstanceID)
            {
                v2f o;
                float4x4 mat = _TransformMatrices[instanceID];
                float4 worldPos = mul(mat, v.vertex);
                worldPos.y -= 9.9;

                // Compute world position
                float3x3 normalMat = (float3x3)mat;
                float3 worldNormal = normalize(mul(normalMat, v.normal));

                o.worldPos = worldPos.xyz;
                o.worldNormal = normalize(mul((float3x3)mat, v.normal));

                float angle1 = 15.0 * 3.14159 / 180.0; // 15 degrees in radians
                float angle2 = -10.0 * 3.14159 / 180.0; // -10 degrees in radians

                float3 axis = float3(0, 1, 0); // bend around Y axis (upward)
                float3 normalA = RotateAroundAxis(worldNormal, axis, angle1);
                float3 normalB = RotateAroundAxis(worldNormal, axis, angle2);

                // blend factor: can use vertex height for more bend at tip
                float blendFactor = saturate(v.vertex.y);
                float3 finalNormal = normalize(lerp(normalA, normalB, blendFactor));

                o.worldNormal = finalNormal;


                // Gradient based on vertex Y height within blade
                float heightFactor = saturate(v.vertex.y); // assumes blade mesh Y=0 bottom, Y=1 top
                float3 baseColor = lerp(_BaseColorBottom.rgb, _BaseColorTop.rgb, heightFactor);
                float ambientOcclusion = lerp(0.6, 1.0, heightFactor); 
                o.colorLerp = baseColor * ambientOcclusion; // multiply here


                float sway = sin((_Time.y * _WindSpeed + worldPos.x + worldPos.z) * 1.3) * _WindStrength;
                worldPos.xz += float2(sway, sway * 0.5) * heightFactor;

                o.pos = UnityObjectToClipPos(worldPos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
{
    float3 normalDir = normalize(i.worldNormal);
    float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);

    // Start with ambient + main directional
    float3 col = 0;
    float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
    float diff = saturate(dot(normalDir, lightDir));
    col += i.colorLerp * (_LightColor0.rgb * diff + 0.2);

    // ----- Add per-pixel point/spot lights -----
    #ifdef _ADDITIONAL_LIGHTS
    for (int lightIndex = 0; lightIndex < unity_LightIndicesOffsetAndCount.y; lightIndex++)
    {
        int index = unity_LightIndices[unity_LightIndicesOffsetAndCount.x + lightIndex];
        Light light = unity_Lights[index];

        float3 lightDir = normalize(light.position.xyz - i.worldPos);
        float distanceAtten = 1.0 / (1.0 + light.range * length(lightDir));
        float diff2 = saturate(dot(normalDir, lightDir));
        col += i.colorLerp * (light.color.rgb * diff2 * distanceAtten);
    }
    #endif

    return float4(col, 1.0);
}
            ENDHLSL
        }
    }
}
