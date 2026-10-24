using System;
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

    // 공격하는 동안 플레이어가 가지는 특성
    public enum AttackTrait
    {
        Throw,      // 잡기 판정
        Projectile, // 장풍 판정
        SuperArmor, // 슈퍼아머 (정해진 높이의 공격을 정해진 횟수만큼 버팀)
        Invincible  // 무적
    }

    // 공격 한 번의 프레임 (발생 / 지속 / 회수)
    public readonly struct AttackFrames
    {
        public int Startup { get; }
        public int Active { get; }
        public int Recovery { get; }

        public AttackFrames(int startup, int active, int recovery)
        {
            Startup = startup;
            Active = active;
            Recovery = recovery;
        }

        // 공격 판정이 나오는 첫 프레임과 마지막 프레임
        public int FirstActiveFrame => Startup;
        public int LastActiveFrame => Startup + Active - 1;

        // 공격 시작부터 회수가 끝날 때까지의 전체 프레임 수
        public int TotalFrames => LastActiveFrame + Recovery;
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

        // 누른 버튼마다 따로 정하는 프레임 (예: 236L, 236M, 236H)
        [Serializable]
        public class ButtonFrames
        {
            [Tooltip("이 버튼으로 기술이 나갔을 때 아래 프레임을 사용 (하나만 고름)")]
            [SerializeField] private CharacterCommand.AttackButtonType _button = CharacterCommand.AttackButtonType.L;

            [Tooltip("발생 프레임: 공격 판정이 처음 나오는 프레임")]
            [SerializeField] private int _startupFrames = 5;

            [Tooltip("지속 프레임: 공격 판정이 유지되는 프레임 수")]
            [SerializeField] private int _activeFrames = 3;

            [Tooltip("회수 프레임: 공격 판정이 끝난 뒤 다시 움직일 수 있을 때까지의 프레임 수")]
            [SerializeField] private int _recoveryFrames = 10;

            [Tooltip("이 버튼으로 나갔을 때의 데미지. 0이면 위의 기본 데미지 사용")]
            [SerializeField] private int AD_damage = 0;

            [Tooltip("이 버튼으로 나갔을 때 투사체가 1프레임에 날아가는 거리. 0이면 투사체 프리팹의 값 사용")]
            [SerializeField] private float _projectileDistancePerFrame = 0f;

            [Tooltip("이 버튼으로 나갔을 때의 공격 상자 크기. 0이면 위의 기본 공격 상자 사용")]
            [SerializeField] private Vector2 AD_attackBoxSize = Vector2.zero;

            [Tooltip("이 버튼으로 나갔을 때의 공격 상자 중심 위치 (크기가 0이 아닐 때만 사용)")]
            [SerializeField] private Vector2 AD_attackBoxOffset = Vector2.zero;

            public CharacterCommand.AttackButtonType Button => _button;
            public AttackFrames Frames => new AttackFrames(_startupFrames, _activeFrames, _recoveryFrames);
            public int Damage => AD_damage;
            public float ProjectileDistancePerFrame => _projectileDistancePerFrame;
            public bool HasAttackBox => AD_attackBoxSize.x > 0f && AD_attackBoxSize.y > 0f;
            public Vector2 AttackBoxSize => AD_attackBoxSize;
            public Vector2 AttackBoxOffset => AD_attackBoxOffset;

            public void Validate()
            {
                if (_startupFrames < 1) _startupFrames = 1;
                if (_activeFrames < 1) _activeFrames = 1;
                if (_recoveryFrames < 0) _recoveryFrames = 0;
                if (AD_damage < 0) AD_damage = 0;
                if (_projectileDistancePerFrame < 0f) _projectileDistancePerFrame = 0f;
                if (AD_attackBoxSize.x < 0f) AD_attackBoxSize.x = 0f;
                if (AD_attackBoxSize.y < 0f) AD_attackBoxSize.y = 0f;
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

        [Header("공격 중 피격 상자")]
        [Tooltip("체크하면 공격하는 동안 Top 피격 상자를 끔 (앉아서 쓰는 공격 등)")]
        [SerializeField] private bool _shouldHideTopHitbox = false;

        [Tooltip("공격하는 동안 더 생기는 피격 상자 (뻗은 팔, 다리 등). 정한 프레임 구간에만 켜짐")]
        [SerializeField] private ExtraHitbox[] _extraHitboxes = new ExtraHitbox[0];

        [Header("프레임")]
        [Tooltip("발생 프레임: 공격 판정이 처음 나오는 프레임.\n예) 5면 1~4프레임은 준비, 5프레임부터 공격 판정")]
        [SerializeField] private int _startupFrames = 5;

        [Tooltip("지속 프레임: 공격 판정이 유지되는 프레임 수")]
        [SerializeField] private int _activeFrames = 3;

        [Tooltip("회수 프레임: 공격 판정이 끝난 뒤 다시 움직일 수 있을 때까지의 프레임 수")]
        [SerializeField] private int _recoveryFrames = 10;

        [Tooltip("누른 버튼마다 프레임을 다르게 할 때 사용 (예: 236을 L / M / H / EX로 썼을 때).\n목록에 없는 버튼으로 나가면 위의 발생 / 지속 / 회수 프레임을 사용")]
        [SerializeField] private ButtonFrames[] _buttonFrames = new ButtonFrames[0];

        [Header("프레임 구간별 특성")]
        [Tooltip("예) 슈퍼아머 / 시작 1 / 끝 8 / 상단 / 2회 → 1~8프레임 동안 상단 공격을 2번까지 버팀")]
        [SerializeField] private TraitWindow[] _traitWindows = new TraitWindow[0];

        [Header("공중으로 뛰어오르기")]
        [Tooltip("이 프레임에 앞으로 뛰어오름 (공격 시작 프레임 = 1). 0이면 뛰어오르지 않음")]
        [SerializeField] private int _launchFrame = 0;
        [Tooltip("체크하면 Launch Frame 대신 발생 프레임에 뛰어오름 (누른 버튼마다 발생 프레임이 달라도 맞춰짐)")]
        [SerializeField] private bool _shouldLaunchAtStartup = false;
        [Tooltip("공중에 있는 동안 앞으로 가는 전체 거리 (음수면 뒤로)")]
        [SerializeField] private float _launchForwardDistance = 1.44f;
        [Tooltip("가장 높은 곳까지의 높이 (바닥 기준)")]
        [SerializeField] private float _launchHeight = 1.62f;
        [Tooltip("공중에 떠 있는 프레임 수. 뛰어오른 프레임이 공중 첫 프레임이고, 높이와 상관없이 항상 이 프레임 수만에 착지함.\n0이면 지속 + 회수 프레임 (누른 버튼마다 프레임이 달라도 회수가 끝날 때 착지)")]
        [SerializeField] private int _launchAirFrames = 43;
        [Tooltip("체크하면 공중에 있는 동안은 프레임이 다 지나도 공격이 끝나지 않고, 착지할 때 끝남")]
        [SerializeField] private bool _holdUntilLanding = false;

        [Header("착지 경직")]
        [Tooltip("공중에서 착지했을 때 움직일 수 없는 프레임 수. 0이면 경직 없음")]
        [SerializeField] private int _landingRecoveryFrames = 0;
        [Tooltip("착지 경직 동안 재생할 Animator 상태 이름. 비워두면 공격 애니메이션을 그대로 유지")]
        [SerializeField] private string _landingAnimationState;

        [Header("애니메이션")]
        [Tooltip("공격 중 재생할 Animator 상태 이름")]
        [SerializeField] private string _animationState;

        [Header("투사체")]
        [Tooltip("공격 중 생성할 투사체 프리팹. 없으면 비워둠")]
        [SerializeField] private Projectile _projectilePrefab;
        [Tooltip("투사체를 생성할 프레임 (공격 시작 프레임 = 1). 0이면 발생 프레임에 생성 (누른 버튼마다 발생 프레임이 달라도 맞춰짐)")]
        [SerializeField] private int _projectileSpawnFrame = 1;
        [Tooltip("투사체 생성 위치 (캐릭터 위치 기준, x는 바라보는 쪽이 +)")]
        [SerializeField] private Vector2 _projectileSpawnOffset = new Vector2(1f, 0.3f);

        public string AttackName => _attackName;
        public int Damage => AD_damage;
        public Vector2 AttackBoxSize => AD_attackBoxSize;
        public Vector2 AttackBoxOffset => AD_attackBoxOffset;
        public bool HasAttackBox => AD_attackBoxSize.x > 0f && AD_attackBoxSize.y > 0f;
        public bool ShouldHideTopHitbox => _shouldHideTopHitbox;
        public ExtraHitbox[] ExtraHitboxes => _extraHitboxes;
        public float LaunchForwardDistance => _launchForwardDistance;
        public float LaunchHeight => _launchHeight;
        public bool HoldUntilLanding => _holdUntilLanding;
        public int LandingRecoveryFrames => _landingRecoveryFrames;
        public string LandingAnimationState => _landingAnimationState;
        public string AnimationState => _animationState;
        public Projectile ProjectilePrefab => _projectilePrefab;
        public int ProjectileSpawnFrame => _projectileSpawnFrame;
        public Vector2 ProjectileSpawnOffset => _projectileSpawnOffset;
        public AttackHeight Height => _height;
        public TraitWindow[] TraitWindows => _traitWindows;

        // 버튼별 프레임 목록에 없을 때 쓰는 기본 프레임
        public AttackFrames DefaultFrames => new AttackFrames(_startupFrames, _activeFrames, _recoveryFrames);

        /// <summary>이 버튼으로 나갔을 때의 프레임 (버튼별 프레임 목록에 없으면 기본 프레임)</summary>
        public AttackFrames GetFrames(CharacterCommand.AttackButtonType button)
        {
            ButtonFrames buttonFrames = FindButtonFrames(button);
            return buttonFrames != null ? buttonFrames.Frames : DefaultFrames;
        }

        /// <summary>이 버튼으로 나갔을 때의 데미지 (버튼별 데미지가 0이거나 없으면 기본 데미지)</summary>
        public int GetDamage(CharacterCommand.AttackButtonType button)
        {
            ButtonFrames buttonFrames = FindButtonFrames(button);
            return buttonFrames != null && buttonFrames.Damage > 0 ? buttonFrames.Damage : AD_damage;
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

        /// <summary>이 버튼으로 나갔을 때 공격 상자가 있는지 (장풍만 쓰는 기술 등은 없음)</summary>
        public bool HasAttackBoxFor(CharacterCommand.AttackButtonType button)
        {
            Vector2 size = GetAttackBoxSize(button);
            return size.x > 0f && size.y > 0f;
        }

        /// <summary>이 버튼으로 나갔을 때 투사체가 1프레임에 날아가는 거리 (0이면 투사체 프리팹의 값)</summary>
        public float GetProjectileDistancePerFrame(CharacterCommand.AttackButtonType button)
        {
            ButtonFrames buttonFrames = FindButtonFrames(button);
            return buttonFrames != null ? buttonFrames.ProjectileDistancePerFrame : 0f;
        }

        /// <summary>공중에 떠 있는 프레임 수 (0으로 두었으면 이번 공격의 지속 + 회수 프레임)</summary>
        public int GetLaunchAirFrames(AttackFrames frames)
        {
            return _launchAirFrames > 0 ? _launchAirFrames : frames.Active + frames.Recovery;
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

        /// <summary>뛰어오르는 프레임 (0이면 뛰어오르지 않음, 발생 프레임에 맞추게 했으면 이번 공격의 발생 프레임)</summary>
        public int GetLaunchFrame(AttackFrames frames)
        {
            return _shouldLaunchAtStartup ? frames.FirstActiveFrame : _launchFrame;
        }

        /// <summary>투사체를 생성할 프레임 (0으로 두었으면 이번 공격의 발생 프레임)</summary>
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

        private void OnValidate()
        {
            if (_startupFrames < 1) _startupFrames = 1;
            if (_activeFrames < 1) _activeFrames = 1;
            if (_recoveryFrames < 0) _recoveryFrames = 0;
            if (_projectileSpawnFrame < 0) _projectileSpawnFrame = 0;
            if (AD_damage < 0) AD_damage = 0;
            if (AD_attackBoxSize.x < 0f) AD_attackBoxSize.x = 0f;
            if (AD_attackBoxSize.y < 0f) AD_attackBoxSize.y = 0f;

            if (_extraHitboxes != null)
            {
                foreach (var extraHitbox in _extraHitboxes)
                {
                    extraHitbox?.Validate();
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
