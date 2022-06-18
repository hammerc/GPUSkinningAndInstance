/*
 * @author: wizardc
 */

#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Dou.GPUSkinning
{
    /// <summary>
    /// GPUSkinning 生成器动画数据
    /// </summary>
    public class GPUSkinningGeneratorAnimationData
    {
        public AnimationClip clip;
        public string name;
        public int frameRate;
        public GPUSkinningWrapMode wrapMode = GPUSkinningWrapMode.Once;
        public bool rootMotion = false;
    }

    /// <summary>
    /// GPUSkinning 生成器自定义采样帧率
    /// </summary>
    [Serializable]
    public class GPUSkinningGeneratorFrameData
    {
        public string clipName;
        public int frameRate;
    }

    /// <summary>
    /// GPUSkinning 生成器，用于采样和生成 GPUSkinning 渲染需要的数据
    /// </summary>
    public class GPUSkinningGenerator : MonoBehaviour
    {
        /// <summary>
        /// 生成的动画名称
        /// </summary>
        public string animName;

        /// <summary>
        /// 根骨骼转换对象
        /// </summary>
        public Transform rootBoneTransform;

        /// <summary>
        /// 自定义采样帧率
        /// </summary>
        public List<GPUSkinningGeneratorFrameData> frameData = new List<GPUSkinningGeneratorFrameData>();

        /// <summary>
        /// 是否同时生成 GPUSkinning 动画预制体
        /// </summary>
        public bool generatorPrefab = false;

        /// <summary>
        /// 生成的动画数据对象
        /// </summary>
        private GPUSkinningAnimationData _animData;

        /// <summary>
        /// 生成的新 Mesh 数据对象
        /// </summary>
        private Mesh _savedMesh;

        /// <summary>
        /// 当前的所有动画剪辑列表
        /// </summary>
        private List<GPUSkinningGeneratorAnimationData> _animClips = new List<GPUSkinningGeneratorAnimationData>();

        /// <summary>
        /// 当前采样的动画剪辑
        /// </summary>
        private AnimationClip _animClip;

        /// <summary>
        /// 是否正在采样中
        /// </summary>
        private bool _isSampling = false;

        /// <summary>
        /// 当前采样的剪辑索引
        /// </summary>
        private int _samplingClipIndex = -1;

        /// <summary>
        /// 当前采样的剪辑帧数索引
        /// </summary>
        private int _samplingFrameIndex = 0;

        /// <summary>
        /// 当前采样的剪辑总帧数
        /// </summary>
        private int _samplingTotalFrams = 0;

        private string _saveDir;

        private Animator _animator;
        private RuntimeAnimatorController _runtimeAnimatorController;

        private SkinnedMeshRenderer _skinnedMeshRenderer;
        private Mesh _mesh;

        private GPUSkinningClip _gpuSkinningClip;
        private Vector3 _rootMotionPosition;
        private Quaternion _rootMotionRotation;

        private void ShowDialog(string msg, bool stopSimple = false)
        {
            if (stopSimple)
            {
                DestroyImmediate(this.gameObject);
                EditorApplication.isPlaying = false;
            }

            EditorUtility.DisplayDialog("GPUSkinning", msg, "确定");
        }

        /// <summary>
        /// 是否正在采样中
        /// </summary>
        public bool IsSamplingProgress()
        {
            return _samplingClipIndex != -1;
        }

        /// <summary>
        /// 收集所有的动画剪辑
        /// </summary>
        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                ShowDialog("没有找到 Animator 组件", true);
                return;
            }

            if (_animator.runtimeAnimatorController == null)
            {
                ShowDialog("Animator 组件缺少 RuntimeAnimatorController", true);
                return;
            }

            if (_animator.runtimeAnimatorController is AnimatorOverrideController)
            {
                ShowDialog("RuntimeAnimatorController 不能是 AnimatorOverrideController 对象", true);
                return;
            }

            _skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            if (_skinnedMeshRenderer == null)
            {
                ShowDialog("没有找到 SkinnedMeshRenderer 组件", true);
                return;
            }

            _mesh = _skinnedMeshRenderer.sharedMesh;
            if (_mesh == null)
            {
                ShowDialog("没有 Mesh 对象", true);
                return;
            }

            if (string.IsNullOrEmpty(animName.Trim()))
            {
                ShowDialog("动画名称不能为空", true);
                return;
            }

            if (rootBoneTransform == null)
            {
                ShowDialog("请设置根骨骼", true);
                return;
            }

            _runtimeAnimatorController = _animator.runtimeAnimatorController;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            InitTransform();
            CollectAnimationClips();

            if (_animClips.Count == 0)
            {
                ShowDialog("动画剪辑不能为空", true);
            }
        }

        private void InitTransform()
        {
            transform.parent = null;
            transform.position = Vector3.zero;
            transform.eulerAngles = Vector3.zero;
        }

        /// <summary>
        /// 收集所有的动画剪辑
        /// 当编辑器界面出现或改变时都会调用该方法收集最新的动画剪辑数据，主要处理动画剪辑被修改时，比如更新git或svn时
        /// </summary>
        public void CollectAnimationClips()
        {
            AnimationClip[] clips = _runtimeAnimatorController.animationClips;

            // 添加新增加的动画剪辑数据
            for (int i = 0; i < clips.Length; ++i)
            {
                AnimationClip clip = clips[i];
                if (clip != null)
                {
                    _animClips.Add(new GPUSkinningGeneratorAnimationData()
                    {
                        name = clip.name,
                        clip = clip,
                        frameRate = (int)clip.frameRate,
                        wrapMode = clip.isLooping ? GPUSkinningWrapMode.Loop : GPUSkinningWrapMode.Once
                    });
                }
            }
        }

        /// <summary>
        /// 开始采样时
        /// </summary>
        public void OnBeginSample()
        {
            string savePath = EditorUtility.SaveFolderPanel("GPUSkinning 动画数据保存路径", Application.dataPath, string.Empty);
            if (string.IsNullOrEmpty(savePath))
            {
                ShowDialog("请设定保存路径", true);
                return;
            }

            if (!savePath.Contains(Application.dataPath.Replace('\\', '/')))
            {
                ShowDialog("必须选择位于当前项目下的Asset目录中", true);
                return;
            }

            _saveDir = "Assets" + savePath.Substring(Application.dataPath.Length);

            _samplingClipIndex = 0;

            // 创建 GPUSkinning 数据对象
            _animData = ScriptableObject.CreateInstance<GPUSkinningAnimationData>();

            // 收集骨骼
            List<GPUSkinningBone> bones_result = new List<GPUSkinningBone>();
            CollectBones(bones_result, _skinnedMeshRenderer.bones, _mesh.bindposes, null, rootBoneTransform, 0);
            _animData.bones = bones_result;
            _animData.clips = new List<GPUSkinningClip>();
            _animData.rootBoneIndex = 0;
            _animData.rootTransformMatrix = Matrix4x4.TRS(rootBoneTransform.localPosition,
                rootBoneTransform.localRotation, rootBoneTransform.localScale);

            string savedAnimPath = _saveDir + "/GPUSKinning_Data_" + animName + ".asset";
            AssetDatabase.CreateAsset(_animData, savedAnimPath);

            // 创建新的 Mesh 对象
            _savedMesh = CreateNewMesh(_skinnedMeshRenderer.sharedMesh, "GPUSkinMesh");
            string savedMeshPath = _saveDir + "/GPUSKinning_Mesh_" + animName + ".asset";
            AssetDatabase.CreateAsset(_savedMesh, savedMeshPath);

            // 开始采样
            StartSample();
        }

        /// <summary>
        /// 收集骨骼和 bindpose 信息
        /// </summary>
        private void CollectBones(List<GPUSkinningBone> bones_result, Transform[] bones_smr, Matrix4x4[] bindposes, GPUSkinningBone parentBone, Transform currentBoneTransform, int currentBoneIndex)
        {
            // 创建骨骼对象
            GPUSkinningBone currentBone = new GPUSkinningBone();
            bones_result.Add(currentBone);

            // 找到骨骼索引并记录相关数据
            int indexOfSmrBones = System.Array.IndexOf(bones_smr, currentBoneTransform);
            currentBone.transform = currentBoneTransform;
            currentBone.name = currentBone.transform.gameObject.name;
            currentBone.bindpose = indexOfSmrBones == -1 ? Matrix4x4.identity : bindposes[indexOfSmrBones];
            currentBone.parentBoneIndex = parentBone == null ? -1 : bones_result.IndexOf(parentBone);

            if (parentBone != null)
            {
                parentBone.childrenBonesIndices[currentBoneIndex] = bones_result.IndexOf(currentBone);
            }

            // 收集子骨骼
            int numChildren = currentBone.transform.childCount;
            if (numChildren > 0)
            {
                currentBone.childrenBonesIndices = new int[numChildren];
                for (int i = 0; i < numChildren; ++i)
                {
                    CollectBones(bones_result, bones_smr, bindposes, currentBone, currentBone.transform.GetChild(i), i);
                }
            }
        }

        /// <summary>
        /// 为 Mesh 新增 uv2 和 uv3 数据，每个顶点记录 4 个关联的骨骼索引和权重
        /// Unity 动画系统中每个顶点最多可以绑定 4 个骨骼
        /// </summary>
        private Mesh CreateNewMesh(Mesh mesh, string meshName)
        {
            Vector3[] normals = mesh.normals;
            Vector4[] tangents = mesh.tangents;
            Color[] colors = mesh.colors;
            Vector2[] uv = mesh.uv;

            Mesh newMesh = new Mesh();

            // 拷贝原有数据
            newMesh.name = meshName;
            newMesh.vertices = mesh.vertices;
            if (normals != null && normals.Length > 0)
            {
                newMesh.normals = normals;
            }
            if (tangents != null && tangents.Length > 0)
            {
                newMesh.tangents = tangents;
            }
            if (colors != null && colors.Length > 0)
            {
                newMesh.colors = colors;
            }
            if (uv != null && uv.Length > 0)
            {
                newMesh.uv = uv;
            }

            // 处理每个顶点的4个关联骨骼
            int numVertices = mesh.vertexCount;
            BoneWeight[] boneWeights = mesh.boneWeights;
            Vector4[] uv2 = new Vector4[numVertices];
            Vector4[] uv3 = new Vector4[numVertices];
            Transform[] smrBones = _skinnedMeshRenderer.bones;
            for (int i = 0; i < numVertices; ++i)
            {
                BoneWeight boneWeight = boneWeights[i];

                BoneWeightSortData[] weights = new BoneWeightSortData[4];
                weights[0] = new BoneWeightSortData() { index = boneWeight.boneIndex0, weight = boneWeight.weight0 };
                weights[1] = new BoneWeightSortData() { index = boneWeight.boneIndex1, weight = boneWeight.weight1 };
                weights[2] = new BoneWeightSortData() { index = boneWeight.boneIndex2, weight = boneWeight.weight2 };
                weights[3] = new BoneWeightSortData() { index = boneWeight.boneIndex3, weight = boneWeight.weight3 };
                // 权重由大到小的排序
                System.Array.Sort(weights);

                // 获取对应的骨骼对象
                GPUSkinningBone bone0 = GetBoneByTransform(smrBones[weights[0].index]);
                GPUSkinningBone bone1 = GetBoneByTransform(smrBones[weights[1].index]);
                GPUSkinningBone bone2 = GetBoneByTransform(smrBones[weights[2].index]);
                GPUSkinningBone bone3 = GetBoneByTransform(smrBones[weights[3].index]);

                // 设置骨骼索引和骨骼权重
                Vector4 skinData_01 = new Vector4();
                skinData_01.x = GetBoneIndex(bone0);
                skinData_01.y = weights[0].weight;
                skinData_01.z = GetBoneIndex(bone1);
                skinData_01.w = weights[1].weight;
                uv2[i] = skinData_01;

                Vector4 skinData_23 = new Vector4();
                skinData_23.x = GetBoneIndex(bone2);
                skinData_23.y = weights[2].weight;
                skinData_23.z = GetBoneIndex(bone3);
                skinData_23.w = weights[3].weight;
                uv3[i] = skinData_23;
            }

            newMesh.SetUVs(1, new List<Vector4>(uv2));
            newMesh.SetUVs(2, new List<Vector4>(uv3));

            newMesh.triangles = mesh.triangles;
            newMesh.bounds = mesh.bounds;
            return newMesh;
        }

        private GPUSkinningBone GetBoneByTransform(Transform transform)
        {
            List<GPUSkinningBone> bones = _animData.bones;
            int numBones = bones.Count;
            for (int i = 0; i < numBones; ++i)
            {
                if (bones[i].transform == transform)
                {
                    return bones[i];
                }
            }
            return null;
        }

        private int GetBoneIndex(GPUSkinningBone bone)
        {
            List<GPUSkinningBone> bones = _animData.bones;
            int numBones = bones.Count;
            for (int i = 0; i < numBones; ++i)
            {
                if (bone == bones[i])
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 开始采样，并非对所有剪辑进行采样，每次调用只采样一个动画剪辑
        /// </summary>
        public void StartSample()
        {
            if (_isSampling)
            {
                return;
            }

            _animClip = _animClips[_samplingClipIndex].clip;
            if (_animClip == null)
            {
                _isSampling = false;
                return;
            }

            _animClip = _animClips[_samplingClipIndex].clip;
            if (_animClip == null)
            {
                _isSampling = false;
                return;
            }

            // 总帧数
            int numFrames = (int)(GetClipFrameRate(_animClips[_samplingClipIndex]) * _animClip.length);
            if (numFrames == 0)
            {
                _isSampling = false;
                return;
            }

            _samplingFrameIndex = 0;

            // 创建剪辑对象
            _gpuSkinningClip = new GPUSkinningClip();
            _gpuSkinningClip.name = _animClips[_samplingClipIndex].name;
            _gpuSkinningClip.frameRate = GetClipFrameRate(_animClips[_samplingClipIndex]);
            _gpuSkinningClip.length = _animClip.length;
            _gpuSkinningClip.wrapMode = _animClips[_samplingClipIndex].wrapMode;
            _gpuSkinningClip.frames = new GPUSkinningFrame[numFrames];
            _gpuSkinningClip.rootMotionEnabled = _animClips[_samplingClipIndex].rootMotion;

            // 添加到列表
            _animData.clips.Add(_gpuSkinningClip);

            SetCurrentAnimationClip();
            PrepareRecordAnimator();

            _isSampling = true;
        }

        private int GetClipFrameRate(GPUSkinningGeneratorAnimationData clip)
        {
            foreach (GPUSkinningGeneratorFrameData frame in frameData)
            {
                if (frame.clipName == clip.name)
                {
                    return Mathf.Clamp(frame.frameRate, 1, 120);
                }
            }

            return clip.frameRate;
        }

        /// <summary>
        /// 将所有的动画剪辑都覆盖为当前播放的剪辑，用于设置当前播放的剪辑
        /// </summary>
        private void SetCurrentAnimationClip()
        {
            AnimatorOverrideController animatorOverrideController = new AnimatorOverrideController();
            AnimationClip[] clips = _runtimeAnimatorController.animationClips;
            // 覆盖所有剪辑
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            for (int i = 0; i < clips.Length; ++i)
            {
                overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(clips[i], _animClip));
            }
            // 设置为当前的动画控制器
            animatorOverrideController.runtimeAnimatorController = _runtimeAnimatorController;
            animatorOverrideController.ApplyOverrides(overrides);
            _animator.runtimeAnimatorController = animatorOverrideController;
        }

        /// <summary>
        /// 根据设定的帧数提前录制好当前的动画
        ///
        /// 先调用StartRecording进行录制，结束时调用StopRecording
        /// 然后再需要时进行回放，需要注意调用StartPlayback开始回放之后，回放的时间需要手动更新
        /// 每一帧的更新值可以使用DeltaTime，而开始值可以用animator.recorderStartTime
        /// 这时，还需要判断playback的时间是否大于录制结束时间，否则会有警告:
        /// 还需要注意两点
        /// 1.animator.StartRecording(...)的参数如果小于1，会被判定为不限时间录制。
        /// 2.非Animator驱动的位移，都会被录制进去。由于Animator的更新时间是在Update之后，LateUpdate之前。
        /// 所以移动控制写在LateUpdate里的时候，在回播时会有操作冲突
        /// </summary>
        private void PrepareRecordAnimator()
        {
            int numFrames = (int)(_gpuSkinningClip.frameRate * _gpuSkinningClip.length);

            _animator.applyRootMotion = _gpuSkinningClip.rootMotionEnabled;
            // 重新绑定所有的动画数据，比如 mesh 添加移除后等情况
            _animator.Rebind();
            _animator.recorderStartTime = 0;
            _animator.StartRecording(numFrames);
            for (int i = 0; i < numFrames; ++i)
            {
                _animator.Update(1.0f / _gpuSkinningClip.frameRate);
            }
            _animator.StopRecording();
            // 开始回放
            _animator.StartPlayback();
        }

        /// <summary>
        /// 采样
        /// </summary>
        private void Update()
        {
            CheckSampleNext();
            Sample();
        }

        private void CheckSampleNext()
        {
            if (!_isSampling && IsSamplingProgress())
            {
                if (++_samplingClipIndex < _animClips.Count)
                {
                    StartSample();
                }
                else
                {
                    OnEndSample();
                }
            }
        }

        private void Sample()
        {
            if (!_isSampling)
            {
                return;
            }

            int totalFrams = (int)(_gpuSkinningClip.length * _gpuSkinningClip.frameRate);
            _samplingTotalFrams = totalFrams;

            // 采样剪辑完毕
            if (_samplingFrameIndex >= totalFrams)
            {
                _animator.StopPlayback();

                EditorUtility.SetDirty(_animData);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                _isSampling = false;
                return;
            }

            // 创建动画帧对象
            GPUSkinningFrame frame = new GPUSkinningFrame();
            _gpuSkinningClip.frames[_samplingFrameIndex] = frame;
            frame.matrices = new Matrix4x4[_animData.bones.Count];

            // 更新动画
            float time = _gpuSkinningClip.length * ((float)_samplingFrameIndex / totalFrams);
            _animator.playbackTime = time;
            _animator.Update(0);

            // 开协程对本帧的动画进行采样
            StartCoroutine(SamplingCoroutine(frame, totalFrams));
        }

        private IEnumerator SamplingCoroutine(GPUSkinningFrame frame, int totalFrames)
        {
            // 等待动画更新完毕
            yield return new WaitForEndOfFrame();

            // 遍历所有的骨骼，存储可以直接将 mesh 空间的顶点坐标转换到指定骨骼对应根骨骼空间的坐标位置
            List<GPUSkinningBone> bones = _animData.bones;
            int numBones = bones.Count;
            for (int i = 0; i < numBones; ++i)
            {
                GPUSkinningBone currentBone = bones[i];
                // mesh空间的顶点乘以当前骨骼的 bindpose 矩阵后得到当前骨骼的对应空间坐标
                frame.matrices[i] = currentBone.bindpose;
                do
                {
                    // 获取当前骨骼相对于父骨骼的矩阵
                    Matrix4x4 mat = Matrix4x4.TRS(currentBone.transform.localPosition,
                        currentBone.transform.localRotation, currentBone.transform.localScale);
                    // matrices[i] 存储的 bindpose 矩阵被 mat 乘以后，就会变成 mesh空间的顶点乘以 matrices[i] 后可以得到父骨骼的对应空间坐标
                    // 相当于是把父骨骼的转换信息也记录进去了
                    frame.matrices[i] = mat * frame.matrices[i];
                    if (currentBone.parentBoneIndex == -1)
                    {
                        break;
                    }
                    currentBone = bones[currentBone.parentBoneIndex];
                } while (true);

                // 上面会遍历处理到根骨骼，matrices[i] 存储的 bindpose 矩阵就变成了可以从 mesh 空间转换到正确的目标骨骼空间的矩阵
            }

            // 计算出每帧的根骨骼偏移，用来实现 rootMotion
            int rootBoneIndex = _animData.rootBoneIndex;
            if (_samplingFrameIndex == 0)
            {
                _rootMotionPosition = bones[rootBoneIndex].transform.localPosition;
                _rootMotionRotation = bones[rootBoneIndex].transform.localRotation;
            }
            else
            {
                Vector3 newPosition = bones[rootBoneIndex].transform.localPosition;
                Quaternion newRotation = bones[rootBoneIndex].transform.localRotation;
                Vector3 deltaPosition = newPosition - _rootMotionPosition;
                frame.rootMotionDeltaPositionQ = Quaternion.Inverse(Quaternion.Euler(transform.forward.normalized)) *
                                                 Quaternion.Euler(deltaPosition.normalized);
                frame.rootMotionDeltaPositionL = deltaPosition.magnitude;
                frame.rootMotionDeltaRotation = Quaternion.Inverse(_rootMotionRotation) * newRotation;

                if (_samplingFrameIndex == 1)
                {
                    _gpuSkinningClip.frames[0].rootMotionDeltaPositionQ =
                        _gpuSkinningClip.frames[1].rootMotionDeltaPositionQ;
                    _gpuSkinningClip.frames[0].rootMotionDeltaPositionL =
                        _gpuSkinningClip.frames[1].rootMotionDeltaPositionL;
                    _gpuSkinningClip.frames[0].rootMotionDeltaRotation =
                        _gpuSkinningClip.frames[1].rootMotionDeltaRotation;
                }
            }

            ++_samplingFrameIndex;
        }

        /// <summary>
        /// 结束采样时
        /// </summary>
        public void OnEndSample()
        {
            _samplingClipIndex = -1;

            // 记录动画贴图数据
            SetTextureInfo(_animData);

            // 生成动画贴图
            CreateTexture2D();

            // 生成预制体
            if (generatorPrefab)
            {
                GeneratorPrefab();
            }

            Selection.activeObject = _animData;
            ShowDialog("采样完毕", true);
        }

        /// <summary>
        /// 记录每个动画剪辑的像素其实值和贴图最终尺寸
        /// </summary>
        private void SetTextureInfo(GPUSkinningAnimationData data)
        {
            int numPixels = 0;

            var clips = data.clips;
            int numClips = clips.Count;
            for (int clipIndex = 0; clipIndex < numClips; ++clipIndex)
            {
                GPUSkinningClip clip = clips[clipIndex];
                // 记录当前剪辑的像素开始索引
                clip.pixelSegmentation = numPixels;

                // numPixels 加上当前剪辑的像素长度，即下一个剪辑的像素开始索引
                // 一个 float3x4 的矩阵数据需要 6 个像素点来记录
                numPixels += data.bones.Count * 6 * clip.frames.Length;
            }

            CalculateTextureSize(numPixels, out data.textureWidth, out data.textureHeight);
        }

        private void CalculateTextureSize(int numPixels, out int texWidth, out int texHeight)
        {
            texWidth = 1;
            texHeight = 1;
            while (true)
            {
                if (texWidth * texHeight >= numPixels) break;
                texWidth *= 2;
                if (texWidth * texHeight >= numPixels) break;
                texHeight *= 2;
            }
        }

        /// <summary>
        /// 创建动画贴图
        /// </summary>
        private void CreateTexture2D()
        {
            Texture2D texture = new Texture2D(_animData.textureWidth, _animData.textureHeight, TextureFormat.RGBA32, false, true);
            // 由于是直接对目标数据进行采样，所以必须使用 Point 类型
            texture.filterMode = FilterMode.Point;

            Color[] pixels = texture.GetPixels();
            int pixelIndex = 0;
            int clipCount = _animData.clips.Count;
            for (int clipIndex = 0; clipIndex < clipCount; ++clipIndex)
            {
                GPUSkinningClip clip = _animData.clips[clipIndex];
                var frames = clip.frames;
                int numFrames = frames.Length;
                for (int frameIndex = 0; frameIndex < numFrames; ++frameIndex)
                {
                    GPUSkinningFrame frame = frames[frameIndex];
                    Matrix4x4[] matrices = frame.matrices;
                    int numMatrices = matrices.Length;
                    for (int matrixIndex = 0; matrixIndex < numMatrices; ++matrixIndex)
                    {
                        // 转换浮点数为颜色值
                        Matrix4x4 matrix = matrices[matrixIndex];
                        pixels[pixelIndex++] = Float2ToColor(matrix.m00, matrix.m01);
                        pixels[pixelIndex++] = Float2ToColor(matrix.m02, matrix.m03);

                        pixels[pixelIndex++] = Float2ToColor(matrix.m10, matrix.m11);
                        pixels[pixelIndex++] = Float2ToColor(matrix.m12, matrix.m13);

                        pixels[pixelIndex++] = Float2ToColor(matrix.m20, matrix.m21);
                        pixels[pixelIndex++] = Float2ToColor(matrix.m22, matrix.m23);
                    }
                }
            }
            texture.SetPixels(pixels, 0);
            texture.Apply(false);

            AssetDatabase.CreateAsset(texture, _saveDir + "/GPUSKinning_AnimMap_" + animName + ".asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 将 2 个 32 位的 float 数据编码到一个 32 位颜色中，只保留浮点 16 位的数据
        /// </summary>
        private Color Float2ToColor(float v1, float v2)
        {
            ushort rg = Mathf.FloatToHalf(v1);
            ushort ba = Mathf.FloatToHalf(v2);
            return new Color((rg >> 8 & 0x00ff) / 255f, (rg & 0x00ff) / 255f, (ba >> 8 & 0x00ff) / 255f, (ba & 0x00ff) / 255f);
        }

        /// <summary>
        /// 生成 GPUSkinning 动画预制体
        /// </summary>
        private void GeneratorPrefab()
        {
            // 材质
            Material material = new Material(Shader.Find("Dou/GPUSkinning/Unlit"));
            material.SetTexture("_MatrixTex", AssetDatabase.LoadAssetAtPath<Texture>(_saveDir + "/GPUSKinning_AnimMap_" + animName + ".asset"));
            material.enableInstancing = true;

            AssetDatabase.CreateAsset(material, _saveDir + "/GPUSKinning_Material_" + animName + ".mat");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 预制体
            GameObject go = new GameObject();
            GPUSkinningAnimation animation = go.AddComponent<GPUSkinningAnimation>();
            animation.animData = AssetDatabase.LoadAssetAtPath<GPUSkinningAnimationData>(_saveDir + "/GPUSKinning_Data_" + animName + ".asset");
            animation.mesh = AssetDatabase.LoadAssetAtPath<Mesh>(_saveDir + "/GPUSKinning_Mesh_" + animName + ".asset");
            animation.material = material;

            MeshRenderer meshRender = go.AddComponent<MeshRenderer>();
            meshRender.sharedMaterial = material;

            MeshFilter meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(_saveDir + "/GPUSKinning_Mesh_" + animName + ".asset");

            PrefabUtility.SaveAsPrefabAsset(go, _saveDir + "/GPUSKinning_" + animName + ".prefab");
            GameObject.DestroyImmediate(go);
        }

        private class BoneWeightSortData : System.IComparable<BoneWeightSortData>
        {
            public int index = 0;

            public float weight = 0;

            public int CompareTo(BoneWeightSortData b)
            {
                return weight > b.weight ? -1 : 1;
            }
        }
    }
}

#endif
