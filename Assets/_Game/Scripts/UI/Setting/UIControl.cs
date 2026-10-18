using NUnit.Framework.Internal;
using System;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.InputSystem;

public class UIControl : MonoBehaviour
{
    [SerializeField] private InputActionAsset uiControl;
    [SerializeField] private string actionMapName = "UI";

    private InputAction Up, Down, Left, Right;
    private InputAction LeftPage, RightPage, Select, Cancel, Close;

    public bool uiUp;
    public bool uiDown;
    public bool uiLeft;
    public bool uiRight;
    public bool uiLeftPG;
    public bool uiRightPG;
    public bool uiSelect;
    public bool uiCancel;
    public bool uiClose;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        if (uiControl == null)
        {
            Debug.LogError("[InputReader] uiControl이 할당되지 않았습니다.");
            enabled = false;
            return;
        }

        var map = uiControl.FindActionMap(actionMapName, throwIfNotFound: true);

        Up = map.FindAction("Up");
        Down = map.FindAction("Down");
        Left = map.FindAction("Left");
        Right = map.FindAction("Right");

        LeftPage = map.FindAction("LeftPage");
        RightPage = map.FindAction("RightPage");
        Select = map.FindAction("Select");
        Cancel = map.FindAction("Cancel");

        Close = map.FindAction("BackToGame");
    }

    void Start()
    {
        
    }

    private void OnEnable()
    {
        uiControl?.FindActionMap(actionMapName)?.Enable();
    }

    private void OnDisable()
    {
        uiControl?.FindActionMap(actionMapName)?.Disable();
    }

    // Update is called once per frame
    void Update()
    {
        ReadAndApply();
    }

    private void ReadAndApply()
    {
        bool up = Up != null && Up.IsPressed();
        bool down = Down != null && Down.IsPressed();
        bool left = Left != null && Left.IsPressed();
        bool right = Right != null && Right.IsPressed();

        bool leftPage = LeftPage != null && LeftPage.WasPressedThisFrame();
        bool rightPage = RightPage != null && RightPage.WasPressedThisFrame();
        bool select = Select != null && Select.IsPressed();
        bool cancel = Cancel != null && Cancel.IsPressed();

        bool close = Close != null && Close.WasPressedThisFrame();

        UpdateButton(up, down, left, right, leftPage, rightPage, select, cancel, close);
    }

    private void UpdateButton(bool up, bool down, bool left, bool right, 
                              bool leftPage, bool rightPage, bool select, bool cancel,
                              bool close)
    {
        uiUp = up;
        uiDown = down;
        uiLeft = left;
        uiRight = right;

        uiLeftPG = leftPage;
        uiRightPG = rightPage;
        uiSelect = select;
        uiCancel = cancel;

        uiClose = close;
    }
}