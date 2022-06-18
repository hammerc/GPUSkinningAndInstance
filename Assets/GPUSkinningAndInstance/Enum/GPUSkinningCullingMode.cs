/*
 * @author: wizardc
 */

namespace Dou.GPUSkinning
{
    /// <summary>
    /// GPUSkinning 剔除模式
    /// </summary>
    public enum GPUSkinningCullingMode
    {
        /// <summary>
        /// 即使摄像机看不见也要进行动画播放的更新
        /// </summary>
        AlwaysAnimate,

        /// <summary>
        /// 摄像机看不见时停止动画播放但是位置会继续更新
        /// </summary>
        CullUpdateTransforms,

        /// <summary>
        /// 摄像机看不见时停止动画的所有更新
        /// </summary>
        CullCompletely
    }
}
