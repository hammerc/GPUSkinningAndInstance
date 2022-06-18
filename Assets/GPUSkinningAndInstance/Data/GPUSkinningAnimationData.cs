/*
 * @author: wizardc
 */

using System.Collections.Generic;
using UnityEngine;

namespace Dou.GPUSkinning
{
    /// <summary>
    /// GPUSkinning 动画数据
    /// </summary>
    public class GPUSkinningAnimationData : ScriptableObject
    {
        /// <summary>
        /// 所有骨骼数据列表
        /// </summary>
        public List<GPUSkinningBone> bones = null;

        /// <summary>
        /// 所有动画剪辑数据列表
        /// </summary>
        public List<GPUSkinningClip> clips = null;

        /// <summary>
        /// 生成的贴图宽度
        /// </summary>
        public int textureWidth = 0;

        /// <summary>
        /// 生成的贴图高度
        /// </summary>
        public int textureHeight = 0;

        /// <summary>
        /// 根骨骼索引
        /// </summary>
        public int rootBoneIndex = 0;

        /// <summary>
        /// 根节点矩阵
        /// </summary>
        public Matrix4x4 rootTransformMatrix;
    }
}
