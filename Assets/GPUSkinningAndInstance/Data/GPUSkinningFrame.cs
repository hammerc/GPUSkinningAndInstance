/*
 * @author: wizardc
 */

using UnityEngine;

namespace Dou.GPUSkinning
{
    /// <summary>
    /// GPUSkinning 帧数据
    /// </summary>
    [System.Serializable]
    public class GPUSkinningFrame
    {
        /// <summary>
        /// 记录所有骨骼的矩阵信息
        /// </summary>
        [System.NonSerialized]
        public Matrix4x4[] matrices = null;

        /// <summary>
        /// 根节点移动，前进方向
        /// </summary>
        public Quaternion rootMotionDeltaPositionQ;

        /// <summary>
        /// 根节点移动，前进距离
        /// </summary>
        public float rootMotionDeltaPositionL;

        /// <summary>
        /// 根节点移动，旋转
        /// </summary>
        public Quaternion rootMotionDeltaRotation;

        // 根节点的逆矩阵是否已初始化
        [System.NonSerialized]
        private bool rootMotionInvInit = false;

        // 根节点的逆矩阵
        [System.NonSerialized]
        private Matrix4x4 rootMotionInv;

        /// <summary>
        /// 初始化根节点的逆矩阵
        /// </summary>
        public Matrix4x4 RootMotionInv(int rootBoneIndex)
        {
            if (!rootMotionInvInit)
            {
                rootMotionInv = matrices[rootBoneIndex].inverse;
                rootMotionInvInit = true;
            }
            return rootMotionInv;
        }
    }
}
