/*
 * @author: wizardc
 */

/// GPUSkinning 动画，无光照

Shader "Dou/GPUSkinning/Unlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _MatrixTex ("Animation Data Matrix Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // 开启 GPU Instancing
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"
            // 包含 GPUSkinning 基础代码
            #include "Assets/GPUSkinningAndInstance/Shader/GPUSkinningBase.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 uv2 : TEXCOORD1;
                float4 uv3 : TEXCOORD2;
                // 这里定义了让顶点数据接收 Instancing 块中的当前 id 的功能
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _MatrixTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                // 这里会从顶点数据中提取 instanceid，后面就可以正常获取 instancing 中的数据时
                UNITY_SETUP_INSTANCE_ID(v);

                v2f o;

                // 骨骼和蒙皮动画实现
                float4 pos = skin4(v.vertex, v.uv2, v.uv3, _MatrixTex);

                // 通过 MPV 矩阵将模型空间的 pos 转换到裁剪空间
                o.vertex = UnityObjectToClipPos(pos);
                // 为 uv 添加 Tiling、Offset 2个变量
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                return col;
            }
            ENDCG
        }
    }
}
