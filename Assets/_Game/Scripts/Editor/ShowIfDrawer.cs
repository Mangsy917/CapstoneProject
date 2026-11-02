using System;
using Game.Common;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    // [ShowIf]를 붙인 칸을 조건 칸의 값이 맞을 때만 그림 (조건 칸을 못 찾으면 항상 그림)
    [CustomPropertyDrawer(typeof(ShowIfAttribute))]
    public class ShowIfDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!IsShown(property)) return;
            EditorGUI.PropertyField(position, property, label, includeChildren: true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // 숨긴 칸은 칸 사이 간격까지 빼서 빈 줄이 남지 않게 함
            if (!IsShown(property)) return -EditorGUIUtility.standardVerticalSpacing;
            return EditorGUI.GetPropertyHeight(property, label, includeChildren: true);
        }

        private bool IsShown(SerializedProperty property)
        {
            var showIf = (ShowIfAttribute)attribute;

            // 같은 클래스 안의 칸을 찾도록 이 칸의 경로에서 마지막 이름만 바꿈 (예: _frames.Array.data[0].AD_damage → _frames.Array.data[0]._frameType)
            string path = property.propertyPath;
            int lastDotIndex = path.LastIndexOf('.');
            string conditionPath = lastDotIndex < 0 ? showIf.ConditionName : path.Substring(0, lastDotIndex + 1) + showIf.ConditionName;

            SerializedProperty condition = property.serializedObject.FindProperty(conditionPath);
            if (condition == null) return true;

            switch (condition.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    return showIf.ExpectedValue == null ? condition.boolValue : condition.boolValue.Equals(showIf.ExpectedValue);
                case SerializedPropertyType.Enum:
                    return showIf.ExpectedValue != null && condition.intValue == Convert.ToInt32(showIf.ExpectedValue);
                default:
                    return true;
            }
        }
    }
}
