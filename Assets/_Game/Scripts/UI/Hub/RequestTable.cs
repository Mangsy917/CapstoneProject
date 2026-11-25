using System;
using UnityEngine;

namespace Game.UI.Hub
{
    public enum RequestLength
    {
        Short,
        Medium,
        Long
    }

    // 의뢰 보드의 데이터 테이블 (3-4. 상점 시스템 '의뢰', 3-1. 레벨 시스템 '레벨 구간별 의뢰 길이 비율', 4. 콘텐츠 '스테이지 테마')
    [CreateAssetMenu(fileName = "D_RequestTable", menuName = "Game/Request Table")]
    public class RequestTable : ScriptableObject
    {
        // 스테이지 배경 하나. 폴라로이드의 사진과 확대 화면의 내용으로 씀
        [Serializable]
        public class PlaceInfo
        {
            [Tooltip("스테이지 배경 이름")]
            [SerializeField] private string _placeName;

            [Tooltip("확대 화면 오른쪽에 적을 스테이지 컨셉")]
            [TextArea]
            [SerializeField] private string _concept;

            [Tooltip("폴라로이드에 들어갈 그림 (정사각형). 비어 있으면 빈 칸으로 보임")]
            [SerializeField] private Sprite _photo;

            public string PlaceName => _placeName;
            public string Concept => _concept;
            public Sprite Photo => _photo;
        }

        [Serializable]
        public class FactionInfo
        {
            [Tooltip("의뢰에 표시할 세력 이름")]
            [SerializeField] private string _factionName;

            [Tooltip("이 세력의 스테이지 배경. 재탐색할 때마다 다른 배경으로 바뀜")]
            [SerializeField] private PlaceInfo[] _placeInfos;

            public string FactionName => _factionName;
            public int PlaceCount => _placeInfos != null ? _placeInfos.Length : 0;

            public PlaceInfo GetPlace(int placeIndex)
            {
                return placeIndex >= 0 && placeIndex < PlaceCount ? _placeInfos[placeIndex] : null;
            }
        }

        [Serializable]
        public class LengthRatio
        {
            [Tooltip("이 레벨부터 적용 (다음 줄의 레벨 전까지)")]
            [Min(1)]
            [SerializeField] private int _minLevel = 1;

            [Min(0)] [SerializeField] private int _shortRatio;
            [Min(0)] [SerializeField] private int _mediumRatio;
            [Min(0)] [SerializeField] private int _longRatio;

            public int MinLevel => _minLevel;

            public RequestLength Pick()
            {
                int value = UnityEngine.Random.Range(0, Mathf.Max(1, _shortRatio + _mediumRatio + _longRatio));
                if (value < _shortRatio) return RequestLength.Short;
                if (value < _shortRatio + _mediumRatio) return RequestLength.Medium;
                return _longRatio > 0 ? RequestLength.Long : RequestLength.Short;
            }
        }

        [Tooltip("의뢰 재탐색 기본 비용(크레딧). 재탐색할 때마다 2배가 됨")]
        [Min(0)]
        [SerializeField] private int _baseRerollCost = 50;

        [Tooltip("의뢰를 주는 세력 (보드의 의뢰 1, 2, 3 순서)")]
        [SerializeField] private FactionInfo[] _factions;

        [Header("메인 의뢰")]
        [SerializeField] private string _mainTitle = "메인 의뢰";
        [SerializeField] private string _mainLength = "2스테이지 (일반 → 보스)";
        [SerializeField] private PlaceInfo _mainPlace;

        [Header("길이 비율")]
        [Tooltip("레벨 구간별 의뢰 길이 비율 (낮은 레벨부터)")]
        [SerializeField] private LengthRatio[] _lengthRatios;

        public int BaseRerollCost => _baseRerollCost;
        public string MainTitle => _mainTitle;
        public string MainLength => _mainLength;
        public PlaceInfo MainPlace => _mainPlace;

        public FactionInfo GetFaction(int factionIndex)
        {
            return _factions != null && factionIndex >= 0 && factionIndex < _factions.Length ? _factions[factionIndex] : null;
        }

        // 플레이어 레벨에 맞는 비율로 의뢰 길이를 뽑음
        public RequestLength PickLength(int level)
        {
            RequestLength length = RequestLength.Short;
            LengthRatio matched = null;

            foreach (LengthRatio ratio in _lengthRatios)
            {
                if (level >= ratio.MinLevel) matched = ratio;
            }

            if (matched != null) length = matched.Pick();
            return length;
        }

        public static string GetLengthName(RequestLength length)
        {
            switch (length)
            {
                case RequestLength.Medium: return "중간";
                case RequestLength.Long: return "긺";
                default: return "짧음";
            }
        }

        public static int GetStageCount(RequestLength length)
        {
            switch (length)
            {
                case RequestLength.Medium: return 5;
                case RequestLength.Long: return 7;
                default: return 3;
            }
        }
    }
}
