/*
 * @author: wizardc
 */

using Dou.GPUSkinning;
using UnityEditor;
using UnityEngine;

namespace DouEditor.GPUSkinning
{
    /// <summary>
    /// GPUSkinning 生成器编辑界面
    /// </summary>
    [CustomEditor(typeof(GPUSkinningGenerator))]
    public class GPUSkinningGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            GPUSkinningGenerator gen = target as GPUSkinningGenerator;
            if (gen == null)
            {
                return;
            }

            EditorGUILayout.Space();
            GUILayout.BeginVertical();
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("animName"), new GUIContent("动画名称", "生成的GPUSkinning动画名称"));
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("rootBoneTransform"), new GUIContent("根骨骼Transform"));
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("frameData"), new GUIContent("帧率设置", "自定义指定动画剪辑的采样帧率，不设置则使用剪辑自己的帧率"));
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("generatorPrefab"), new GUIContent("创建预制体", "同时创建 GPUSkinning 动画预制体"));
                EditorGUILayout.Space();

                // 存储属性修改
                serializedObject.ApplyModifiedProperties();

                // 在场景中时
                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(gen)))
                {
                    if (!Application.isPlaying)
                    {
                        if (GUILayout.Button("启动编辑器"))
                        {
                            EditorApplication.isPlaying = true;
                        }
                    }
                    else
                    {
                        if (gen.IsSamplingProgress())
                        {
                            GUI.color = Color.gray;
                            if (GUILayout.Button("开始采样"))
                            {
                            }
                            GUI.color = Color.white;
                        }
                        else
                        {
                            if (GUILayout.Button("开始采样"))
                            {
                                gen.OnBeginSample();
                            }
                        }
                    }
                }
                // 不在场景中时
                else
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.FlexibleSpace();
                        GUI.color = Color.yellow;
                        GUILayout.Label("需要采样请拖入场景中");
                        GUI.color = Color.white;
                        GUILayout.FlexibleSpace();
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndVertical();
            EditorGUILayout.Space();
        }
    }
}
