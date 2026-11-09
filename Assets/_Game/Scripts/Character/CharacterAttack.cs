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

        [Tooltip("공격 중 상단/중단/하단, 장풍/잡기/슈퍼아머 값과 준 데미지를 넣을 CharactorState. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private CharactorState CA_characterState;

        [Tooltip("바라보는 방향, 땅/공중 상태를 알려줄 CharacterMove. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private CharacterMove CA_characterMove;

        [Tooltip("피격 / 가드 경직 중에는 공격하지 않도록 확인할 CharacterHitReaction. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private CharacterHitReaction CA_hitReaction;

        [Tooltip("투사체가 맞힐 상대의 태그 (Player에는 Enemy)")]
        [SerializeField] private string CA_opponentTag = "Enemy";

        /// <summary>지금 진행 중인 공격 (공격 중이 아니면 null)</summary>
        public AttackData CurrentAttack { get; private set; }

        /// <summary>지금 진행 중인 공격의 발생 / 지속 / 회수 프레임 (누른 버튼에 맞춘 값)</summary>
        public AttackFrames CurrentFrames { get; private set; }

        public AttackPhase CurrentPhase { get; private set; } = AttackPhase.None;

        /// <summary>공격 시작부터 지난 프레임 (공격 시작 프레임 = 1)</summary>
        public int AttackFrame { get; private set; }

        public bool IsAttacking => CurrentAttack != null;

        /// <summary>착지 경직 중인지 (이 동안에도 IsAttacking은 true라서 움직일 수 없음)</summary>
        public bool IsLanding => CurrentPhase == AttackPhase.Landing;

        /// <summary>
        /// 지금 프레임에 완전 무적인지 (피격 상자를 모두 끔)
        /// 버튼별 무적 프레임(예: EX 승룡권 1~8프레임) 또는 프레임 구간별 특성의 무적
        /// </summary>
        public bool IsInvincible =>
            IsAttacking && !IsLanding &&
            ((AttackFrame >= 1 && AttackFrame <= CurrentAttack.GetInvincibleFrames(_currentButton)) || HasTrait(AttackTrait.Invincible));

        // 슈퍼아머 구간마다 남은 횟수 (CurrentAttack.TraitWindows와 같은 순서)
        private int[] _remainingArmorHits = new int[0];

        private bool _isAirAttack; // 점프 중에 시작한 공격인지 (착지하면 끝남)
        private int _lastHitIndex; // 이번 공격에서 마지막으로 상대를 맞힌 타 (1타 = 0, 아직 안 맞혔으면 -1). 타마다 한 번만 맞힘
        private CharacterCommand.AttackButtonType _currentButton; // 이번 공격을 낸 버튼 (버튼별 데미지, 투사체 속도에 사용)
        private int _landingFrame;  // 착지 경직이 시작된 뒤 지난 프레임 (착지한 프레임 = 1)

        // 공중에서 프레임이 다 끝난 공격. 착지하면 이 공격의 착지 경직을 시작함
        private AttackData _pendingLandingAttack;

        [Tooltip("지금 낼 수 없는 입력을 기억해 두는 프레임 수 (입력 버퍼). 히트스톱 중에는 줄지 않음")]
        [SerializeField] private int _inputBufferFrames = 6;

        // 기억해 둔 입력 (없으면 null)
        private CharacterCommand.CommandData _bufferedCommand;
        private CharacterCommand.AttackButtonType _bufferedButton;
        private int _bufferedFramesLeft;

        private bool _isSpecialAttack;  // 지금 공격이 필살기(레버를 두 방향 이상 순서대로 넣는 커맨드, 예: 236, 623)인지
        private bool _isBufferLocked;   // 필살기가 히트해서 이 공격이 끝날 때까지 선입력을 받지 않는 중인지

        private int _attackStartedFrame = -1; // 마지막으로 공격을 시작한 Time.frameCount
        private CharacterHitReaction _lastHitReaction; // 이번 공격이 닿은 상대 (캔슬할 때 경직을 늘리는 데 사용)
        private CharacterHitReaction _chainTarget; // 이번 공격이 캔슬로 나왔을 때 앞 공격이 닿은 상대 (캔슬로 나온 공격이 아니면 null)

        private void Awake()
        {
            if (_characterCommand == null) _characterCommand = GetComponent<CharacterCommand>();
            if (CA_characterState == null) CA_characterState = GetComponent<CharactorState>();
            if (CA_characterMove == null) CA_characterMove = GetComponent<CharacterMove>();
            if (CA_hitReaction == null) CA_hitReaction = GetComponent<CharacterHitReaction>();

            if (_characterCommand == null)
                Debug.LogError("[CharacterAttack] CharacterCommand를 찾을 수 없습니다. Character Command 칸에 지정해 주세요.", this);
        }

        private void OnEnable()
        {
            if (_characterCommand == null) return;
            _characterCommand.OnCommandTriggered += HandleCommandTriggered;
            _characterCommand.CanUseCommand = CanUseCommand;
        }

        private void OnDisable()
        {
            _bufferedCommand = null;
            if (_characterCommand == null) return;
            _characterCommand.OnCommandTriggered -= HandleCommandTriggered;
            if (_characterCommand.CanUseCommand == CanUseCommand) _characterCommand.CanUseCommand = null;
        }

        // 커맨드 판정에서 쓸 수 있는 커맨드인지 알려줌
        // 내가 쏜 투사체가 남아 있으면 투사체 공격(장풍) 커맨드는 쓸 수 없음 → 같은 버튼의 다른 공격이 나감
        private bool CanUseCommand(CharacterCommand.CommandData command)
        {
            if (command.attack == null || command.attack.ProjectilePrefab == null) return true;
            return !Projectile.HasActiveProjectile(gameObject);
        }

        private void Update()
        {
            // 슬로우 모션 중에는 건너뛰는 프레임이 있음 (GameSpeed)
            if (!Game.Field.GameSpeed.IsTickFrame) return;

            // 히트스톱 중에는 공격 프레임이 진행되지 않음
            if (HitStop.IsStoppedNow(this)) return;

            TryStartBufferedCommand();

            if (!IsAttacking)
            {
                if (_pendingLandingAttack != null && IsGrounded())
                    StartLanding(_pendingLandingAttack);
                return;
            }

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
                    StartLanding(CurrentAttack);
                else
                    EndAttack();
                return;
            }

            // HoldUntilLanding이면 공중에 있는 동안은 프레임이 다 지나도 끝내지 않음
            bool isFramesOver = AttackFrame > CurrentFrames.TotalFrames &&
                                !(CurrentAttack.HoldUntilLanding && !IsGrounded());
            if (isFramesOver)
            {
                // 공중에서 끝난 공격은 점프 모습으로 돌아가고, 착지할 때 착지 경직을 받음
                if (_isAirAttack && !IsGrounded() && CurrentAttack.LandingRecoveryFrames > 0)
                    _pendingLandingAttack = CurrentAttack;
                EndAttack();
                return;
            }

            SetPhase(GetPhaseAt(AttackFrame));

            if (CurrentAttack.ProjectilePrefab != null && AttackFrame == CurrentAttack.GetProjectileSpawnFrame(CurrentFrames))
                SpawnProjectile(CurrentAttack);

            if (AttackFrame == CurrentAttack.GetLaunchFrame(CurrentFrames) && CA_characterMove != null)
            {
                CA_characterMove.LaunchIntoAir(forwardDistance: CurrentAttack.LaunchForwardDistance, height: CurrentAttack.LaunchHeight, airFrames: CurrentAttack.GetLaunchAirFrames(CurrentFrames));
                _isAirAttack = true; // 뛰어오른 뒤 착지하면 공격이 끝남
            }

            // 지속 프레임 동안 공격 상자가 상대의 피격 상자에 닿으면 데미지 (다단 공격은 타마다 한 번씩)
            if (CurrentPhase == AttackPhase.Active)
            {
                int hitIndex = CurrentFrames.FindHitIndex(AttackFrame);
                if (hitIndex > _lastHitIndex)
                    TryHitOpponent(CurrentAttack, hitIndex);
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
            projectile.Launch(isFacingRight, owner: gameObject, targetTag: CA_opponentTag, damage: attack.GetProjectileDamage(_currentButton), hitInfo: attack.GetHitInfo(_currentButton), distancePerFrame: attack.GetProjectileDistancePerFrame(_currentButton), count: attack.ProjectileCount);
        }

        // 이번 프레임의 공격 상자와 겹치는 상대(피격 상자)가 있으면 hitIndex번째 타의 데미지를 줌 (공격 상자가 없는 프레임이면 아무것도 안 함)
        private void TryHitOpponent(AttackData attack, int hitIndex)
        {
            if (!attack.TryGetAttackBox(_currentButton, AttackFrame, CurrentFrames, out Vector2 offset, out Vector2 size)) return;

            float facingSign = IsFacingRight() ? 1f : -1f;
            Vector2 attackBoxCenter = (Vector2)transform.position + new Vector2(offset.x * facingSign, offset.y);

            foreach (GameObject opponent in GameObject.FindGameObjectsWithTag(CA_opponentTag))
            {
                if (opponent == gameObject) continue;
                Hitbox hitbox = Hitbox.FindHitHitbox(opponent, attackBoxCenter, size);
                if (hitbox == null) continue;

                _lastHitIndex = hitIndex;
                _lastHitReaction = opponent.GetComponent<CharacterHitReaction>();
                CharacterHitReaction.HitTarget(opponent, attack.GetDamage(_currentButton, hitIndex), attack.GetHitInfo(_currentButton, hitIndex), hitbox, direction: facingSign, attacker: this, shouldPushAttacker: true);
                LockBufferIfSpecialHit(_lastHitReaction);
                return;
            }
        }

        // 씬 뷰에서 이번 프레임의 공격 상자를 빨간 사각형으로 보여줌 (플레이 중 지속 프레임, 이 오브젝트를 선택했을 때)
        private void OnDrawGizmosSelected()
        {
            if (!IsAttacking || !CurrentAttack.TryGetAttackBox(_currentButton, AttackFrame, CurrentFrames, out Vector2 offset, out Vector2 size)) return;

            float facingSign = IsFacingRight() ? 1f : -1f;
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position + new Vector3(offset.x * facingSign, offset.y, 0f), new Vector3(size.x, size.y, 0.1f));
        }

        // 지금 낼 수 있으면 바로 내고, 아니면 잠시 기억해 뒀다가 낼 수 있게 되는 순간 냄 (입력 버퍼)
        // - 같은 프레임에 눌린 버튼으로 이미 공격이 시작됐으면 두 번째 입력은 버림
        // - 동시입력 커맨드(잡기 등)는 기억하지 않음 (먼저 눌린 버튼의 공격 뒤에 뒤늦게 나가지 않도록)
        private void HandleCommandTriggered(CharacterCommand.CommandData command, CharacterCommand.AttackButtonType button)
        {
            if (command.attack == null || _attackStartedFrame == Time.frameCount) return;

            if (!HitStop.IsStoppedNow(this) && CanStartCommand(command))
            {
                StartAttack(command.attack, button, isSpecial: IsSpecialCommand(command));
                return;
            }

            // 필살기가 히트한 뒤에는 그 기술이 끝날 때까지 지금 낼 수 없는 입력을 기억하지 않음 (선입력 불가)
            if (command.requireAllButtons || _isBufferLocked) return;

            // 버튼을 먼저 누른 커맨드는 캔슬로 이어지는 중일 때만 기억함 (평소에는 먼저 나간 기본기 뒤에 뒤늦게 나가지 않도록)
            bool isConnected = IsAttacking && !IsLanding && (_lastHitIndex >= 0 || _chainTarget != null);
            if (_characterCommand.IsLateMotionCheck && !isConnected) return;

            // 기억 중인 방향 커맨드는 연타한 기본기로 덮어쓰지 않음
            if (_bufferedCommand != null && HasMotion(_bufferedCommand) && !HasMotion(command)) return;

            _bufferedCommand = command;
            _bufferedButton = button;
            _bufferedFramesLeft = _inputBufferFrames;
        }

        // 지금 이 커맨드의 공격을 시작할 수 있는지
        // 공격 중이면 캔슬할 수 있을 때만 가능: 캔슬 목록에 있는 공격이고, 다음 프레임이 회수 프레임일 때 (지속이 끝나고 회수 대신 새 공격이 시작됨)
        private bool CanStartCommand(CharacterCommand.CommandData command)
        {
            if (!CanUseCommand(command)) return false;
            if (CA_hitReaction != null && CA_hitReaction.IsInStun) return false;
            if (!IsAttacking) return true;
            if (CanCancelInto(command.attack)) return CurrentFrames.GetFrameType(AttackFrame + 1) == FrameType.Recovery;
            return HasMotion(command) && CanReplaceChainedStartup(command.attack);
        }

        private static bool HasMotion(CharacterCommand.CommandData command)
        {
            return command.directionSequence != null && command.directionSequence.Length > 0;
        }

        // 필살기 커맨드인지 (레버를 두 방향 이상 순서대로 넣는 커맨드. 6M처럼 한 방향만 누르는 특수기는 아님)
        private static bool IsSpecialCommand(CharacterCommand.CommandData command)
        {
            return command.directionSequence != null && command.directionSequence.Length >= 2;
        }

        // 필살기가 상대에게 히트하면(가드, 슈퍼아머로 버틴 것은 아님) 이 공격이 끝날 때까지 선입력을 받지 않고, 기억해 둔 입력도 버림
        private void LockBufferIfSpecialHit(CharacterHitReaction target)
        {
            if (!_isSpecialAttack || target == null || !target.IsInHitStun) return;
            _isBufferLocked = true;
            _bufferedCommand = null;
        }

        /// <summary>내가 쏜 투사체가 상대에게 닿았을 때 호출 (Projectile). 투사체를 쏜 필살기가 아직 진행 중이면 히트 시 선입력을 막음</summary>
        public void NotifyProjectileHit(GameObject target)
        {
            if (!IsAttacking || CurrentAttack.ProjectilePrefab == null || target == null) return;
            LockBufferIfSpecialHit(target.GetComponent<CharacterHitReaction>());
        }

        // 캔슬로 나간 공격의 발생 중에는 다른 공격으로 바꿀 수 있음
        // (연타로 먼저 나간 약 때문에 커맨드가 빠지지 않도록. 약이 닿기 전이라 회수를 기다릴 수 없음)
        private bool CanReplaceChainedStartup(AttackData attack)
        {
            return IsAttacking && !IsLanding && _chainTarget != null &&
                   AttackFrame + 1 < CurrentFrames.FirstActiveFrame && CurrentAttack.CanChainTo(attack);
        }

        // 이번 공격이 상대에게 닿았고(히트 또는 가드) 캔슬 목록에 attack이 있는지
        private bool CanCancelInto(AttackData attack)
        {
            return IsAttacking && !IsLanding && _lastHitIndex >= 0 && CurrentAttack.CanChainTo(attack);
        }

        // 이번 프레임에 시작한 attack이 target에게 닿기까지 target의 경직이 남아 있어야 하는 프레임 수
        // 맞으면 새 기술의 경직으로 바뀌므로, 투사체는 판정 크기를 빼지 않고 넉넉하게 계산함 (어차피 그 전에 맞으면 덮어씀)
        private int GetFramesUntilHit(AttackData attack, CharacterCommand.AttackButtonType button, Transform target)
        {
            AttackFrames frames = attack.GetFrames(button);
            if (attack.ProjectilePrefab == null) return frames.FirstActiveFrame;

            float speed = attack.GetProjectileDistancePerFrame(button);
            float distance = Mathf.Abs(target.position.x - transform.position.x) - attack.ProjectileSpawnOffset.x;
            int travelFrames = speed > 0f ? Mathf.CeilToInt(Mathf.Max(0f, distance) / speed) : 0;
            return attack.GetProjectileSpawnFrame(frames) + travelFrames + 1; // 투사체는 나온 다음 프레임부터 움직이며 맞힘
        }

        // 기억해 둔 입력을 낼 수 있으면 냄. 히트스톱 중에는 불리지 않으므로 멈춘 동안에는 기억하는 시간이 줄지 않음
        private void TryStartBufferedCommand()
        {
            if (_bufferedCommand == null) return;

            CharacterCommand.CommandData command = _bufferedCommand;

            // 기억하는 동안 땅 / 공중 상태가 바뀌어 쓸 수 없게 된 커맨드는 버림
            bool isGrounded = IsGrounded();
            bool isUsableState = command.usableState == CharacterCommand.UsableState.Both ||
                                 (command.usableState == CharacterCommand.UsableState.Ground) == isGrounded;
            if (!isUsableState)
            {
                _bufferedCommand = null;
                return;
            }

            if (CanStartCommand(command))
            {
                _bufferedCommand = null;
                StartAttack(command.attack, _bufferedButton, isSpecial: IsSpecialCommand(command));
                return;
            }

            // 이번 공격이 상대에게 닿았으면 공격이 끝날 때까지 입력을 계속 기억함
            // (캔슬할 수 있으면 회수에 들어갈 때, 아니면 공격이 끝난 뒤 첫 프레임에 나감. 회수가 긴 기술 뒤에 잇는 기술이 사라지지 않도록)
            if (IsAttacking && !IsLanding && _lastHitIndex >= 0) return;

            _bufferedFramesLeft--;
            if (_bufferedFramesLeft <= 0) _bufferedCommand = null;
        }

        /// <summary>
        /// attack을 시작함. 발생 / 지속 / 회수 프레임은 button에 맞춘 값을 씀 (AttackData의 버튼별 프레임)
        /// isSpecial: 필살기 커맨드로 나간 공격인지 (히트하면 끝날 때까지 선입력을 받지 않음)
        /// </summary>
        public void StartAttack(AttackData attack, CharacterCommand.AttackButtonType button, bool isSpecial = false)
        {
            _isSpecialAttack = isSpecial;
            _isBufferLocked = false;

            // 캔슬이면 맞고 있는 상대를 기억해 둠 (아래에서 값을 지우기 전에)
            CharacterHitReaction cancelledTarget = CanCancelInto(attack) ? _lastHitReaction : CanReplaceChainedStartup(attack) ? _chainTarget : null;

            _attackStartedFrame = Time.frameCount;
            _bufferedCommand = null;
            _lastHitReaction = null;
            _chainTarget = cancelledTarget;
            CurrentAttack = attack;
            CurrentFrames = attack.GetFrames(button);
            _currentButton = button;
            AttackFrame = 0; // 이번 프레임 Update에서 1이 됨
            if (cancelledTarget != null) cancelledTarget.ExtendHitStun(GetFramesUntilHit(attack, button, cancelledTarget.transform));
            _isAirAttack = !IsGrounded();
            _lastHitIndex = -1;
            _pendingLandingAttack = null; // 공중에서 다시 공격하면 새 공격의 착지 경직을 따름

            var windows = attack.TraitWindows;
            _remainingArmorHits = new int[windows.Length];
            for (int i = 0; i < windows.Length; i++)
            {
                _remainingArmorHits[i] = windows[i].ArmorHitCount;
            }

            ApplyCharacterState(attack);
        }

        /// <summary>재시작할 때 호출. 진행 중인 공격과 기다리던 착지 경직을 모두 취소</summary>
        public void CancelAttack()
        {
            _bufferedCommand = null;
            _pendingLandingAttack = null;
            if (!IsAttacking) return;
            EndAttack();
        }

        // 착지 경직 시작. 이 동안은 공격 중으로 취급해서 움직일 수 없음
        private void StartLanding(AttackData attack)
        {
            CurrentAttack = attack;
            _pendingLandingAttack = null;
            _landingFrame = 1;
            SetPhase(AttackPhase.Landing);
        }

        private void EndAttack()
        {
            _chainTarget = null;
            _isSpecialAttack = false;
            _isBufferLocked = false;
            CurrentAttack = null;
            AttackFrame = 0;
            _landingFrame = 0;
            SetPhase(AttackPhase.None);
            if (CA_characterState != null) CA_characterState.ClearAttack();
        }

        // 프레임 목록에 적은 종류대로 정함
        // 발생의 마지막 프레임과 공격 판정 첫 프레임은 같은 프레임이라서, 그 프레임은 지속(공격 판정)으로 봄
        private AttackPhase GetPhaseAt(int frame)
        {
            switch (CurrentFrames.GetFrameType(frame))
            {
                case FrameType.Active: return AttackPhase.Active;
                case FrameType.Recovery: return AttackPhase.Recovery;
                default: return AttackPhase.Startup;
            }
        }

        private void SetPhase(AttackPhase phase)
        {
            CurrentPhase = phase;
        }

        // 이 공격의 판정 높이와 특성(장풍, 잡기, 슈퍼아머)을 CharactorState에 넣음 (공격이 끝나면 EndAttack에서 지움)
        // 잡기는 판정 높이가 없음. 장풍은 투사체를 쏘는 공격이거나 장풍 특성이 있는 공격
        private void ApplyCharacterState(AttackData attack)
        {
            if (CA_characterState == null) return;

            CA_characterState.ClearAttack();
            CA_characterState.Grap = attack.HasTrait(AttackTrait.Throw);
            CA_characterState.Projectile = attack.ProjectilePrefab != null || attack.HasTrait(AttackTrait.Projectile);
            CA_characterState.SuperArmor = attack.HasTrait(AttackTrait.SuperArmor);
            if (CA_characterState.Grap) return;

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

        /// <summary>이 캐릭터의 공격(투사체 포함)이 상대에게 damage를 줬을 때 CharacterHealth에서 호출 (단일 데미지 기록)</summary>
        public void NotifyDamageDealt(int damage)
        {
            if (CA_characterState != null) CA_characterState.SetDealtDamage(damage);
        }

        /// <summary>
        /// 지금 맞으면 어떤 히트가 되는지.
        /// 공격의 발생 / 지속 중이면 카운터, 회수 / 착지 경직 중이면 퍼니시 카운터, 공격 중이 아니면 노말 히트
        /// </summary>
        public HitType GetHitTypeOnHit()
        {
            if (!IsAttacking) return HitType.Normal;

            switch (CurrentPhase)
            {
                case AttackPhase.Recovery:
                case AttackPhase.Landing:
                    return HitType.PunishCounter;
                default:
                    return HitType.Counter; // 발생, 지속 (공격을 시작한 프레임에 아직 단계가 정해지기 전도 발생으로 봄)
            }
        }

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
