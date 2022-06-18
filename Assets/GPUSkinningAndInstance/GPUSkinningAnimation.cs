/*
 * @author: wizardc
 */

using System.Collections.Generic;
using UnityEngine;

namespace Dou.GPUSkinning
{
    /// <summary>
    /// GPUSkinning 动画
    /// </summary>
    [ExecuteInEditMode]
    public class GPUSkinningAnimation : MonoBehaviour
    {
        private static bool ShaderPropID_Inited = false;
        private static int ShaderPropID_TextureSize_NumPixelsPerFrame;
        private static int ShaderPorpID_FrameIndex_PixelSegmentation;
        private static int ShaderPorpID_FrameIndex_PixelSegmentation_Blend_CrossFade;
        private static int ShaderPorpID_RootMotion;

        /// <summary>
        /// 动画数据
        /// </summary>
        public GPUSkinningAnimationData animData;

        /// <summary>
        /// 动画网格
        /// </summary>
        public Mesh mesh;

        /// <summary>
        /// 材质
        /// </summary>
        public Material material;

        /// <summary>
        /// 剔除模式
        /// </summary>
        public GPUSkinningCullingMode cullingMode = GPUSkinningCullingMode.CullUpdateTransforms;

        /// <summary>
        /// 是否开启根运动
        /// </summary>
        public bool rootMotionEnabled = false;

        private MeshRenderer _meshRender;
        private MeshFilter _meshFilter;

        private GPUSkinningClip _playingClip;
        private GPUSkinningClip _lastPlayingClip;
        private GPUSkinningClip _lastPlayedClip;

        private int _lastPlayingFrameIndex = -1;
        private bool _isPlaying = false;

        private float _time = 0;
        private float _crossFadeTime = -1;
        private float _crossFadeProgress = 0;

        private float _lastPlayedTime = 0;

        private MaterialPropertyBlock _materialPropertyBlock;

        private int _rootMotionFrameIndex = -1;
        private Vector3 _rootMotionDeltaPosition = Vector3.zero;

        /// <summary>
        /// 获取当前是否在播放
        /// </summary>
        public bool isPlaying
        {
            get
            {
                return _isPlaying;
            }
        }

        /// <summary>
        /// 获取当前循环模式
        /// </summary>
        public GPUSkinningWrapMode wrapMode
        {
            get
            {
                return _playingClip == null ? GPUSkinningWrapMode.Once : _playingClip.wrapMode;
            }
        }

        /// <summary>
        /// 获取当前是否在最后一帧
        /// </summary>
        public bool isTimeAtTheEndOfLoop
        {
            get
            {
                if (_playingClip == null)
                {
                    return false;
                }
                else
                {
                    return GetFrameIndex() == ((int)(_playingClip.length * _playingClip.frameRate) - 1);
                }
            }
        }

        private void Start()
        {
            if (!ShaderPropID_Inited)
            {
                ShaderPropID_TextureSize_NumPixelsPerFrame = Shader.PropertyToID("_GPUSkin_TextureSize_NumPixelsPerFrame");
                ShaderPorpID_FrameIndex_PixelSegmentation = Shader.PropertyToID("_GPUSkin_FrameIndex_PixelSegmentation");
                ShaderPorpID_FrameIndex_PixelSegmentation_Blend_CrossFade = Shader.PropertyToID("_GPUSkin_FrameIndex_PixelSegmentation_Blend_CrossFade");
                ShaderPorpID_RootMotion = Shader.PropertyToID("_GPUSkin_RootMotion");
                ShaderPropID_Inited = true;
            }

            StartInit();
        }

        /// <summary>
        /// 设置动画数据
        /// </summary>
        public void SetAnimData(GPUSkinningAnimationData animData, Mesh mesh, Material material)
        {
            this.animData = animData;
            this.mesh = mesh;
            this.material = material;

            StartInit();
        }

        private void StartInit()
        {
            if (animData == null || mesh == null || material == null)
            {
                return;
            }

            GameObject go = gameObject;

            _meshRender = go.GetComponent<MeshRenderer>();
            if (_meshRender == null)
            {
                _meshRender = go.AddComponent<MeshRenderer>();
            }
            if (_meshRender.sharedMaterial != material)
            {
                _meshRender.sharedMaterial = material;
            }

            _meshFilter = go.GetComponent<MeshFilter>();
            if (_meshFilter == null)
            {
                _meshFilter = go.AddComponent<MeshFilter>();
            }
            if (_meshFilter.sharedMesh != mesh)
            {
                _meshFilter.sharedMesh = mesh;
            }

            _materialPropertyBlock = new MaterialPropertyBlock();

            SetMaterialProp();
        }

        private void SetMaterialProp()
        {
            // 一个 float3x4 的矩阵数据需要 6 个像素点来记录
            _meshRender.sharedMaterial.SetVector(ShaderPropID_TextureSize_NumPixelsPerFrame,
                new Vector4(animData.textureWidth, animData.textureHeight, animData.bones.Count * 6, 0));
        }

        /// <summary>
        /// 直接播放指定名称动画
        /// </summary>
        public void Play(string clipName)
        {
            if (animData == null)
            {
                return;
            }
            List<GPUSkinningClip> clips = animData.clips;
            int numClips = clips == null ? 0 : clips.Count;
            for (int i = 0; i < numClips; ++i)
            {
                GPUSkinningClip clip = clips[i];
                if (clip.name == clipName)
                {
                    // 同一个剪辑，非循环已经播放完毕或暂停时才会继续播放
                    if (_playingClip != clip ||
                        (_playingClip != null && _playingClip.wrapMode == GPUSkinningWrapMode.Once && isTimeAtTheEndOfLoop) ||
                        (_playingClip != null && !isPlaying))
                    {
                        SetNewPlayingClip(clip);
                        _crossFadeTime = 0;
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// 插值平滑切换指定名称动画
        /// </summary>
        public void CrossFade(string clipName, float fadeLength)
        {
            if (_playingClip == null)
            {
                Play(clipName);
            }
            else
            {
                List<GPUSkinningClip> clips = animData.clips;
                int numClips = clips == null ? 0 : clips.Count;
                for (int i = 0; i < numClips; ++i)
                {
                    GPUSkinningClip clip = clips[i];
                    if (clip.name == clipName)
                    {
                        // 切换动画剪辑时
                        if (_playingClip != clip)
                        {
                            // 记录平滑插值时间
                            _crossFadeProgress = 0;
                            _crossFadeTime = fadeLength;
                            SetNewPlayingClip(clip);
                            return;
                        }
                        // 同一个剪辑，非循环已经播放完毕或暂停时才会继续播放
                        if ((_playingClip.wrapMode == GPUSkinningWrapMode.Once && isTimeAtTheEndOfLoop) || !isPlaying)
                        {
                            SetNewPlayingClip(clip);
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 停止动画播放
        /// </summary>
        public void Stop()
        {
            _isPlaying = false;
        }

        /// <summary>
        /// 重新开始动画播放
        /// </summary>
        public void Resume()
        {
            if (_playingClip != null)
            {
                _isPlaying = true;
            }
        }

        /// <summary>
        /// 设置当前播放的动画剪辑
        /// </summary>
        private void SetNewPlayingClip(GPUSkinningClip clip)
        {
            _lastPlayedClip = _playingClip;
            _lastPlayedTime = GetCurrentTime();

            _isPlaying = true;
            _playingClip = clip;
            _time = 0;
            _rootMotionFrameIndex = -1;

            _rootMotionDeltaPosition = Vector3.zero;
        }

        /// <summary>
        /// 根据时间获取对应的帧数
        /// </summary>
        private int GetFrameIndex()
        {
            float time = GetCurrentTime();
            // 当前时间等于剪辑时长时需要单独处理，因为取余是 0
            if (_playingClip.length == time)
            {
                return GetTheLastFrameIndex(_playingClip);
            }
            return GetFrameIndex(_playingClip, time);
        }

        private float GetCurrentTime()
        {
            return _time;
        }

        private int GetTheLastFrameIndex(GPUSkinningClip clip)
        {
            return (int)(clip.length * clip.frameRate) - 1;
        }

        private int GetFrameIndex(GPUSkinningClip clip, float time)
        {
            return (int)(time * clip.frameRate) % (int)(clip.length * clip.frameRate);
        }

        private void Update()
        {
            if (!isPlaying || _playingClip == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;

            UpdateMaterial(deltaTime);

            if (_playingClip.wrapMode == GPUSkinningWrapMode.Loop)
            {
                _time += deltaTime;
            }
            else if (_playingClip.wrapMode == GPUSkinningWrapMode.Once)
            {
                if (_time >= _playingClip.length)
                {
                    _time = _playingClip.length;
                }
                else
                {
                    _time = Mathf.Clamp(_time + deltaTime, 0, _playingClip.length);
                }
            }

            _crossFadeProgress += deltaTime;
            _lastPlayedTime += deltaTime;
        }

        private void UpdateMaterial(float deltaTime)
        {
            // 剪辑和帧数不变时不执行
            int frameIndex = GetFrameIndex();
            if (_lastPlayingClip == _playingClip && _lastPlayingFrameIndex == frameIndex)
            {
                return;
            }

            _lastPlayingClip = _playingClip;
            _lastPlayingFrameIndex = frameIndex;

            // 是否处于平滑插值过程中
            bool isCrossBlending = IsCrossFadeBlending(_lastPlayedClip, _crossFadeTime, _crossFadeProgress);

            // 上一个剪辑用来混合的索引帧数
            int frameIndexCrossFade = -1;
            // 2 个动画之间插值的因子
            float crossFadeBlendFactor = 1;

            if (isCrossBlending)
            {
                frameIndexCrossFade = GetCrossFadeFrameIndex();
                crossFadeBlendFactor = Mathf.Clamp01(_crossFadeProgress / _crossFadeTime);
            }

            GPUSkinningFrame frame = _playingClip.frames[frameIndex];
            bool isRootMotion = _playingClip.rootMotionEnabled && rootMotionEnabled;

            // 裁剪模式
            bool updateAnimate = true;
            bool updateRootMotion = true;
            if (cullingMode != GPUSkinningCullingMode.AlwaysAnimate)
            {
                if (!_meshRender.isVisible)
                {
                    updateAnimate = false;
                    if (cullingMode == GPUSkinningCullingMode.CullCompletely)
                    {
                        updateRootMotion = false;
                    }
                }
            }

            // 更新动画信息
            if (updateAnimate)
            {
                // x: 当前帧索引, y: 骨骼数据位于贴图的开始索引
                _materialPropertyBlock.SetVector(ShaderPorpID_FrameIndex_PixelSegmentation, new Vector4(frameIndex, _playingClip.pixelSegmentation, 0, 0));
                if (isRootMotion)
                {
                    Matrix4x4 rootMotionInv = animData.rootTransformMatrix * frame.RootMotionInv(animData.rootBoneIndex);
                    _materialPropertyBlock.SetMatrix(ShaderPorpID_RootMotion, rootMotionInv);
                }
                else
                {
                    _materialPropertyBlock.SetMatrix(ShaderPorpID_RootMotion, Matrix4x4.identity);
                }

                if (isCrossBlending)
                {
                    // x: 上一个动画帧 y: 上一个动画的骨骼数据位于贴图的开始索引 z: 动画混合百分比
                    _materialPropertyBlock.SetVector(ShaderPorpID_FrameIndex_PixelSegmentation_Blend_CrossFade, new Vector4(frameIndexCrossFade, _lastPlayedClip.pixelSegmentation, crossFadeBlendFactor));
                }
                else
                {
                    // z 传 1 时 Shader 中不会进行混合操作
                    _materialPropertyBlock.SetVector(ShaderPorpID_FrameIndex_PixelSegmentation_Blend_CrossFade, new Vector4(0, 0, 1));
                }

                _meshRender.SetPropertyBlock(_materialPropertyBlock);
            }

            // 更新根运动
            if (isRootMotion && deltaTime > 0)
            {
                if (updateRootMotion)
                {
                    DoRootMotion(deltaTime);
                }
            }
        }

        /// <summary>
        /// 是否在动画切换的平滑插值中
        /// </summary>
        public bool IsCrossFadeBlending(GPUSkinningClip lastPlayedClip, float crossFadeTime, float crossFadeProgress)
        {
            return lastPlayedClip != null && crossFadeTime > 0 && crossFadeProgress <= crossFadeTime;
        }

        /// <summary>
        /// 获取上一个剪辑中用于动画切换混合的帧索引
        /// </summary>
        private int GetCrossFadeFrameIndex()
        {
            if (_lastPlayedClip == null)
            {
                return 0;
            }

            if (_lastPlayedClip.wrapMode == GPUSkinningWrapMode.Once)
            {
                if (_lastPlayedTime >= _lastPlayedClip.length)
                {
                    return GetTheLastFrameIndex(_lastPlayedClip);
                }
                return GetFrameIndex(_lastPlayedClip, _lastPlayedTime);
            }
            return GetFrameIndex(_lastPlayedClip, _lastPlayedTime);
        }

        // 根运动实现
        private void DoRootMotion(float deltaTime)
        {
            int frameIndex = GetFrameIndex();
            GPUSkinningFrame frame = _playingClip.frames[frameIndex];
            if (frame == null)
            {
                return;
            }

            Transform trans = transform;

            if (wrapMode == GPUSkinningWrapMode.Once)
            {
                if (_rootMotionFrameIndex != frameIndex)
                {
                    Vector3 oldPos = _rootMotionDeltaPosition;
                    _rootMotionDeltaPosition = frame.rootMotionDeltaPositionQ * trans.forward * frame.rootMotionDeltaPositionL;
                    trans.Translate(_rootMotionDeltaPosition - oldPos, Space.World);

                    _rootMotionFrameIndex = frameIndex;
                }
            }
            else
            {
                // 循环播放模式下需要额外处理下重新播放时的位移
                int totalFrameCount = Mathf.Max((int)(deltaTime * _playingClip.frameRate), frameIndex - _rootMotionFrameIndex);
                int lastFrameIndex = GetTheLastFrameIndex(_playingClip);

                while (totalFrameCount > 0)
                {
                    if (_rootMotionFrameIndex + totalFrameCount >= lastFrameIndex)
                    {
                        var lastFrame = _playingClip.frames[lastFrameIndex];

                        Vector3 oldPos = _rootMotionDeltaPosition;
                        _rootMotionDeltaPosition = lastFrame.rootMotionDeltaPositionQ * trans.forward * lastFrame.rootMotionDeltaPositionL;
                        trans.Translate(_rootMotionDeltaPosition - oldPos, Space.World);

                        totalFrameCount -= (lastFrameIndex - _rootMotionFrameIndex);
                        _rootMotionFrameIndex = 0;
                        _rootMotionDeltaPosition = Vector3.zero;
                    }
                    else
                    {
                        Vector3 oldPos = _rootMotionDeltaPosition;
                        _rootMotionDeltaPosition = frame.rootMotionDeltaPositionQ * trans.forward * frame.rootMotionDeltaPositionL;
                        trans.Translate(_rootMotionDeltaPosition - oldPos, Space.World);

                        totalFrameCount = 0;
                    }
                }

                _rootMotionFrameIndex = frameIndex;
            }

            transform.rotation *= frame.rootMotionDeltaRotation;
        }
    }
}
