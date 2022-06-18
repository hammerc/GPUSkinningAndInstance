/*
* @author: wizardc
*/

/// GPUSkinning 动画核心实现

#ifndef GPU_SKIN_BASE
#define GPU_SKIN_BASE

// 记录贴图尺寸，和每帧的像素数量（每一帧都会记录所有骨骼的矩阵信息，一个 float3x4 的矩阵数据需要 6 个像素点来记录，所以这里是骨骼数量 * 6）
float3 _GPUSkin_TextureSize_NumPixelsPerFrame;

// START 和 END 表示定义了一个 Instancing 块，下面的数据是块中每一个项目的数据结构
// 定义名为 GPUSkinProperties 的数据块
UNITY_INSTANCING_BUFFER_START(GPUSkinProperties)
// 动画数据 x: 当前动画剪辑中播放到的帧索引 y: 当前动画剪辑的骨骼数据位于贴图的开始索引
UNITY_DEFINE_INSTANCED_PROP(float2, _GPUSkin_FrameIndex_PixelSegmentation)
// 动画混合数据  x: 上一个动画剪辑播放到的帧 y: 上一个动画剪辑的骨骼数据位于贴图的开始索引 z: 上一个动画和当前动画混合比率
UNITY_DEFINE_INSTANCED_PROP(float3, _GPUSkin_FrameIndex_PixelSegmentation_Blend_CrossFade)
// 根运动位移旋转矩阵
UNITY_DEFINE_INSTANCED_PROP(float4x4, _GPUSkin_RootMotion)
UNITY_INSTANCING_BUFFER_END(GPUSkinProperties)

// UNITY_ACCESS_INSTANCED_PROP 从 instancing 块中获取对应的数据
// 获取动画数据
#define AnimateData UNITY_ACCESS_INSTANCED_PROP(GPUSkinProperties, _GPUSkin_FrameIndex_PixelSegmentation)
// 获取上一个动画数据
#define LastAnimateData UNITY_ACCESS_INSTANCED_PROP(GPUSkinProperties, _GPUSkin_FrameIndex_PixelSegmentation_Blend_CrossFade)
// 获取当前帧的位移旋转矩阵
#define RootMotion UNITY_ACCESS_INSTANCED_PROP(GPUSkinProperties, _GPUSkin_RootMotion)

// 2 个动画动作混合的顶点算法
#define skin_blend(pos0, pos1, crossFadeBlend) pos1.xyz + (pos0.xyz - pos1.xyz) * crossFadeBlend

// 根据当前帧获取动画贴图骨骼数据的起始索引
inline float getFrameStartIndex()
{
    float2 frameIndex_segment = AnimateData;
    float segment = frameIndex_segment.y;
    float frameIndex = frameIndex_segment.x;
    // 动画剪辑数据开始索引 + 当前播放的动画帧 * 每帧需要的像素数量
    float frameStartIndex = segment + frameIndex * _GPUSkin_TextureSize_NumPixelsPerFrame.z;
    return frameStartIndex;
}

// 根据当前帧获取动画贴图骨骼数据的起始索引，用于获取上一个动画的起始索引，用于动画混合
inline float getFrameStartIndex_crossFade()
{
    float3 frameIndex_segment = LastAnimateData;
    float segment = frameIndex_segment.y;
    float frameIndex = frameIndex_segment.x;
    // 动画剪辑数据开始索引 + 当前播放的动画帧 * 每帧需要的像素数量
    float frameStartIndex = segment + frameIndex * _GPUSkin_TextureSize_NumPixelsPerFrame.z;
    return frameStartIndex;
}

// 从 16 位的颜色值中还原出 32 位的 float 值，只保留 16 位有效数据
float decode2half(float2 c) {
    float high = c.x * 255;
    float sign = floor(high / 128);
    high -= sign * 128;
    sign = 1 - 2 * sign;
    float exp = floor(high / 4) - 15;
    float mantissa = c.y * 255 + high % 2 * 256 + floor(high / 2 % 2) * 512;
    return sign * pow(2, exp) * (1 + mantissa / 1024);
}

// 从一个 32 位的颜色值中还原出 2 个 32 位的 float 值，只保留 16 位有效数据
float2 rgbaToFloat2(float4 rgba) {
    return float2(decode2half(rgba.xy), decode2half(rgba.zw));
}

// 像素索引转为采用的 uv 值
inline float4 indexToUV(float index)
{
    float width = _GPUSkin_TextureSize_NumPixelsPerFrame.x;
    float height = _GPUSkin_TextureSize_NumPixelsPerFrame.y;

    int row = (int)(index / width);
    float col = index - row * width;
    return float4(col / width, row / height, 0, 0);
}

// 从动画贴图中获取指定索引的骨骼矩阵
inline float4x4 getMatrix(int frameStartIndex, float boneIndex, sampler2D matrixTexture)
{
    // 当前的动画剪辑帧索引 + 骨骼索引 * 6（一个骨骼数据 float3x4 的矩阵数据需要 6 个像素点来记录）
    float matStartIndex = frameStartIndex + boneIndex * 6;

    float2 r0 = rgbaToFloat2(tex2Dlod(matrixTexture, indexToUV(matStartIndex + 0)).xyzw);
    float2 r1 = rgbaToFloat2(tex2Dlod(matrixTexture, indexToUV(matStartIndex + 1)).xyzw);

    float2 r2 = rgbaToFloat2(tex2Dlod(matrixTexture, indexToUV(matStartIndex + 2)).xyzw);
    float2 r3 = rgbaToFloat2(tex2Dlod(matrixTexture, indexToUV(matStartIndex + 3)).xyzw);

    float2 r4 = rgbaToFloat2(tex2Dlod(matrixTexture, indexToUV(matStartIndex + 4)).xyzw);
    float2 r5 = rgbaToFloat2(tex2Dlod(matrixTexture, indexToUV(matStartIndex + 5)).xyzw);

    float4x4 mat = float4x4(float4(r0, r1), float4(r2, r3), float4(r4, r5), float4(0, 0, 0, 1));
    return mat;
}

/// GPU 骨骼和蒙皮实现
///@param vertex 顶点数据
///@param uv2 记录 2 个骨骼索引和权重数据
///@param uv3 记录 2 个骨骼索引和权重数据
inline float4 skin4(float4 vertex, float4 uv2, float4 uv3, sampler2D matrixTexture)
{
    // 获取 4 个骨骼顶点的 bindpose 矩阵信息
    float frameStartIndex = getFrameStartIndex();
    float4x4 mat0 = getMatrix(frameStartIndex, uv2.x, matrixTexture);
    float4x4 mat1 = getMatrix(frameStartIndex, uv2.z, matrixTexture);
    float4x4 mat2 = getMatrix(frameStartIndex, uv3.x, matrixTexture);
    float4x4 mat3 = getMatrix(frameStartIndex, uv3.z, matrixTexture);

    // 获取 RootMotion 移动信息
    float4x4 root = RootMotion;

    // 算出最终的顶点受 4 个骨骼影响后的最终位置信息
    // 先根据每个骨骼对顶点的影响算出顶点的偏移值（矩阵相乘）
    // 每个骨骼的影响相加后就是顶点在动画中的最终位置（矩阵相加）
    float4 pos = mul(root, mul(mat0, vertex)) * uv2.y
               + mul(root, mul(mat1, vertex)) * uv2.w
               + mul(root, mul(mat2, vertex)) * uv3.y
               + mul(root, mul(mat3, vertex)) * uv3.w;

    // 动画切换时实现动画混合
    float crossFadeBlend = LastAnimateData.z;
    if (crossFadeBlend < 1)
    {
        // 上一个动画的顶点位置信息
        float frameStartIndex_crossFade = getFrameStartIndex_crossFade();
        float4x4 mat0_crossFade = getMatrix(frameStartIndex_crossFade, uv2.x, matrixTexture);
        float4x4 mat1_crossFade = getMatrix(frameStartIndex_crossFade, uv2.z, matrixTexture);
        float4x4 mat2_crossFade = getMatrix(frameStartIndex_crossFade, uv3.x, matrixTexture);
        float4x4 mat3_crossFade = getMatrix(frameStartIndex_crossFade, uv3.z, matrixTexture);

        // 算出最终的顶点受 4 个骨骼影响后的最终位置信息
        float4 pos1 = mul(mat0_crossFade, vertex) * uv2.y
                    + mul(mat1_crossFade, vertex) * uv2.w
                    + mul(mat2_crossFade, vertex) * uv3.y
                    + mul(mat3_crossFade, vertex) * uv3.w;

        // 通过线性插值得到动画混合后的顶点位置信息
        pos = float4(skin_blend(pos, pos1, crossFadeBlend), 1);
    }

    return pos;
}

#endif
