using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI.Hub
{
    // 메인화면(거점) 배경 안에 놓인 2D 상호작용 오브젝트 (5. UI/UX '메인화면 구성')
    // 1depth 메뉴 항목(HubTab)의 Linked Object에 연결하면 그 항목을 고를 때 외곽 빛으로 강조됨
    // - 마우스를 올리거나 누르는 것을 받으려면 콜라이더가 있어야 하고, 카메라에 Physics Raycaster가 있어야 함
    [RequireComponent(typeof(Collider))]
    public class HubObject : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [Tooltip("강조될 때만 켜지는 외곽 빛")]
        [SerializeField] private GameObject _outlineGlow;

        [Tooltip("선택했을 때 화면이 확대되어 다가갈 위치 (비워 두면 이 오브젝트의 위치)")]
        [SerializeField] private Transform _zoomTarget;

        public event Action HoverStarted;
        public event Action LeftClicked;

        public Vector3 ZoomPosition => _zoomTarget != null ? _zoomTarget.position : transform.position;

        public void SetHighlighted(bool isHighlighted)
        {
            if (_outlineGlow != null) _outlineGlow.SetActive(isHighlighted);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            HoverStarted?.Invoke();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // 우클릭은 뒤로 가기이므로 왼쪽 버튼만 받음
            if (eventData.button == PointerEventData.InputButton.Left) LeftClicked?.Invoke();
        }
    }
}
