using System;
using UnityEngine;

namespace Game.Character
{
    // 커맨드가 성공하면 그 커맨드에 연결된 AttackData로 공격을 진행함
    // - 프레임을 세면서 준비(발생 전) → 공격 판정(지속) → 회수 순서로 진행
    // - 프레임 구간별 특성(잡기, 장풍, 슈퍼아머, 무적)을 현재 프레임에 맞춰 알려줌
    // - 공격 중에는 CharacterMove가 땅에서 움직이지 않음
    // CharacterCommand가 커맨드를 판정한 뒤에 프레임을 세도록 실행 순서를 뒤로 미룸
    [DefaultExecutionOrder(20)]
    public class CharacterAttack : MonoBehaviour
    {
        public enum AttackPhase
        {
            None,     // 공격 안 함
            Startup,  // 준비 (발생 전)
            Active,   // 공격 판정 중 (지속)
            Recovery, // 회수
            Landing   // 착지 경직
        }

        [Tooltip("커맨드 성공을 받아올 CharacterCommand. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private CharacterCommand _characterCommand;

        [Tooltip("공격 중 상단/중단/하단/잡기 값을 넣을 CharactorState. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private CharactorState CA_characterState;

        [Tooltip("바라보는 방향, 땅/공중 상태를 알려줄 CharacterMove. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private CharacterMove CA_characterMove;

        /// <summary>지금 진행 중인 공격 (공격 중이 아니면 null)</summary>
        public AttackData CurrentAttack { get; private set; }

        public AttackPhase CurrentPhase { get; private set; } = AttackPhase.None;

        /// <summary>공격 시작부터 지난 프레임 (공격 시작 프레임 = 1)</summary>
        public int AttackFrame { get; private set; }

        public bool IsAttacking => CurrentAttack != null;

        /// <summary>착지 경직 중인지 (이 동안에도 IsAttacking은 true라서 움직일 수 없음)</summary>
        public bool IsLanding => CurrentPhase == AttackPhase.Landing;

        public event Action<AttackData> OnAttackStarted;
        public event Action<AttackPhase> OnPhaseChanged;
        public event Action<AttackData> OnAttackEnded;

        // 슈퍼아머 구간마다 남은 횟수 (CurrentAttack.TraitWindows와 같은 순서)
        private int[] _remainingArmorHits = new int[0];

        private bool _isAirAttack; // 점프 중에 시작한 공격인지 (착지하면 끝남)
        private int _landingFrame;  // 착지 경직이 시작된 뒤 지난 프레임 (착지한 프레임 = 1)

        private void Awake()
        {
            if (_characterCommand == null) _characterCommand = GetComponent<CharacterCommand>();
            if (CA_characterState == null) CA_characterState = GetComponent<CharactorState>();
            if (CA_characterMove == null) CA_characterMove = GetComponent<CharacterMove>();

            if (_characterCommand == null)
                Debug.LogError("[CharacterAttack] CharacterCommand를 찾을 수 없습니다. Character Command 칸에 지정해 주세요.", this);
        }

        private void OnEnable()
        {
            if (_characterCommand != null) _characterCommand.OnCommandTriggered += HandleCommandTriggered;
        }

        private void OnDisable()
        {
            if (_characterCommand != null) _characterCommand.OnCommandTriggered -= HandleCommandTriggered;
        }

        private void Update()
        {
            if (!IsAttacking) return;

            if (IsLanding)
            {
                _landingFrame++;
                if (_landingFrame > CurrentAttack.LandingRecoveryFrames) EndAttack();
                return;
            }

            AttackFrame++;

            // 공중 공격이 착지하면, 착지 경직이 있으면 경직으로 넘어가고 없으면 바로 끝남
            if (_isAirAttack && IsGrounded())
            {
                if (CurrentAttack.LandingRecoveryFrames > 0)
                {
                    _landingFrame = 1;
                    SetPhase(AttackPhase.Landing);
                }
                else
                {
                    EndAttack();
                }
                return;
            }

            // HoldUntilLanding이면 공중에 있는 동안은 프레임이 다 지나도 끝내지 않음
            bool isFramesOver = AttackFrame > CurrentAttack.TotalFrames &&
                                !(CurrentAttack.HoldUntilLanding && !IsGrounded());
            if (isFramesOver)
            {
                EndAttack();
                return;
            }

            SetPhase(GetPhaseAt(AttackFrame));

            if (CurrentAttack.ProjectilePrefab != null && AttackFrame == CurrentAttack.ProjectileSpawnFrame)
                SpawnProjectile(CurrentAttack);

            if (AttackFrame == CurrentAttack.LaunchFrame && CA_characterMove != null)
            {
                CA_characterMove.LaunchIntoAir(CurrentAttack.LaunchForwardSpeed, CurrentAttack.LaunchUpSpeed);
                _isAirAttack = true; // 뛰어오른 뒤 착지하면 공격이 끝남
            }
        }

        private bool IsGrounded()
        {
            return CA_characterMove == null || CA_characterMove.IsGrounded;
        }

        private bool IsFacingRight()
        {
            return CA_characterMove == null || CA_characterMove.IsFacingRight;
        }

        private void SpawnProjectile(AttackData attack)
        {
            bool isFacingRight = IsFacingRight();
            Vector2 offset = attack.ProjectileSpawnOffset;
            Vector3 position = transform.position + new Vector3(isFacingRight ? offset.x : -offset.x, offset.y, 0f);

            Projectile projectile = Instantiate(attack.ProjectilePrefab, position, Quaternion.identity);
            projectile.Launch(isFacingRight);
        }

        // 공격 중에 들어온 커맨드는 무시함 (캔슬은 아직 없음)
        private void HandleCommandTriggered(CharacterCommand.CommandData command)
        {
            if (command.attack == null || IsAttacking) return;
            StartAttack(command.attack);
        }

        public void StartAttack(AttackData attack)
        {
            CurrentAttack = attack;
            AttackFrame = 0; // 이번 프레임 Update에서 1이 됨
            _isAirAttack = !IsGrounded();

            var windows = attack.TraitWindows;
            _remainingArmorHits = new int[windows.Length];
            for (int i = 0; i < windows.Length; i++)
            {
                _remainingArmorHits[i] = windows[i].ArmorHitCount;
            }

            ApplyCharacterState(attack);
            OnAttackStarted?.Invoke(attack);
        }

        private void EndAttack()
        {
            AttackData endedAttack = CurrentAttack;
            CurrentAttack = null;
            AttackFrame = 0;
            _landingFrame = 0;
            SetPhase(AttackPhase.None);
            OnAttackEnded?.Invoke(endedAttack);
        }

        private AttackPhase GetPhaseAt(int frame)
        {
            if (frame < CurrentAttack.FirstActiveFrame) return AttackPhase.Startup;
            if (frame <= CurrentAttack.LastActiveFrame) return AttackPhase.Active;
            return AttackPhase.Recovery;
        }

        private void SetPhase(AttackPhase phase)
        {
            if (phase == CurrentPhase) return;
            CurrentPhase = phase;
            OnPhaseChanged?.Invoke(phase);
        }

        // 잡기 특성이 있으면 잡기, 없으면 판정 높이를 CharactorState에 넣음
        private void ApplyCharacterState(AttackData attack)
        {
            if (CA_characterState == null) return;

            if (attack.HasTrait(AttackTrait.Throw))
            {
                CA_characterState.Grap = true;
                return;
            }

            switch (attack.Height)
            {
                case AttackHeight.High: CA_characterState.High = true; break;
                case AttackHeight.Middle: CA_characterState.Middle = true; break;
                case AttackHeight.Low: CA_characterState.Low = true; break;
            }
        }

        // =====================================================================
        // 다른 스크립트(피격 판정 등)에서 쓰는 기능
        // =====================================================================

        /// <summary>지금 프레임에 이 특성이 켜져 있는지 (예: HasTrait(AttackTrait.Invincible))</summary>
        public bool HasTrait(AttackTrait trait)
        {
            if (!IsAttacking || IsLanding) return false; // 착지 경직 중에는 특성이 없음

            var windows = CurrentAttack.TraitWindows;
            for (int i = 0; i < windows.Length; i++)
            {
                if (windows[i].Trait == trait && windows[i].IsActiveAt(AttackFrame)) return true;
            }
            return false;
        }

        /// <summary>
        /// 공격을 맞았을 때 피격 판정 스크립트에서 호출.
        /// 지금 프레임의 슈퍼아머가 이 높이의 공격을 버틸 수 있으면 횟수를 1 줄이고 true를 반환
        /// </summary>
        public bool TryAbsorbHit(AttackHeight incomingHeight)
        {
            if (!IsAttacking || IsLanding) return false;

            AttackHeightMask incomingMask = (AttackHeightMask)(1 << (int)incomingHeight);
            var windows = CurrentAttack.TraitWindows;
            for (int i = 0; i < windows.Length; i++)
            {
                var window = windows[i];
                if (window.Trait != AttackTrait.SuperArmor || !window.IsActiveAt(AttackFrame)) continue;
                if ((window.ArmorHeights & incomingMask) == 0) continue;

                // ArmorHitCount가 0이면 횟수 제한 없음
                if (window.ArmorHitCount == 0) return true;
                if (_remainingArmorHits[i] <= 0) continue;

                _remainingArmorHits[i]--;
                return true;
            }
            return false;
        }
    }
}
