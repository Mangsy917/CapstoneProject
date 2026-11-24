using System.Collections.Generic;
using Game.Character;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 칸으로 나뉜 게이지 바 (체력바 아래의 냉각수 / 가드 게이지, 화면 아래의 오버클럭)
    // - 최대치에 맞춰 칸을 만들고, 남은 양만큼 채움 (4.2칸처럼 딱 떨어지지 않으면 마지막 칸이 좁음)
    // - 줄어든 만큼은 잠깐 밝은 막대로 남았다가 사라짐
    // - 과부하: 채운 칸이 회색이 되고 바탕이 붉게 깜빡이며 글자를 보여줌
    // - 오버클럭: 가득 찬 칸은 밝게, 덜 찬 칸은 어둡게. 모자라서 OD 기술이 안 나가면 붉게 깜빡임
    // - Enemy가 사라졌다가 다시 생성되면 새로 찾음
    public class GaugeBar : MonoBehaviour
    {
        public enum GaugeKind
        {
            Coolant,
            Overclock
        }

        private class GaugeCell
        {
            public RectTransform CellRect;
            public Image Background;
            public RectTransform LossRect;
            public RectTransform FillRect;
            public Image FillImage;
            public float StartAmount;
            public float GB_Size;
        }

        [SerializeField] private GaugeKind _kind = GaugeKind.Coolant;
        [Tooltip("게이지를 보여줄 캐릭터의 태그")]
        [SerializeField] private string GB_targetTag = "Player";
        [Tooltip("체크하면 왼쪽 끝부터 채움 (끄면 오른쪽 끝부터, 왼쪽 체력바와 같은 방향)")]
        [SerializeField] private bool _isFilledFromLeft;
        [Tooltip("칸 사이 간격 (픽셀)")]
        [SerializeField] private float _cellGap = 5f;

        [Header("색")]
        [SerializeField] private Color GB_backgroundColor = new Color(0.1f, 0.12f, 0.14f, 0.85f);
        [SerializeField] private Color _fullColor = new Color(0.18f, 0.95f, 0.85f, 1f);
        [Tooltip("덜 찬 칸 (오버클럭)")]
        [SerializeField] private Color _partialColor = new Color(0.1f, 0.55f, 0.5f, 1f);
        [SerializeField] private Color _lossColor = new Color(1f, 1f, 1f, 0.7f);
        [SerializeField] private Color _overloadColor = new Color(0.55f, 0.55f, 0.6f, 1f);
        [SerializeField] private Color _warningColor = new Color(0.75f, 0.1f, 0.1f, 0.9f);

        [Header("글자")]
        [Tooltip("과부하 중에 보일 글자 (없으면 비워둠)")]
        [SerializeField] private TMP_Text _overloadText;
        [Tooltip("가득 찬 칸 수를 보여줄 글자 (없으면 비워둠)")]
        [SerializeField] private TMP_Text _countText;

        [Header("움직임")]
        [Tooltip("줄어든 자리가 사라지는 속도 (1초에 몇 칸)")]
        [SerializeField] private float _lossShrinkSpeed = 4f;
        [Tooltip("1초에 깜빡이는 횟수")]
        [SerializeField] private float _blinkSpeed = 4f;
        [Tooltip("오버클럭이 모자랄 때 깜빡이는 시간(초)")]
        [SerializeField] private float _lackBlinkTime = 0.6f;

        private readonly List<GaugeCell> _cells = new List<GaugeCell>();
        private CharacterGauge _targetGauge;
        private float _builtMaximum = -1f;
        private float _lossAmount;
        private float _lackTimeLeft;
        private int _shownCount = -1;

        private float CellSize => _kind == GaugeKind.Coolant ? GaugeData.CoolantPerCell : GaugeData.OverclockPerCell;

        private void OnDisable()
        {
            if (_targetGauge != null) _targetGauge.OverclockLacked -= PlayLackBlink;
            _targetGauge = null;
        }

        private void Update()
        {
            if (_targetGauge == null) FindGauge();
            if (_targetGauge == null) return;

            float maximum = _kind == GaugeKind.Coolant ? _targetGauge.MaxCoolant : _targetGauge.MaxOverclock;
            float amount = _kind == GaugeKind.Coolant ? _targetGauge.Coolant : _targetGauge.Overclock;
            if (!Mathf.Approximately(maximum, _builtMaximum)) BuildCells(maximum, amount);

            // Time.deltaTime을 써서 설정창이 열려 멈춘 동안에는 움직이지 않음
            _lossAmount = Mathf.Max(amount, _lossAmount - _lossShrinkSpeed * CellSize * Time.deltaTime);
            if (_lackTimeLeft > 0f) _lackTimeLeft -= Time.deltaTime;

            DrawCells(amount);
            DrawTexts(amount);
        }

        private void FindGauge()
        {
            GameObject target = GameObject.FindGameObjectWithTag(GB_targetTag);
            if (target == null || !target.TryGetComponent(out CharacterGauge gauge)) return;

            _targetGauge = gauge;
            _targetGauge.OverclockLacked += PlayLackBlink;
            _builtMaximum = -1f;
        }

        private void PlayLackBlink()
        {
            _lackTimeLeft = _lackBlinkTime;
        }

        private void BuildCells(float maximum, float amount)
        {
            foreach (GaugeCell cell in _cells) Destroy(cell.CellRect.gameObject);
            _cells.Clear();

            _builtMaximum = maximum;
            _lossAmount = amount;
            for (float start = 0f; start < maximum - 0.01f; start += CellSize)
            {
                _cells.Add(CreateCell(start, Mathf.Min(CellSize, maximum - start), maximum));
            }
        }

        private GaugeCell CreateCell(float startAmount, float size, float maximum)
        {
            GaugeCell cell = new GaugeCell { StartAmount = startAmount, GB_Size = size };
            cell.Background = CreateImage("Cell" + _cells.Count, transform, GB_backgroundColor);
            cell.CellRect = cell.Background.rectTransform;
            SetRange(cell.CellRect, startAmount / maximum, (startAmount + size) / maximum);
            cell.CellRect.offsetMin = new Vector2(_cellGap * 0.5f, 0f);
            cell.CellRect.offsetMax = new Vector2(-_cellGap * 0.5f, 0f);

            cell.LossRect = CreateImage("Loss", cell.CellRect, _lossColor).rectTransform;
            cell.FillImage = CreateImage("Fill", cell.CellRect, _fullColor);
            cell.FillRect = cell.FillImage.rectTransform;
            return cell;
        }

        private static Image CreateImage(string objectName, Transform parent, Color color)
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            imageObject.layer = parent.gameObject.layer;
            imageObject.transform.SetParent(parent, false);
            imageObject.transform.SetAsLastSibling();
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // 게이지가 차는 방향에 맞춰 start ~ end 비율만큼의 자리에 놓음
        private void SetRange(RectTransform rect, float start, float end)
        {
            rect.anchorMin = new Vector2(_isFilledFromLeft ? start : 1f - end, 0f);
            rect.anchorMax = new Vector2(_isFilledFromLeft ? end : 1f - start, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void DrawCells(float amount)
        {
            bool isOverloaded = _kind == GaugeKind.Coolant && _targetGauge.IsOverloaded;
            bool isWarning = isOverloaded || (_kind == GaugeKind.Overclock && _lackTimeLeft > 0f);
            float blink = Mathf.PingPong(Time.time * _blinkSpeed * 2f, 1f);
            Color background = isWarning ? Color.Lerp(GB_backgroundColor, _warningColor, blink) : GB_backgroundColor;

            foreach (GaugeCell cell in _cells)
            {
                float ratio = Mathf.Clamp01((amount - cell.StartAmount) / cell.GB_Size);
                SetRange(cell.FillRect, 0f, ratio);
                SetRange(cell.LossRect, 0f, Mathf.Clamp01((_lossAmount - cell.StartAmount) / cell.GB_Size));

                Color fillColor = _kind == GaugeKind.Overclock && ratio < 1f ? _partialColor : _fullColor;
                cell.FillImage.color = isOverloaded ? _overloadColor : fillColor;
                cell.Background.color = background;
            }
        }

        private void DrawTexts(float amount)
        {
            if (_overloadText != null) _overloadText.enabled = _kind == GaugeKind.Coolant && _targetGauge.IsOverloaded;
            if (_countText == null) return;

            _countText.color = _lackTimeLeft > 0f ? _warningColor : Color.white;
            int count = Mathf.FloorToInt(amount / CellSize + 0.001f);
            if (count == _shownCount) return;
            _shownCount = count;
            _countText.text = count.ToString();
        }
    }
}
