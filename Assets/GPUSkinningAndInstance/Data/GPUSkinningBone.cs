/*
 * @author: wizardc
 */

using UnityEngine;

namespace Dou.GPUSkinning
{
    /// <summary>
    /// GPUSkinning 骨骼
    /// </summary>
    [System.Serializable]
    public class GPUSkinningBone
    {
        /// <summary>
        /// 骨骼的变换对象
        /// </summary>
        [System.NonSerialized]
        public Transform transform = null;

        /// <summary>
        /// 骨骼名称
        /// </summary>
        public string name = null;

        /// <summary>
        /// 将顶点由 mesh 的局部空间变换到 bone 的局部空间的矩阵
        /// </summary>
        public Matrix4x4 bindpose;

        /// <summary>
        /// 父骨骼的索引, -1 表示根骨骼
        /// </summary>
        public int parentBoneIndex = -1;

        /// <summary>
        /// 子骨骼的索引列表
        /// </summary>
        public int[] childrenBonesIndices = null;
    }
}
