/*
 * @author: wizardc
 */

namespace Dou.GPUSkinning
{
    /// <summary>
    /// GPUSkinning 动画剪辑
    /// </summary>
    [System.Serializable]
    public class GPUSkinningClip
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string name = null;

        /// <summary>
        /// 时长
        /// </summary>
        public float length = 0.0f;

        /// <summary>
        /// 帧率
        /// </summary>
        public int frameRate = 0;

        /// <summary>
        /// 循环模式
        /// </summary>
        public GPUSkinningWrapMode wrapMode = GPUSkinningWrapMode.Once;

        /// <summary>
        /// 帧数据列表
        /// </summary>
        public GPUSkinningFrame[] frames = null;

        /// <summary>
        /// 当前动画剪辑位于贴图上的第一个像素索引
        /// </summary>
        public int pixelSegmentation = 0;

        /// <summary>
        /// 根运动是否开启
        /// </summary>
        public bool rootMotionEnabled = false;
    }
}
