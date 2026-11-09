using System;
using System.Collections.Generic;
using Game.Common;
using UnityEngine;
using UnityEngine.Serialization;

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

    // 맞은 쪽이 재생할 피격 모션
    public enum HitMotion
    {
        Auto,    // 자동: 상단 공격은 Middle, 중단 공격은 Top2Low, 하단 공격은 맞은 피격 상자(Top / Middle / Bottom)에 따라 정함
        Top,     // 상단 피격 (TopHit)
        Middle,  // 중단 피격 (MiddleHit)
        Low,     // 하단 피격 (LowHit)
        Top2Low, // 위에서 아래로 맞음 (Top2LowHit)
        Low2Top  // 아래에서 위로 올려 맞음 (Low2TopHit, 승룡권 등)
    }

    // 공격하는 동안 플레이어가 가지는 특성
    public enum AttackTrait
    {
        Throw,      // 잡기 판정
        Projectile, // 장풍 판정
        SuperArmor, // 슈퍼아머 (정해진 높이의 공격을 정해진 횟수만큼 버팀)
        Invincible  // 무적
    }

    // 공격 프레임 목록의 한 칸이 어떤 프레임인지
    public enum FrameType
    {
        Startup, // 발생: 공격 판정이 나오기 전
        Active,  // 공격 판정 (지속)
        Recovery // 회수: 공격 판정이 끝난 뒤 다시 움직일 수 있을 때까지
    }

    // 공격 한 번의 프레임. AttackData의 프레임 목록(발생 / 공격 판정 / 회수를 순서대로 적은 것)으로 만듦
    // - 발생의 마지막 프레임과 바로 뒤 공격 판정의 첫 프레임은 같은 프레임 (겹침)
    //   예) 발생 5 / 공격 판정 3 / 회수 10 → 1~5 발생, 5~7 공격 판정, 8~17 회수 (전체 17프레임)
    // - 공격 판정 칸 하나가 한 타. 다단 공격은 발생 / 공격 판정을 이어서 적음
    //   예) 발생 5 / 판정 2 / 발생 3 / 판정 2 / 회수 10 → 5~6 1타, 7~9 발생, 9~10 2타, 11~20 회수
    // - 발생 없이 공격 판정 칸이 바로 이어지면 앞 칸이 끝난 다음 프레임부터 판정
    public readonly struct AttackFrames
    {
        // 목록의 칸마다 종류와 차지하는 첫 프레임 / 마지막 프레임 (목록 순서)
        private readonly FrameType[] _types;
        private readonly int[] _firstFrames;
        private readonly int[] _lastFrames;

        // 타마다(1타 = 0번) 공격 판정 칸과 그 첫 프레임 / 마지막 프레임
        private readonly AttackData.FrameSegment[] _activeSegments;
        private readonly int[] _firstActiveFrames;
        private readonly int[] _lastActiveFrames;

        public AttackFrames(AttackData.FrameSegment[] segments)
        {
            int count = segments?.Length ?? 0;
            _types = new FrameType[count];
            _firstFrames = new int[count];
            _lastFrames = new int[count];
            var activeSegments = new List<AttackData.FrameSegment>();
            var firstActiveFrames = new List<int>();
            var lastActiveFrames = new List<int>();

            int lastFrame = 0; // 앞 칸까지 차지한 마지막 프레임
            bool isAfterStartup = false;
            for (int i = 0; i < count; i++)
            {
                AttackData.FrameSegment segment = segments[i];
                if (segment == null)
                {
                    // 빈 칸은 프레임을 차지하지 않음
                    _types[i] = FrameType.Startup;
                    _firstFrames[i] = lastFrame + 1;
                    _lastFrames[i] = lastFrame;
                    continue;
                }

                // 발생 바로 뒤의 공격 판정은 발생의 마지막 프레임과 겹침
                int firstFrame = segment.FrameType == FrameType.Active && isAfterStartup ? lastFrame : lastFrame + 1;
                int segmentLastFrame = firstFrame + Mathf.Max(1, segment.AD_FrameCount) - 1;
                _types[i] = segment.FrameType;
                _firstFrames[i] = firstFrame;
                _lastFrames[i] = segmentLastFrame;

                if (segment.FrameType == FrameType.Active)
                {
                    activeSegments.Add(segment);
                    firstActiveFrames.Add(firstFrame);
                    lastActiveFrames.Add(segmentLastFrame);
                }

                isAfterStartup = segment.FrameType == FrameType.Startup;
                lastFrame = segmentLastFrame;
            }

            _activeSegments = activeSegments.ToArray();
            _firstActiveFrames = firstActiveFrames.ToArray();
            _lastActiveFrames = lastActiveFrames.ToArray();
            TotalFrames = lastFrame;
        }

        /// <summary>공격 판정 횟수 (공격 판정 칸의 수)</summary>
        public int AD_HitCount => _firstActiveFrames?.Length ?? 0;

        /// <summary>hitIndex번째 타(1타 = 0)의 공격 판정 첫 프레임 (그런 타가 없으면 0)</summary>
        public int GetFirstActiveFrame(int hitIndex)
        {
            return hitIndex >= 0 && hitIndex < AD_HitCount ? _firstActiveFrames[hitIndex] : 0;
        }

        /// <summary>hitIndex번째 타(1타 = 0)의 공격 판정 마지막 프레임 (그런 타가 없으면 0)</summary>
        public int GetLastActiveFrame(int hitIndex)
        {
            return hitIndex >= 0 && hitIndex < AD_HitCount ? _lastActiveFrames[hitIndex] : 0;
        }

        /// <summary>hitIndex번째 타(1타 = 0)의 공격 판정 칸 (그런 타가 없으면 null)</summary>
        public AttackData.FrameSegment GetActiveSegment(int hitIndex)
        {
            return hitIndex >= 0 && hitIndex < AD_HitCount ? _activeSegments[hitIndex] : null;
        }

        /// <summary>공격 시작부터 frame번째 프레임이 어떤 프레임인지 (발생과 공격 판정이 겹치는 프레임은 공격 판정, 목록이 끝난 뒤는 회수)</summary>
        public FrameType GetFrameType(int frame)
        {
            if (FindHitIndex(frame) >= 0) return FrameType.Active;
            int count = _types?.Length ?? 0;
            for (int i = 0; i < count; i++)
            {
                if (frame >= _firstFrames[i] && frame <= _lastFrames[i]) return _types[i];
            }
            return frame < 1 ? FrameType.Startup : FrameType.Recovery;
        }

        /// <summary>frame이 몇 번째 타(1타 = 0)의 지속 프레임인지. 어느 타의 지속도 아니면 -1</summary>
        public int FindHitIndex(int frame)
        {
            for (int i = 0; i < AD_HitCount; i++)
            {
                if (frame >= GetFirstActiveFrame(i) && frame <= GetLastActiveFrame(i)) return i;
            }
            return -1;
        }

        // 공격 판정이 나오는 첫 프레임(1타)과 마지막 프레임(마지막 타)
        public int FirstActiveFrame => GetFirstActiveFrame(0);
        public int LastActiveFrame => GetLastActiveFrame(AD_HitCount - 1);

        // 공격 시작부터 목록의 마지막 칸이 끝날 때까지의 전체 프레임 수
        public int TotalFrames { get; }

        // 마지막 타의 공격 판정이 끝난 뒤 공격이 끝날 때까지의 프레임 수
        public int Recovery => TotalFrames - LastActiveFrame;
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

        // 공격하는 동안 더 생기는 피격 상자 (뻗은 팔, 다리 등)
        // 켜지는 구간은 발생 / 지속 프레임 기준이라서, 누른 버튼마다 프레임이 달라도 같은 모습에 맞춰짐
        [Serializable]
        public class ExtraHitbox
        {
            [Tooltip("피격 상자 크기 (월드 단위)")]
            [SerializeField] private Vector2 AD_size = new Vector2(0.6f, 0.3f);

            [Tooltip("피격 상자 중심 위치 (캐릭터 위치 기준, x는 바라보는 쪽이 +)")]
            [SerializeField] private Vector2 AD_offset = new Vector2(0.6f, 0.4f);

            [Tooltip("발생 프레임 몇 프레임 전부터 켤지 (0이면 발생 프레임부터)")]
            [SerializeField] private int _framesBeforeActive = 2;

            [Tooltip("지속 프레임이 끝난 뒤 몇 프레임까지 켜 둘지 (회수 중에도 팔다리가 뻗어 있는 동안)")]
            [SerializeField] private int _framesAfterActive = 6;

            public Vector2 Size => AD_size;
            public Vector2 Offset => AD_offset;

            /// <summary>공격 시작부터 frame 프레임째에 켜져 있는지</summary>
            public bool IsActiveAt(int frame, AttackFrames frames)
            {
                return frame >= frames.FirstActiveFrame - _framesBeforeActive && frame <= frames.LastActiveFrame + _framesAfterActive;
            }

            public void Validate()
            {
                if (AD_size.x < 0f) AD_size.x = 0f;
                if (AD_size.y < 0f) AD_size.y = 0f;
                if (_framesBeforeActive < 0) _framesBeforeActive = 0;
                if (_framesAfterActive < 0) _framesAfterActive = 0;
            }
        }

        // 지속 프레임 중 일부 구간에만 쓰는 공격 상자 (예: 2H는 앞쪽에서 머리 위로 올라감)
        [Serializable]
        public class AttackBoxWindow
        {
            [Tooltip("공격 상자 크기 (월드 단위)")]
            [SerializeField] private Vector2 AD_size = new Vector2(0.6f, 0.4f);

            [Tooltip("공격 상자 중심 위치 (캐릭터 위치 기준, x는 바라보는 쪽이 +)")]
            [SerializeField] private Vector2 AD_offset = new Vector2(0.8f, 0.3f);

            [Tooltip("지속 프레임 중 몇 번째 프레임부터 쓸지 (지속 첫 프레임 = 1)")]
            [SerializeField] private int _firstActiveFrame = 1;

            [Tooltip("지속 프레임 중 몇 번째 프레임까지 쓸지 (이 프레임까지 포함). 0이면 지속이 끝날 때까지")]
            [SerializeField] private int _lastActiveFrame = 0;

            public Vector2 Size => AD_size;
            public Vector2 Offset => AD_offset;

            /// <summary>지속 activeFrame번째 프레임에 이 공격 상자를 쓰는지</summary>
            public bool IsActiveAt(int activeFrame)
            {
                return activeFrame >= _firstActiveFrame && (_lastActiveFrame <= 0 || activeFrame <= _lastActiveFrame);
            }

            public void Validate()
            {
                if (AD_size.x < 0f) AD_size.x = 0f;
                if (AD_size.y < 0f) AD_size.y = 0f;
                if (_firstActiveFrame < 1) _firstActiveFrame = 1;
                if (_lastActiveFrame < 0) _lastActiveFrame = 0;
            }
        }

        // 히트 종류(노말 히트 / 카운터 / 퍼니시 카운터)마다 맞은 쪽이 받는 결과
        [Serializable]
        public class HitTypeResult
        {
            [Tooltip("데미지에 곱할 배율 (1이면 기술의 데미지 그대로)")]
            [SerializeField] private float _damageRate = 1f;

            [Tooltip("체크하면 이 히트 종류로 맞았을 때 아래의 히트스톱을 씀 (끄면 기술의 히트스톱)")]
            [SerializeField] private bool _shouldOverrideHitStop = false;

            [Tooltip("히트스톱: 이 히트 종류로 맞았을 때 때린 쪽과 맞은 쪽이 멈추는 프레임 수 (60프레임 = 1초). 0이면 멈추지 않음")]
            [ShowIf(nameof(_shouldOverrideHitStop))]
            [SerializeField] private int AD_hitStopFrames = 6;

            [Tooltip("히트 시 유리 프레임에 더할 피격 경직 프레임 수. 다운되면 기상이 이만큼 늦어짐")]
            [SerializeField] private int _extraHitStunFrames = 0;

            [Tooltip("체크하면 이 히트 종류로 맞았을 때 피격 경직을 아래 프레임 수로 직접 정함 (끄면 히트 시 유리 프레임 + 위의 추가 경직으로 계산)\n" +
                     "다운되면 이 프레임 수가 쓰러지는 프레임 + 다운 프레임 전체가 됨")]
            [SerializeField] private bool _shouldOverrideHitStun = false;

            [Tooltip("땅에 서 있는 상대가 맞았을 때의 피격 경직 프레임 수")]
            [ShowIf(nameof(_shouldOverrideHitStun))]
            [SerializeField] private int _standHitStunFrames = 20;

            [Tooltip("공중에 있는 상대가 맞았을 때(아래의 띄울 높이로 띄우는 경우 포함)의 피격 경직 프레임 수")]
            [ShowIf(nameof(_shouldOverrideHitStun))]
            [SerializeField] private int _airHitStunFrames = 20;

            [Tooltip("체크하면 이 히트 종류로 맞았을 때 아래의 히트백을 씀 (끄면 기술의 히트백)")]
            [SerializeField] private bool _shouldOverrideHitBack = false;

            [Tooltip("이 히트 종류로 맞았을 때 뒤로 밀려나는 거리 (월드 단위)")]
            [SerializeField] private float _hitBackDistance = 0.5f;

            [Tooltip("이 히트 종류로 맞았을 때 히트백으로 밀려나는 데 걸리는 프레임 수")]
            [SerializeField] private int _hitBackFrames = 8;

            [Tooltip("체크하면 맞은 뒤 다운됨.\n아래의 쓰러지는 프레임 동안은 피격 경직(추가타 가능), 그 뒤 기상할 때까지는 다운(무적, 조작 불가)")]
            [SerializeField] private bool _shouldKnockDown = false;

            [Tooltip("다운될 때: 맞은 뒤 쓰러지는 동안의 피격 경직 프레임 수 (이 동안은 추가타를 맞을 수 있음).\n그 뒤 기상할 때까지는 다운")]
            [SerializeField] private int _fallFrames = 20;

            [Tooltip("다운될 때: 쓰러진 뒤 기상할 때까지 다운되어 있는 프레임 수 (60프레임 = 1초).\n" +
                     "0이면 자동: 기상 시점이 히트 시 유리 프레임(위키의 KD 값) + 위의 추가 경직이 되도록 계산함")]
            [SerializeField] private int _downFrames = 0;

            [Tooltip("맞은 상대를 공중으로 띄울 높이 (0이면 띄우지 않음).\n피격 경직 동안(다운되면 쓰러지는 프레임 동안) 포물선을 그리고, 끝나는 프레임에 착지함")]
            [SerializeField] private float _airborneHeight = 0f;

            [Tooltip("이 히트 종류로 맞았을 때 재생할 피격 모션. 자동이면 기술의 피격 모션(위의 Hit Motion)을 씀")]
            [SerializeField] private HitMotion _hitMotion = HitMotion.Auto;

            public HitTypeResult() { }

            public HitTypeResult(float damageRate, int extraHitStunFrames)
            {
                _damageRate = damageRate;
                _extraHitStunFrames = extraHitStunFrames;
            }

            public float DamageRate => _damageRate;
            public bool ShouldOverrideHitStop => _shouldOverrideHitStop;
            public int AD_HitStopFrames => AD_hitStopFrames;
            public int ExtraHitStunFrames => _extraHitStunFrames;
            public bool ShouldOverrideHitStun => _shouldOverrideHitStun;
            public int StandHitStunFrames => _standHitStunFrames;
            public int AirHitStunFrames => _airHitStunFrames;
            public bool ShouldOverrideHitBack => _shouldOverrideHitBack;
            public float HitBackDistance => _hitBackDistance;
            public int HitBackFrames => _hitBackFrames;
            public bool ShouldKnockDown => _shouldKnockDown;
            public int FallFrames => _fallFrames;
            public int DownFrames => _downFrames;
            public float AirborneHeight => _airborneHeight;
            public HitMotion HitMotion => _hitMotion;

            public void Validate()
            {
                if (_hitBackDistance < 0f) _hitBackDistance = 0f;
                if (_hitBackFrames < 1) _hitBackFrames = 1;
                if (_downFrames < 0) _downFrames = 0;
                if (_airborneHeight < 0f) _airborneHeight = 0f;
                if (_damageRate < 0f) _damageRate = 0f;
                if (_extraHitStunFrames < 0) _extraHitStunFrames = 0;
                if (_fallFrames < 1) _fallFrames = 1;
                if (AD_hitStopFrames < 0) AD_hitStopFrames = 0;
                if (_standHitStunFrames < 1) _standHitStunFrames = 1;
                if (_airHitStunFrames < 1) _airHitStunFrames = 1;
            }
        }

        // 프레임 목록의 한 칸: 어떤 프레임(발생 / 공격 판정 / 회수)이 몇 프레임인지
        // 공격 판정 칸 하나가 한 타이고, 그 타의 데미지와 공격 상자를 따로 정할 수 있음
        [Serializable]
        public class FrameSegment
        {
            [Tooltip("어떤 프레임인지 (발생 / 공격 판정 / 회수)")]
            [SerializeField] private FrameType _frameType = FrameType.Startup;

            [Tooltip("프레임 수. 발생의 마지막 프레임과 바로 뒤 공격 판정의 첫 프레임은 겹침\n" +
                     "예) 발생 5 / 공격 판정 3 / 회수 10 → 1~5 발생, 5~7 공격 판정, 8~17 회수")]
            [SerializeField] private int AD_frameCount = 1;

            [Tooltip("이 타의 데미지. 0이면 기술의 데미지 (버튼별 데미지가 있으면 그 값)")]
            [ShowIf(nameof(_frameType), FrameType.Active)]
            [SerializeField] private int AD_damage = 0;

            [Tooltip("이 타의 공격 상자 크기. 0이면 기술의 공격 상자 (버튼별 공격 상자가 있으면 그 값)")]
            [ShowIf(nameof(_frameType), FrameType.Active)]
            [SerializeField] private Vector2 AD_attackBoxSize = Vector2.zero;

            [Tooltip("이 타의 공격 상자 중심 위치 (크기가 0이 아닐 때만 사용)")]
            [ShowIf(nameof(_frameType), FrameType.Active)]
            [SerializeField] private Vector2 AD_attackBoxOffset = Vector2.zero;

            public FrameSegment() { }

            public FrameSegment(FrameType frameType, int frameCount)
            {
                _frameType = frameType;
                AD_frameCount = frameCount;
            }

            public FrameType FrameType => _frameType;
            public int AD_FrameCount => AD_frameCount;
            public int Damage => AD_damage;
            public bool HasAttackBox => AD_attackBoxSize.x > 0f && AD_attackBoxSize.y > 0f;
            public Vector2 AttackBoxSize => AD_attackBoxSize;
            public Vector2 AttackBoxOffset => AD_attackBoxOffset;

            public void Validate()
            {
                if (AD_frameCount < 1) AD_frameCount = 1;
                if (AD_damage < 0) AD_damage = 0;
                if (AD_attackBoxSize.x < 0f) AD_attackBoxSize.x = 0f;
                if (AD_attackBoxSize.y < 0f) AD_attackBoxSize.y = 0f;
            }
        }

        // 누른 버튼마다 따로 정하는 프레임 (예: 236L, 236M, 236H)
        [Serializable]
        public class ButtonFrames
        {
            [Tooltip("이 버튼으로 기술이 나갔을 때 아래 프레임을 사용 (하나만 고름)")]
            [SerializeField] private CharacterCommand.AttackButtonType _button = CharacterCommand.AttackButtonType.L;

            [Tooltip("이 버튼으로 나갔을 때의 프레임 목록 (발생 / 공격 판정 / 회수를 순서대로). 비워두면 기술의 기본 프레임 목록 사용")]
            [SerializeField] private FrameSegment[] _frames = new FrameSegment[0];

            [Tooltip("이 버튼으로 나갔을 때의 데미지. 0이면 위의 기본 데미지 사용")]
            [SerializeField] private int AD_damage = 0;

            [Tooltip("이 버튼으로 나갔을 때 투사체가 1프레임에 날아가는 거리. 0이면 기술의 투사체 속도 사용")]
            [SerializeField] private float _projectileDistancePerFrame = 0f;

            [Tooltip("이 버튼으로 나갔을 때의 공격 상자 크기. 0이면 위의 기본 공격 상자 사용")]
            [SerializeField] private Vector2 AD_attackBoxSize = Vector2.zero;

            [Tooltip("이 버튼으로 나갔을 때의 공격 상자 중심 위치 (크기가 0이 아닐 때만 사용)")]
            [SerializeField] private Vector2 AD_attackBoxOffset = Vector2.zero;

            [Tooltip("이 버튼으로 나갔을 때 공격 시작부터 이 프레임까지 완전 무적 (피격 상자가 모두 꺼짐). 0이면 무적 없음.\n예) 8이면 1~8프레임 무적")]
            [SerializeField] private int _invincibleFrames = 0;

            [Tooltip("체크하면 이 버튼으로 나갔을 때 아래의 히트 / 가드 시 유리 프레임을 씀 (끄면 기술의 기본 값)")]
            [SerializeField] private bool _shouldOverrideAdvantage = false;

            [Tooltip("이 버튼으로 나갔을 때의 히트 시 유리 프레임 (위키의 Hit 값)")]
            [SerializeField] private int _hitAdvantage = 0;

            [Tooltip("이 버튼으로 나갔을 때의 가드 시 유리 프레임 (위키의 Block 값)")]
            [SerializeField] private int _blockAdvantage = 0;

            [Tooltip("이 버튼으로 나갔을 때의 히트스톱 프레임 수. 0이면 기술의 히트스톱 사용")]
            [SerializeField] private int AD_hitStopFrames = 0;

            [Tooltip("이 버튼으로 나갔을 때 발생 동안 앞으로 나가는 전체 거리. 0이면 기술의 전진 거리 사용")]
            [SerializeField] private float AD_forwardMoveDistance = 0f;

            public CharacterCommand.AttackButtonType Button => _button;
            public float AD_ForwardMoveDistance => AD_forwardMoveDistance;
            public int AD_HitStopFrames => AD_hitStopFrames;
            public bool ShouldOverrideAdvantage => _shouldOverrideAdvantage;
            public int HitAdvantage => _hitAdvantage;
            public int BlockAdvantage => _blockAdvantage;
            public FrameSegment[] Frames => _frames;
            public bool HasFrames => _frames != null && _frames.Length > 0;
            public int Damage => AD_damage;
            public float ProjectileDistancePerFrame => _projectileDistancePerFrame;
            public bool HasAttackBox => AD_attackBoxSize.x > 0f && AD_attackBoxSize.y > 0f;
            public Vector2 AttackBoxSize => AD_attackBoxSize;
            public Vector2 AttackBoxOffset => AD_attackBoxOffset;
            public int InvincibleFrames => _invincibleFrames;

            public void Validate()
            {
                if (AD_damage < 0) AD_damage = 0;
                if (AD_hitStopFrames < 0) AD_hitStopFrames = 0;
                if (_projectileDistancePerFrame < 0f) _projectileDistancePerFrame = 0f;
                if (AD_attackBoxSize.x < 0f) AD_attackBoxSize.x = 0f;
                if (AD_attackBoxSize.y < 0f) AD_attackBoxSize.y = 0f;
                if (_invincibleFrames < 0) _invincibleFrames = 0;
                if (AD_forwardMoveDistance < 0f) AD_forwardMoveDistance = 0f;
                ValidateFrames(_frames);
            }
        }

        [SerializeField] private string _attackName;

        [Header("판정")]
        [SerializeField] private AttackHeight _height = AttackHeight.High;

        [Tooltip("맞혔을 때 상대 체력을 줄이는 양 (투사체도 이 값을 씀)")]
        [SerializeField] private int AD_damage = 500;

        [Tooltip("공격 상자 크기 (월드 단위). 지속 프레임 동안 상대의 피격 상자(Hitbox)와 겹치면 맞음.\n0이면 직접 맞히는 판정이 없음 (장풍만 쓰는 기술 등)")]
        [FormerlySerializedAs("AD_hitboxSize")]
        [SerializeField] private Vector2 AD_attackBoxSize = new Vector2(0.8f, 0.4f);

        [Tooltip("공격 상자 중심 위치 (캐릭터 위치 기준, x는 바라보는 쪽이 +)")]
        [FormerlySerializedAs("AD_hitboxOffset")]
        [SerializeField] private Vector2 AD_attackBoxOffset = new Vector2(0.8f, 0.3f);

        [Tooltip("지속 프레임 구간마다 다른 공격 상자 (공격 동작에 따라 상자가 움직이는 기술).\n비워두면 위의 공격 상자 하나를 지속 내내 씀 (버튼별 공격 상자보다 먼저 적용)\n다단 공격이면 타마다 그 타의 지속 첫 프레임을 1로 셈")]
        [SerializeField] private AttackBoxWindow[] _attackBoxWindows = new AttackBoxWindow[0];

        [Header("히트")]
        [Tooltip("히트 시 유리 프레임 (위키의 Hit 값). 지속 첫 프레임(장풍은 투사체가 나오는 프레임)에 맞았을 때 기준이며,\n피격 경직 = 그때 남은 공격 프레임 + 이 값 (늦게 맞혀도 경직은 같아서 그만큼 더 유리해짐)")]
        [SerializeField] private int _hitAdvantage = 3;

        [Tooltip("맞은 상대가 뒤로 밀려나는 거리 (월드 단위). 상대가 벽에 붙어 있으면 대신 때린 쪽이 밀려남 (장풍은 제외)")]
        [SerializeField] private float _hitBackDistance = 0.5f;

        [Tooltip("히트백으로 밀려나는 데 걸리는 프레임 수")]
        [SerializeField] private int _hitBackFrames = 8;

        [Tooltip("히트스톱: 맞혔을 때 때린 쪽과 맞은 쪽이 멈추는 프레임 수 (60프레임 = 1초, 6이면 0.1초). 0이면 멈추지 않음\n" +
                 "게임 시간은 그대로 흐르고 두 캐릭터만 멈춤. 맞은 쪽의 피격 경직은 멈춤이 풀린 뒤부터 줄어듦 (장풍은 멈추지 않음)")]
        [SerializeField] private int _hitStopFrames = 6;

        [Tooltip("맞은 쪽이 재생할 피격 모션. 자동이면 상단 공격은 MiddleHit, 중단 공격은 Top2Low, 하단 공격은 맞은 피격 상자 위치에 따라 정함")]
        [SerializeField] private HitMotion _hitMotion = HitMotion.Auto;

        [Header("가드")]
        [Tooltip("가드 시 유리 프레임 (위키의 Block 값, 보통 음수). 히트 시 유리 프레임과 같은 기준으로 가드 경직을 정함")]
        [SerializeField] private int _blockAdvantage = -2;

        [Tooltip("가드한 상대가 뒤로 밀려나는 거리 (월드 단위). 상대가 벽에 붙어 있으면 대신 때린 쪽이 밀려남 (장풍은 제외)")]
        [SerializeField] private float _guardBackDistance = 0.4f;

        [Tooltip("가드백으로 밀려나는 데 걸리는 프레임 수")]
        [SerializeField] private int _guardBackFrames = 8;

        [Header("히트 종류별 결과 (노말 히트 / 카운터 / 퍼니시 카운터)")]
        [Tooltip("노말 히트: 상대가 공격 중이 아닐 때 맞았을 때 (콤보의 2타부터도 노말 히트)")]
        [SerializeField] private HitTypeResult _normalHitResult = new HitTypeResult(damageRate: 1f, extraHitStunFrames: 0);

        [Tooltip("카운터: 콤보의 첫 타가 상대의 공격 발생 / 지속 중에 맞았을 때")]
        [SerializeField] private HitTypeResult _counterHitResult = new HitTypeResult(damageRate: 1.2f, extraHitStunFrames: 2);

        [Tooltip("퍼니시 카운터: 콤보의 첫 타가 상대의 공격 회수 / 착지 경직 중에 맞았을 때")]
        [SerializeField] private HitTypeResult _punishCounterResult = new HitTypeResult(damageRate: 1.2f, extraHitStunFrames: 4);

        [Header("공격 중 피격 상자")]
        [Tooltip("체크하면 공격하는 동안 Top 피격 상자를 끔 (앉아서 쓰는 공격 등)")]
        [SerializeField] private bool _shouldHideTopHitbox = false;

        [Tooltip("공격하는 동안 더 생기는 피격 상자 (뻗은 팔, 다리 등). 정한 프레임 구간에만 켜짐")]
        [SerializeField] private ExtraHitbox[] _extraHitboxes = new ExtraHitbox[0];

        [Header("프레임")]
        [Tooltip("공격 프레임 목록. 칸마다 어떤 프레임(발생 / 공격 판정 / 회수)이 몇 프레임인지 순서대로 적음\n" +
                 "발생의 마지막 프레임과 바로 뒤 공격 판정의 첫 프레임은 겹침. 예) 발생 5 / 공격 판정 3 / 회수 10 → 1~5 발생, 5~7 공격 판정, 8~17 회수\n" +
                 "다단 공격은 발생 / 공격 판정을 이어서 적음 (공격 판정 칸마다 한 번씩 맞힐 수 있음)\n" +
                 "예) 발생 5 / 판정 2 / 발생 3 / 판정 2 / 회수 10 → 5~6 1타, 9~10 2타")]
        [SerializeField] private FrameSegment[] _frames =
        {
            new FrameSegment(FrameType.Startup, 5),
            new FrameSegment(FrameType.Active, 3),
            new FrameSegment(FrameType.Recovery, 10)
        };

        [Tooltip("누른 버튼마다 프레임을 다르게 할 때 사용 (예: 236을 L / M / H / EX로 썼을 때).\n목록에 없는 버튼으로 나가면 위의 프레임 목록을 사용")]
        [SerializeField] private ButtonFrames[] _buttonFrames = new ButtonFrames[0];

        [Header("프레임 구간별 특성")]
        [Tooltip("예) 슈퍼아머 / 시작 1 / 끝 8 / 상단 / 2회 → 1~8프레임 동안 상단 공격을 2번까지 버팀")]
        [SerializeField] private TraitWindow[] _traitWindows = new TraitWindow[0];

        [Header("전진")]
        [Tooltip("땅에서 쓴 공격이 발생 동안(공격 시작 ~ 첫 공격 판정 직전) 앞으로 나가는 전체 거리. 0이면 전진하지 않음.\n" +
                 "매 프레임 같은 거리만큼 나가므로 발생이 짧은 버튼일수록 빠르게 나감 (버튼별 전진 거리로 따로 정할 수 있음)")]
        [SerializeField] private float _forwardMoveDistance = 0f;

        [Header("공중으로 뛰어오르기")]
        [Tooltip("체크하면 공격 중에 공중으로 뛰어오름 (승룡권 등). 체크하면 아래에 뛰어오르는 설정이 나옴")]
        [SerializeField] private bool _isLaunchAttack = false;
        [Tooltip("이 프레임에 앞으로 뛰어오름 (공격 시작 프레임 = 1). 0이면 뛰어오르지 않음")]
        [ShowIf(nameof(_isLaunchAttack))]
        [SerializeField] private int _launchFrame = 0;
        [Tooltip("체크하면 Launch Frame 대신 첫 공격 판정 프레임에 뛰어오름 (누른 버튼마다 발생 프레임이 달라도 맞춰짐)")]
        [ShowIf(nameof(_isLaunchAttack))]
        [SerializeField] private bool _shouldLaunchAtStartup = false;
        [Tooltip("공중에 있는 동안 앞으로 가는 전체 거리 (음수면 뒤로)")]
        [ShowIf(nameof(_isLaunchAttack))]
        [SerializeField] private float _launchForwardDistance = 1.44f;
        [Tooltip("가장 높은 곳까지의 높이 (바닥 기준)")]
        [ShowIf(nameof(_isLaunchAttack))]
        [SerializeField] private float _launchHeight = 1.62f;
        [Tooltip("공중에 떠 있는 프레임 수. 뛰어오른 프레임이 공중 첫 프레임이고, 높이와 상관없이 항상 이 프레임 수만에 착지함.\n0이면 첫 공격 판정부터 공격이 끝날 때까지 (누른 버튼마다 프레임이 달라도 회수가 끝날 때 착지)")]
        [ShowIf(nameof(_isLaunchAttack))]
        [SerializeField] private int _launchAirFrames = 43;
        [Tooltip("체크하면 공중에 있는 동안은 프레임이 다 지나도 공격이 끝나지 않고, 착지할 때 끝남")]
        [ShowIf(nameof(_isLaunchAttack))]
        [SerializeField] private bool _holdUntilLanding = false;

        [Header("캔슬")]
        [Tooltip("이 공격이 상대에게 닿았을 때(히트 또는 가드) 캔슬해서 낼 수 있는 공격 (예: 약 -> 약, 2L, 236). 비워두면 캔슬되지 않음\n" +
                 "지속이 끝나고 회수 프레임에 들어갈 때 회수 대신 새 공격이 시작됨. 헛치면 캔슬되지 않음\n" +
                 "횟수 제한은 없고, 밀려난 상대에게 공격이 닿지 않으면 연타도 끊김")]
        [SerializeField] private AttackData[] _chainAttacks = new AttackData[0];

        [Header("착지 경직")]
        [Tooltip("공중에서 착지했을 때 움직일 수 없는 프레임 수. 0이면 경직 없음")]
        [SerializeField] private int _landingRecoveryFrames = 0;
        [Tooltip("착지 경직 동안 재생할 Animator 상태 이름. 비워두면 공격 애니메이션을 그대로 유지\n전이 만들기 메뉴(Tools > Animator)가 이 값으로 공격 상태 → 착지 상태 전이를 만듦")]
        [SerializeField] private string _landingAnimationState;

        [Header("애니메이션")]
        [Tooltip("공격 중 재생할 Animator 상태 이름. 공격 시작 때 같은 이름의 트리거를 Animator에 보냄\n전이 만들기 메뉴(Tools > Animator)가 이 이름으로 트리거와 전이를 만듦")]
        [SerializeField] private string _animationState;

        [Header("투사체")]
        [Tooltip("체크하면 공격 중에 투사체가 나감 (장풍 등). 체크하면 아래에 투사체 설정이 나옴")]
        [SerializeField] private bool _isProjectileAttack = false;
        [Tooltip("공격 중 생성할 투사체 프리팹")]
        [ShowIf(nameof(_isProjectileAttack))]
        [SerializeField] private Projectile _projectilePrefab;
        [Tooltip("투사체를 생성할 프레임 (공격 시작 프레임 = 1). 0이면 첫 공격 판정 프레임에 생성 (누른 버튼마다 발생 프레임이 달라도 맞춰짐)")]
        [ShowIf(nameof(_isProjectileAttack))]
        [SerializeField] private int _projectileSpawnFrame = 1;
        [Tooltip("투사체 생성 위치 (캐릭터 위치 기준, x는 바라보는 쪽이 +)")]
        [ShowIf(nameof(_isProjectileAttack))]
        [SerializeField] private Vector2 _projectileSpawnOffset = new Vector2(1f, 0.3f);
        [Tooltip("투사체의 데미지. 0이면 기술의 데미지 (버튼별 데미지가 있으면 그 값)")]
        [ShowIf(nameof(_isProjectileAttack))]
        [SerializeField] private int _projectileDamage = 0;
        [Tooltip("투사체의 속도: 1프레임에 날아가는 거리. 0이면 투사체 프리팹의 값 (버튼별 값이 있으면 그 값)")]
        [ShowIf(nameof(_isProjectileAttack))]
        [SerializeField] private float _projectileDistancePerFrame = 0f;
        [Tooltip("투사체의 개수. 다른 캐릭터의 투사체와 부딪히면 서로 1씩 줄고, 0이 되면 사라짐\n상대에게 맞거나 스테이지 끝 벽에 닿으면 개수와 상관없이 바로 사라짐")]
        [ShowIf(nameof(_isProjectileAttack))]
        [SerializeField] private int _projectileCount = 1;

        public int Damage => AD_damage;
        public Vector2 AttackBoxSize => AD_attackBoxSize;
        public Vector2 AttackBoxOffset => AD_attackBoxOffset;
        public bool ShouldHideTopHitbox => _shouldHideTopHitbox;
        public ExtraHitbox[] ExtraHitboxes => _extraHitboxes;
        public float LaunchForwardDistance => _launchForwardDistance;
        public float LaunchHeight => _launchHeight;
        /// <summary>이 공격이 상대에게 닿은 뒤 attack으로 바로 이어갈 수 있는지</summary>
        public bool CanChainTo(AttackData attack)
        {
            return _chainAttacks != null && Array.IndexOf(_chainAttacks, attack) >= 0;
        }

        public bool HoldUntilLanding => _holdUntilLanding;
        public int LandingRecoveryFrames => _landingRecoveryFrames;
        public string LandingAnimationState => _landingAnimationState;
        public string AnimationState => _animationState;
        // 투사체 공격이 아니면 null (프리팹을 넣어 두었어도 체크를 끄면 투사체가 나가지 않음)
        public Projectile ProjectilePrefab => _isProjectileAttack ? _projectilePrefab : null;
        public Vector2 ProjectileSpawnOffset => _projectileSpawnOffset;
        public int ProjectileCount => _projectileCount;
        public AttackHeight Height => _height;
        public TraitWindow[] TraitWindows => _traitWindows;
        public int HitAdvantage => _hitAdvantage;
        public float HitBackDistance => _hitBackDistance;
        public int HitBackFrames => _hitBackFrames;
        public HitMotion HitMotion => _hitMotion;
        public int BlockAdvantage => _blockAdvantage;
        public float GuardBackDistance => _guardBackDistance;
        public int GuardBackFrames => _guardBackFrames;

        /// <summary>이 버튼으로 나간 공격의 hitIndex번째 타(1타 = 0)에 맞은 상대의 피격 경직 프레임 수 (히트 시 유리 프레임 기준, 버튼별 값이 있으면 그 값)</summary>
        public int GetHitStunFrames(CharacterCommand.AttackButtonType button, int hitIndex = 0)
        {
            ButtonFrames buttonFrames = FindButtonFrames(button);
            int advantage = buttonFrames != null && buttonFrames.ShouldOverrideAdvantage ? buttonFrames.HitAdvantage : _hitAdvantage;
            return GetStunFrames(button, advantage, hitIndex);
        }

        /// <summary>이 버튼으로 나간 공격의 hitIndex번째 타(1타 = 0)를 가드한 상대의 가드 경직 프레임 수 (가드 시 유리 프레임 기준, 버튼별 값이 있으면 그 값)</summary>
        public int GetBlockStunFrames(CharacterCommand.AttackButtonType button, int hitIndex = 0)
        {
            ButtonFrames buttonFrames = FindButtonFrames(button);
            int advantage = buttonFrames != null && buttonFrames.ShouldOverrideAdvantage ? buttonFrames.BlockAdvantage : _blockAdvantage;
            return GetStunFrames(button, advantage, hitIndex);
        }

        // 그 타의 지속 첫 프레임(투사체 공격이면 투사체가 가장 빨리 맞힐 수 있는 프레임)에 맞았을 때 "남은 공격 프레임 + 유리 프레임"이 되는 경직 프레임 수
        // - 다단 공격은 어느 타에 맞아도 공격이 끝난 뒤 같은 유리 프레임이 되도록, 맞은 타 기준으로 남은 프레임을 셈
        // - 공격은 전체 프레임이 지난 다음 프레임에 끝나서(CharacterAttack) 그 프레임까지 움직일 수 없으므로 1프레임을 더함
        // - 착지 경직이 있는 공격(승룡권)은 위키 값이 착지 경직까지 포함한 값이므로 착지 경직도 더함
        // - 점프 공격은 언제 착지할지 정해져 있지 않음. 위키 값(4~9의 큰 쪽)은 착지 직전에 맞았을 때 기준이라
        //   남은 공격 프레임은 세지 않고 착지 경직 + 1 + 유리 프레임으로 정함 (점프 약 공격: 3 + 1 + 9 = 13, 위키의 피격 경직과 같음)
        // - 투사체는 나온 다음 프레임부터 날아가며 맞히므로, 바로 앞에서 맞으면 나온 프레임 + 1에 맞음 (위키 값은 바로 앞 기준)
        private int GetStunFrames(CharacterCommand.AttackButtonType button, int advantage, int hitIndex)
        {
            bool isJumpAttack = _holdUntilLanding && !_isLaunchAttack;
            if (isJumpAttack) return Mathf.Max(1, _landingRecoveryFrames + 1 + advantage);

            AttackFrames frames = GetFrames(button);
            hitIndex = Mathf.Clamp(hitIndex, 0, frames.AD_HitCount - 1);
            int hitFrame = ProjectilePrefab != null ? GetProjectileSpawnFrame(frames) + 1 : frames.GetFirstActiveFrame(hitIndex);
            return Mathf.Max(1, frames.TotalFrames - hitFrame + 1 + _landingRecoveryFrames + advantage);
        }

        /// <summary>이 버튼으로 나간 공격의 히트스톱 프레임 수 (버튼별 값이 0이면 기술의 값)</summary>
        public int GetHitStopFrames(CharacterCommand.AttackButtonType button)
        {
            ButtonFrames buttonFrames = FindButtonFrames(button);
            return buttonFrames != null && buttonFrames.AD_HitStopFrames > 0 ? buttonFrames.AD_HitStopFrames : _hitStopFrames;
        }

        /// <summary>이 버튼으로 나간 공격의 hitIndex번째 타(1타 = 0)가 맞았을 때(또는 가드됐을 때) 상대에게 줄 정보</summary>
        public HitInfo GetHitInfo(CharacterCommand.AttackButtonType button, int hitIndex = 0)
        {
            return new HitInfo(
                hitStunFrames: GetHitStunFrames(button, hitIndex), hitBackDistance: _hitBackDistance, hitBackFrames: _hitBackFrames,
                blockStunFrames: GetBlockStunFrames(button, hitIndex), guardBackDistance: _guardBackDistance, guardBackFrames: _guardBackFrames,
                height: _height, motion: _hitMotion, isThrow: HasTrait(AttackTrait.Throw),
                hitStopFrames: GetHitStopFrames(button),
                normalHitResult: _normalHitResult, counterHitResult: _counterHitResult, punishCounterResult: _punishCounterResult);
        }

        // 버튼별 프레임 목록에 없을 때 쓰는 기본 프레임
        public AttackFrames DefaultFrames => new AttackFrames(_frames);

        /// <summary>이 버튼으로 나갔을 때의 프레임 (버튼별 프레임 목록이 없거나 비어 있으면 기본 프레임)</summary>
        public AttackFrames GetFrames(CharacterCommand.AttackButtonType button)
        {
            ButtonFrames buttonFrames = FindButtonFrames(button);
            return buttonFrames != null && buttonFrames.HasFrames ? new AttackFrames(buttonFrames.Frames) : DefaultFrames;
        }

        /// <summary>이 버튼으로 나간 공격의 hitIndex번째 타(1타 = 0)의 데미지 (그 타의 공격 판정 칸에 정한 데미지 → 버튼별 데미지 → 기본 데미지 순서로, 0이면 다음 값)</summary>
        public int GetDamage(CharacterCommand.AttackButtonType button, int hitIndex = 0)
        {
            FrameSegment activeSegment = GetFrames(button).GetActiveSegment(hitIndex);
            if (activeSegment != null && activeSegment.Damage > 0) return activeSegment.Damage;

            ButtonFrames buttonFrames = FindButtonFrames(button);
            return buttonFrames != null && buttonFrames.Damage > 0 ? buttonFrames.Damage : AD_damage;
        }

        /// <summary>이 버튼으로 나갔을 때 투사체의 데미지 (투사체 데미지가 0이면 기술의 데미지)</summary>
        public int GetProjectileDamage(CharacterCommand.AttackButtonType button)
        {
            return _projectileDamage > 0 ? _projectileDamage : GetDamage(button);
        }

        /// <summary>이 버튼으로 나갔을 때의 공격 상자 크기 (버튼별 공격 상자가 없으면 기본 공격 상자)</summary>
        public Vector2 GetAttackBoxSize(CharacterCommand.AttackButtonType button)
        {
            ButtonFrames buttonFrames = FindButtonFrames(button);
            return buttonFrames != null && buttonFrames.HasAttackBox ? buttonFrames.AttackBoxSize : AD_attackBoxSize;
        }

        /// <summary>이 버튼으로 나갔을 때의 공격 상자 중심 위치 (버튼별 공격 상자가 없으면 기본 공격 상자)</summary>
        public Vector2 GetAttackBoxOffset(CharacterCommand.AttackButtonType button)
        {
            ButtonFrames buttonFrames = FindButtonFrames(button);
            return buttonFrames != null && buttonFrames.HasAttackBox ? buttonFrames.AttackBoxOffset : AD_attackBoxOffset;
        }

        /// <summary>이 버튼으로 나갔을 때 공격 시작부터 완전 무적인 프레임 수 (0이면 무적 없음)</summary>
        public int GetInvincibleFrames(CharacterCommand.AttackButtonType button)
        {
            ButtonFrames buttonFrames = FindButtonFrames(button);
            return buttonFrames != null ? buttonFrames.InvincibleFrames : 0;
        }

        /// <summary>
        /// 공격 시작부터 attackFrame번째 프레임의 공격 상자 (지속 프레임이 아니거나 공격 상자가 없으면 false)
        /// 그 타의 공격 판정 칸에 정한 공격 상자가 있으면 그것을, 지속 구간별 공격 상자가 있으면 그것을(그 타의 지속 기준), 없으면 버튼별 / 기본 공격 상자를 씀
        /// </summary>
        public bool TryGetAttackBox(CharacterCommand.AttackButtonType button, int attackFrame, AttackFrames frames, out Vector2 offset, out Vector2 size)
        {
            offset = Vector2.zero;
            size = Vector2.zero;
            int hitIndex = frames.FindHitIndex(attackFrame);
            if (hitIndex < 0) return false;

            FrameSegment activeSegment = frames.GetActiveSegment(hitIndex);
            if (activeSegment != null && activeSegment.HasAttackBox)
            {
                offset = activeSegment.AttackBoxOffset;
                size = activeSegment.AttackBoxSize;
                return true;
            }

            if (_attackBoxWindows != null && _attackBoxWindows.Length > 0)
            {
                int activeFrame = attackFrame - frames.GetFirstActiveFrame(hitIndex) + 1;
                foreach (var window in _attackBoxWindows)
                {
                    if (window == null || !window.IsActiveAt(activeFrame)) continue;
                    offset = window.Offset;
                    size = window.Size;
                    return size.x > 0f && size.y > 0f;
                }
                return false;
            }

            offset = GetAttackBoxOffset(button);
            size = GetAttackBoxSize(button);
            return size.x > 0f && size.y > 0f;
        }

        /// <summary>이 버튼으로 나갔을 때 투사체가 1프레임에 날아가는 거리 (버튼별 값 → 기술의 투사체 속도 순서로, 둘 다 0이면 0 = 투사체 프리팹의 값)</summary>
        /// <summary>
        /// 이번 공격 프레임(공격 시작 = 1)에 앞으로 나갈 거리. 발생 동안(첫 공격 판정 직전까지) 전체 전진 거리를 같은 거리로 나눠서 나감
        /// 버튼별 전진 거리가 있으면 그 값, 없으면 기술의 전진 거리
        /// </summary>
        public float GetForwardMoveDistancePerFrame(CharacterCommand.AttackButtonType button, AttackFrames frames, int attackFrame)
        {
            ButtonFrames buttonFrames = FindButtonFrames(button);
            float distance = buttonFrames != null && buttonFrames.AD_ForwardMoveDistance > 0f ? buttonFrames.AD_ForwardMoveDistance : _forwardMoveDistance;
            int moveFrames = frames.FirstActiveFrame - 1;
            if (distance <= 0f || moveFrames <= 0 || attackFrame < 1 || attackFrame > moveFrames) return 0f;
            return distance / moveFrames;
        }

        public float GetProjectileDistancePerFrame(CharacterCommand.AttackButtonType button)
        {
            ButtonFrames buttonFrames = FindButtonFrames(button);
            return buttonFrames != null && buttonFrames.ProjectileDistancePerFrame > 0f ? buttonFrames.ProjectileDistancePerFrame : _projectileDistancePerFrame;
        }

        /// <summary>공중에 떠 있는 프레임 수 (0으로 두었으면 이번 공격의 첫 공격 판정부터 공격이 끝날 때까지)</summary>
        public int GetLaunchAirFrames(AttackFrames frames)
        {
            return _launchAirFrames > 0 ? _launchAirFrames : frames.TotalFrames - frames.FirstActiveFrame + 1;
        }

        private ButtonFrames FindButtonFrames(CharacterCommand.AttackButtonType button)
        {
            if (_buttonFrames == null) return null;

            foreach (var buttonFrames in _buttonFrames)
            {
                if (buttonFrames != null && buttonFrames.Button == button) return buttonFrames;
            }
            return null;
        }

        /// <summary>뛰어오르는 프레임 (0이면 뛰어오르지 않음, 발생 프레임에 맞추게 했으면 이번 공격의 첫 공격 판정 프레임)</summary>
        public int GetLaunchFrame(AttackFrames frames)
        {
            if (!_isLaunchAttack) return 0;
            return _shouldLaunchAtStartup ? frames.FirstActiveFrame : _launchFrame;
        }

        /// <summary>투사체를 생성할 프레임 (0으로 두었으면 이번 공격의 첫 공격 판정 프레임)</summary>
        public int GetProjectileSpawnFrame(AttackFrames frames)
        {
            return _projectileSpawnFrame > 0 ? _projectileSpawnFrame : frames.FirstActiveFrame;
        }

        public bool HasTrait(AttackTrait trait)
        {
            foreach (var window in _traitWindows)
            {
                if (window.Trait == trait) return true;
            }
            return false;
        }

        private static void ValidateFrames(FrameSegment[] frames)
        {
            if (frames == null) return;
            foreach (var frameSegment in frames)
            {
                frameSegment?.Validate();
            }
        }

        private void OnValidate()
        {
            ValidateFrames(_frames);
            if (_projectileSpawnFrame < 0) _projectileSpawnFrame = 0;
            if (_projectileDamage < 0) _projectileDamage = 0;
            if (_projectileDistancePerFrame < 0f) _projectileDistancePerFrame = 0f;
            if (_projectileCount < 1) _projectileCount = 1;
            if (_forwardMoveDistance < 0f) _forwardMoveDistance = 0f;
            if (AD_damage < 0) AD_damage = 0;
            if (AD_attackBoxSize.x < 0f) AD_attackBoxSize.x = 0f;
            if (AD_attackBoxSize.y < 0f) AD_attackBoxSize.y = 0f;
            if (_hitBackDistance < 0f) _hitBackDistance = 0f;
            if (_hitBackFrames < 1) _hitBackFrames = 1;
            if (_guardBackDistance < 0f) _guardBackDistance = 0f;
            if (_guardBackFrames < 1) _guardBackFrames = 1;
            if (_hitStopFrames < 0) _hitStopFrames = 0;
            _normalHitResult?.Validate();
            _counterHitResult?.Validate();
            _punishCounterResult?.Validate();

            if (_extraHitboxes != null)
            {
                foreach (var extraHitbox in _extraHitboxes)
                {
                    extraHitbox?.Validate();
                }
            }

            if (_attackBoxWindows != null)
            {
                foreach (var attackBoxWindow in _attackBoxWindows)
                {
                    attackBoxWindow?.Validate();
                }
            }

            if (_launchFrame < 0) _launchFrame = 0;
            if (_launchHeight < 0f) _launchHeight = 0f;
            if (_launchAirFrames < 0) _launchAirFrames = 0;
            if (_landingRecoveryFrames < 0) _landingRecoveryFrames = 0;

            if (_buttonFrames != null)
            {
                foreach (var buttonFrames in _buttonFrames)
                {
                    buttonFrames?.Validate();
                }
            }

            if (_traitWindows == null) return;
            foreach (var window in _traitWindows)
            {
                window?.Validate();
            }
        }
    }
}
