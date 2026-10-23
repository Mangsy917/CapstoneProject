using System;
using UnityEngine;

namespace Game.Character
{
    // 공격 판정 높이
    public enum AttackHeight
    {
        High,   // 상단
        Middle, // 중단
        Low     // 하단
    }

    // 슈퍼아머가 막아줄 공격 높이 (여러 개 선택 가능)
    [Flags]
    public enum AttackHeightMask
    {
        High = 1 << 0,
        Middle = 1 << 1,
        Low = 1 << 2
    }

    // 공격하는 동안 플레이어가 가지는 특성
    public enum AttackTrait
    {
        Throw,      // 잡기 판정
        Projectile, // 장풍 판정
        SuperArmor, // 슈퍼아머 (정해진 높이의 공격을 정해진 횟수만큼 버팀)
        Invincible  // 무적
    }

    // 공격 하나의 속성. Project 창에서 Create > Game > Attack Data로 만들고,
    // CharacterCommand의 커맨드 목록에서 attack 칸에 넣어서 사용
    [CreateAssetMenu(fileName = "D_NewAttack", menuName = "Game/Attack Data")]
    public class AttackData : ScriptableObject
    {
        // 정해진 프레임 구간(시작 ~ 끝) 동안만 적용되는 특성
        [Serializable]
        public class TraitWindow
        {
            [SerializeField] private AttackTrait _trait;

            [Tooltip("특성이 시작되는 프레임 (공격 시작 프레임 = 1)")]
            [SerializeField] private int _startFrame = 1;

            [Tooltip("특성이 끝나는 프레임 (이 프레임까지 포함). 0이면 공격이 끝날 때까지")]
            [SerializeField] private int _endFrame = 0;

            [Tooltip("슈퍼아머일 때만 사용: 버틸 수 있는 공격 높이")]
            [SerializeField] private AttackHeightMask _armorHeights = AttackHeightMask.High;

            [Tooltip("슈퍼아머일 때만 사용: 버틸 수 있는 횟수. 0이면 횟수 제한 없음")]
            [SerializeField] private int _armorHitCount = 1;

            public AttackTrait Trait => _trait;
            public int StartFrame => _startFrame;
            public int EndFrame => _endFrame;
            public AttackHeightMask ArmorHeights => _armorHeights;
            public int ArmorHitCount => _armorHitCount;

            public bool IsActiveAt(int frame)
            {
                return frame >= _startFrame && (_endFrame <= 0 || frame <= _endFrame);
            }

            public void Validate()
            {
                if (_startFrame < 1) _startFrame = 1;
                if (_endFrame < 0) _endFrame = 0;
                if (_endFrame > 0 && _endFrame < _startFrame) _endFrame = _startFrame;
                if (_armorHitCount < 0) _armorHitCount = 0;
            }
        }

        [SerializeField] private string _attackName;

        [Header("판정")]
        [SerializeField] private AttackHeight _height = AttackHeight.High;

        [Header("프레임")]
        [Tooltip("발생 프레임: 공격 판정이 처음 나오는 프레임.\n예) 5면 1~4프레임은 준비, 5프레임부터 공격 판정")]
        [SerializeField] private int _startupFrames = 5;

        [Tooltip("지속 프레임: 공격 판정이 유지되는 프레임 수")]
        [SerializeField] private int _activeFrames = 3;

        [Tooltip("회수 프레임: 공격 판정이 끝난 뒤 다시 움직일 수 있을 때까지의 프레임 수")]
        [SerializeField] private int _recoveryFrames = 10;

        [Header("프레임 구간별 특성")]
        [Tooltip("예) 슈퍼아머 / 시작 1 / 끝 8 / 상단 / 2회 → 1~8프레임 동안 상단 공격을 2번까지 버팀")]
        [SerializeField] private TraitWindow[] _traitWindows = new TraitWindow[0];

        public string AttackName => _attackName;
        public AttackHeight Height => _height;
        public int StartupFrames => _startupFrames;
        public int ActiveFrames => _activeFrames;
        public int RecoveryFrames => _recoveryFrames;
        public TraitWindow[] TraitWindows => _traitWindows;

        // 공격 판정이 나오는 첫 프레임과 마지막 프레임
        public int FirstActiveFrame => _startupFrames;
        public int LastActiveFrame => _startupFrames + _activeFrames - 1;

        // 공격 시작부터 회수가 끝날 때까지의 전체 프레임 수
        public int TotalFrames => LastActiveFrame + _recoveryFrames;

        public bool HasTrait(AttackTrait trait)
        {
            foreach (var window in _traitWindows)
            {
                if (window.Trait == trait) return true;
            }
            return false;
        }

        private void OnValidate()
        {
            if (_startupFrames < 1) _startupFrames = 1;
            if (_activeFrames < 1) _activeFrames = 1;
            if (_recoveryFrames < 0) _recoveryFrames = 0;

            if (_traitWindows == null) return;
            foreach (var window in _traitWindows)
            {
                window?.Validate();
            }
        }
    }
}
