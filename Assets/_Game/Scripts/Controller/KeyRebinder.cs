using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class KeyRebinder : MonoBehaviour
{
    [Serializable]
    public class BindingOverrideEntry
    {
        [Tooltip("바꾸고 싶은 액션 이름")]
        public string actionName;

        [Tooltip("키보드 그룹에 적용할 새 바인딩 경로. 비워두면 기본값 유지.")]
        public string keyboardOverridePath;

        [Tooltip("게임패드 그룹에 적용할 새 바인딩 경로. 비워두면 기본값 유지.")]
        public string gamepadOverridePath;
    }

    [Header("대상 Input Action Asset")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Char";

    [Header("이동 버튼 매핑")]
    [SerializeField]
    private List<BindingOverrideEntry> movementOverrides = new List<BindingOverrideEntry>
    {
        new BindingOverrideEntry { actionName = "Up",    keyboardOverridePath = "<Keyboard>/w", gamepadOverridePath = "" },
        new BindingOverrideEntry { actionName = "Down",  keyboardOverridePath = "<Keyboard>/s", gamepadOverridePath = "" },
        new BindingOverrideEntry { actionName = "Left",  keyboardOverridePath = "<Keyboard>/a", gamepadOverridePath = "" },
        new BindingOverrideEntry { actionName = "Right", keyboardOverridePath = "<Keyboard>/d", gamepadOverridePath = "" },
    };

    [Header("공격 버튼 매핑")]
    [SerializeField]
    private List<BindingOverrideEntry> attackOverrides = new List<BindingOverrideEntry>
    {
        new BindingOverrideEntry { actionName = "AttackL",  keyboardOverridePath = "<Keyboard>/u", gamepadOverridePath = "<Gamepad>/buttonWest" },
        new BindingOverrideEntry { actionName = "AttackM",  keyboardOverridePath = "<Keyboard>/i", gamepadOverridePath = "<Gamepad>/buttonNorth" },
        new BindingOverrideEntry { actionName = "AttackH",  keyboardOverridePath = "<Keyboard>/j", gamepadOverridePath = "<Gamepad>/buttonSouth" },
        new BindingOverrideEntry { actionName = "AttackEX", keyboardOverridePath = "<Keyboard>/k", gamepadOverridePath = "<Gamepad>/buttonEast" },
    };

    private void Awake()
    {
        ApplyAllOverrides();
    }

    [ContextMenu("Apply All Overrides")]
    public void ApplyAllOverrides()
    {
        if (inputActions == null)
        {
            Debug.LogError("[KeyRebinder] InputActionAsset이 할당되지 않았습니다.");
            return;
        }

        var map = inputActions.FindActionMap(actionMapName, throwIfNotFound: true);

        foreach (var entry in movementOverrides)
            ApplyEntry(map, entry);

        foreach (var entry in attackOverrides)
            ApplyEntry(map, entry);
    }

    private void ApplyEntry(InputActionMap map, BindingOverrideEntry entry)
    {
        if (string.IsNullOrEmpty(entry.actionName))
            return;

        var action = map.FindAction(entry.actionName);
        if (action == null)
        {
            Debug.LogWarning($"[KeyRebinder] 액션을 찾을 수 없습니다: {entry.actionName}");
            return;
        }

        ApplyForGroup(action, "PC", entry.keyboardOverridePath);
        ApplyForGroup(action, "GamePad", entry.gamepadOverridePath);
    }

    private void ApplyForGroup(InputAction action, string group, string newPath)
    {
        if (string.IsNullOrEmpty(newPath))
            return; 

        var bindings = action.bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            if (!string.IsNullOrEmpty(bindings[i].groups) && bindings[i].groups.Contains(group))
            {
                action.ApplyBindingOverride(i, new InputBinding { overridePath = newPath });
                return;
            }
        }
        Debug.LogWarning($"[KeyRebinder] '{action.name}' 액션에서 '{group}' 그룹 바인딩을 찾지 못했습니다.");
    }

    [ContextMenu("Reset To Default")]
    public void ResetToDefault()
    {
        if (inputActions == null) return;
        var map = inputActions.FindActionMap(actionMapName);
        map?.RemoveAllBindingOverrides();
    }
}