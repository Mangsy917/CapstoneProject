using Game.Common;
using Game.UI.Hub;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    // 거점 씬의 HubInventory 인스펙터에서 레벨, 경험치, 크레딧을 조정함
    // - 플레이 전: 처음 값(D_PlayerStart)을 고침. 거점과 연습장이 같이 씀
    // - 플레이 중: 지금 값을 고침. 바로 화면에 반영되고 플레이를 끝내면 처음 값으로 돌아감
    [CustomEditor(typeof(HubInventory))]
    public class HubInventoryEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (Application.isPlaying) DrawCurrentValues();
            else DrawStartValues();
        }

        private void DrawStartValues()
        {
            EditorGUILayout.LabelField("처음 레벨 / 크레딧", EditorStyles.boldLabel);

            Object startData = serializedObject.FindProperty("HI_startData").objectReferenceValue;
            if (startData == null)
            {
                EditorGUILayout.HelpBox("Start Data를 넣어주세요 (D_PlayerStart)", MessageType.Warning);
                return;
            }

            SerializedObject startObject = new SerializedObject(startData);
            SerializedProperty level = startObject.FindProperty("_level");
            SerializedProperty experience = startObject.FindProperty("_experience");
            EditorGUILayout.PropertyField(level, new GUIContent("레벨"));
            EditorGUILayout.PropertyField(experience, new GUIContent("경험치"));
            EditorGUILayout.PropertyField(startObject.FindProperty("PSD_credit"), new GUIContent("크레딧"));

            int maxExperience = PlayerProgress.GetMaxExperience(Mathf.Max(1, level.intValue));
            int safeExperience = Mathf.Clamp(experience.intValue, 0, maxExperience - 1);
            if (experience.intValue != safeExperience) experience.intValue = safeExperience;
            EditorGUILayout.LabelField(" ", $"다음 레벨까지 {maxExperience:N0}");
            startObject.ApplyModifiedProperties();
        }

        private void DrawCurrentValues()
        {
            EditorGUILayout.LabelField("지금 레벨 / 크레딧 (플레이 중)", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            int level = EditorGUILayout.IntField("레벨", PlayerProgress.PP_Level);
            int experience = EditorGUILayout.IntField("경험치", PlayerProgress.PP_Experience);
            if (EditorGUI.EndChangeCheck()) PlayerProgress.SetLevel(level, experience);

            EditorGUI.BeginChangeCheck();
            int credit = EditorGUILayout.IntField("크레딧", PlayerProgress.PP_Credit);
            if (EditorGUI.EndChangeCheck()) PlayerProgress.SetCredit(credit);
        }
    }
}
