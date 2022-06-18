/*
 * @author: wizardc
 */

using Dou.GPUSkinning;
using UnityEditor;
using UnityEngine;

namespace DouEditor.GPUSkinning
{
    /// <summary>
    /// GPUSkinning 动画编辑界面
    /// </summary>
    [CustomEditor(typeof(GPUSkinningAnimation))]
    public class GPUSkinningAnimationEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            GPUSkinningAnimation gen = target as GPUSkinningAnimation;
            if (gen == null)
            {
                return;
            }

            GUILayout.BeginVertical();
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("animData"), new GUIContent("动画数据"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("mesh"), new GUIContent("动画模型"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("material"), new GUIContent("动画材质"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cullingMode"), new GUIContent("裁剪模式"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("rootMotionEnabled"), new GUIContent("根运动"));

                // 存储属性修改
                serializedObject.ApplyModifiedProperties();
            }
            GUILayout.EndVertical();
        }
    }
}
