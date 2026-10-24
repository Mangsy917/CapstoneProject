using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Character
{
    // 격투게임 커맨드(기술) 판정
    // - 매 프레임 방향 입력과 그 프레임에 새로 눌린 버튼을 입력 버퍼에 쌓아둠
    // - 버퍼는 _bufferFrames 프레임만큼만 보관하고, 그보다 오래된 입력은 버림
    // - 공격 버튼이 눌린 순간 버퍼를 최신 → 과거 순으로 읽어서, 등록된 커맨드와 일치하면 기술이 나감
    // CharacterMove가 이번 프레임의 좌우 판정을 끝낸 뒤에 읽도록 실행 순서를 뒤로 미룸
    [DefaultExecutionOrder(10)]
    public class CharacterCommand : MonoBehaviour
    {
        [Flags]
        public enum AttackButtonType
        {
            L = 1 << 0,
            M = 1 << 1,
            H = 1 << 2,
            EX = 1 << 3
        }

        // 기술을 쓸 수 있는 상태
        public enum UsableState
        {
            Ground, // 땅에서만
            Air,    // 점프 중에만
            Both    // 어디서나
        }

        [Serializable]
        public class CommandData
        {
            [Tooltip("커맨드 이름 (로그에 표시됨)")]
            public string commandName;

            [Tooltip("순서대로 입력해야 하는 레버 방향. 예) 236. 비워두면 레버 입력 순서는 보지 않음")]
            public int[] directionSequence;

            [Tooltip("버튼을 누르는 순간 레버가 이 방향 중 하나여야 함. 예) 앉아서 쓰는 기술은 1, 2, 3. 비워두면 상관없음")]
            public int[] holdDirections;

            [Tooltip("기술을 쓸 수 있는 상태 (땅 / 점프 중 / 어디서나)")]
            public UsableState usableState = UsableState.Ground;

            [Tooltip("눌러야 인식되는 버튼. 여러 개를 고르면 그중 아무거나 눌러도 됨")]
            public AttackButtonType buttons;

            [Tooltip("체크하면 고른 버튼을 전부 (comboWindowFrames 이내에) 같이 눌러야 함")]
            public bool requireAllButtons = false;

            [Tooltip("동시입력 유예 프레임. 0이면 기본값 사용")]
            public int comboWindowFrames = 0;

            [Tooltip("레버 방향 사이, 마지막 방향과 버튼 사이의 유예 프레임. 0이면 기본값 사용 (클수록 널널한 판정)")]
            public int windowFrames = 0;

            [Tooltip("방향마다 따로 정하는 유예 프레임 (directionSequence와 개수가 같아야 적용, 0인 칸은 windowFrames 사용)")]
            public int[] stepWindowFrames;

            [Tooltip("여러 커맨드가 동시에 맞으면 이 값이 큰 것이 나감")]
            public int priority = 0;

            [Tooltip("커맨드가 성공하면 나갈 공격")]
            public AttackData attack;
        }

        [Header("입력")]
        [Tooltip("방향, 버튼 입력을 읽어올 InputReader. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private InputReader CC_inputReader;
        [Tooltip("바라보는 방향, 땅/공중 상태를 알려줄 CharacterMove. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private CharacterMove CC_characterMove;

        [Header("입력 버퍼")]
        [Tooltip("입력 버퍼에 보관할 프레임 수. 이보다 오래된 입력은 버려지므로, 커맨드 전체가 이 프레임 안에 들어와야 성공합니다.")]
        [SerializeField] private int _bufferFrames = 20;

        [Header("유예 프레임")]
        [Tooltip("각 커맨드의 windowFrames를 0(또는 음수)으로 두면 이 값이 기본값으로 사용됩니다.\n" +
                 "방향과 방향 사이, 마지막 방향과 버튼 사이에 허용하는 프레임 수입니다.")]
        [SerializeField] private int _defaultCommandWindowFrames = 4;
        [Tooltip("requireAllButtons(동시입력) 커맨드에서 comboWindowFrames를 0(또는 음수)으로 두면 이 값이 기본값으로 사용됩니다.\n" +
                 "예) 2로 두면 L, M을 2프레임 이내에 누른 경우까지 '동시에 누름'으로 인정합니다.")]
        [SerializeField] private int _defaultComboWindowFrames = 2;

        [Header("방향 기준")]
        [Tooltip("체크하면 커맨드를 '바라보는 방향' 기준으로 판정합니다.\n" +
                 "예) 오른쪽에 서 있을 때도 화면상 214가 아니라 커맨드표 그대로 236으로 파동권이 나갑니다.\n" +
                 "끄면 화면상 방향(절대 방향) 그대로 판정합니다.")]
        [SerializeField] private bool _useRelativeDirection = true;

        [Header("등록된 커맨드 목록")]
        [SerializeField] private List<CommandData> CC_commandList = new List<CommandData>();

        /// <summary>가장 최근에 성공한 커맨드</summary>
        public CommandData LastCommand { get; private set; }

        /// <summary>
        /// 커맨드 입력에 성공했을 때 호출되는 이벤트 (공격/애니메이션 스크립트에서 구독)
        /// 두 번째 값은 커맨드를 완성한 버튼 (예: 236L이면 L). 공격이 버튼마다 다른 프레임을 쓸 때 사용
        /// </summary>
        public event Action<CommandData, AttackButtonType> OnCommandTriggered;

        /// <summary>
        /// 지금 쓸 수 있는 커맨드인지 알려주는 함수 (비어 있으면 모두 쓸 수 있음)
        /// false인 커맨드는 입력이 맞아도 건너뛰고, 우선순위가 그다음인 커맨드를 찾음
        /// 예) 장풍이 남아 있으면 236L은 건너뛰고, 같은 L 버튼의 일반 공격이 나감
        /// </summary>
        public Func<CommandData, bool> CanUseCommand { get; set; }

        private static readonly AttackButtonType[] _allButtonFlags =
        {
            AttackButtonType.L, AttackButtonType.M, AttackButtonType.H, AttackButtonType.EX
        };

        // 입력 버퍼에 쌓이는 한 프레임의 입력
        private struct BufferedInput
        {
            public int frame;
            public int direction;
            public AttackButtonType pressedButtons; // 이 프레임에 새로 눌린 버튼

            public BufferedInput(int frame, int direction, AttackButtonType pressedButtons)
            {
                this.frame = frame;
                this.direction = direction;
                this.pressedButtons = pressedButtons;
            }
        }

        // 오래된 입력이 앞, 최신 입력이 뒤
        private readonly List<BufferedInput> _inputBuffer = new List<BufferedInput>();
        private List<CommandData> _sortedCommandList;
        private int CC_frameCounter = 0;

        private void Awake()
        {
            if (CC_inputReader == null) CC_inputReader = GetComponent<InputReader>();
            if (CC_characterMove == null) CC_characterMove = GetComponent<CharacterMove>();

            if (CC_inputReader == null)
                Debug.LogError("[CharacterCommand] InputReader를 찾을 수 없습니다. Input Reader 칸에 지정해 주세요.", this);

            // priority가 높은 커맨드부터, 그다음은 방향 시퀀스가 긴(=더 구체적인) 커맨드부터
            // 검사하도록 미리 정렬해둔다. (예: 623L이 236L보다 먼저 검사되도록 priority로 순서를 명시)
            _sortedCommandList = CC_commandList
                .OrderByDescending(c => c.priority)
                .ThenByDescending(c => c.directionSequence?.Length ?? 0)
                .ToList();
        }

        private void Update()
        {
            if (CC_inputReader == null) return;

            // 슬로우 모션 중에는 건너뛰는 프레임이 있음 (GameSpeed)
            if (!Game.Field.GameSpeed.IsTickFrame) return;

            CC_frameCounter++;

            AttackButtonType pressedButtons = ReadPressedButtons();
            AddToBuffer(new BufferedInput(CC_frameCounter, ReadDirection(), pressedButtons));

            if (pressedButtons != 0)
                CheckCommands(pressedButtons);
        }

        // =====================================================================
        // 입력 버퍼
        // =====================================================================

        private int ReadDirection()
        {
            int rawDirection = CC_inputReader.Direction;
            if (!_useRelativeDirection || CC_characterMove == null) return rawDirection;

            return CharacterMove.ToRelativeDirection(rawDirection, CC_characterMove.IsFacingRight);
        }

        private AttackButtonType ReadPressedButtons()
        {
            AttackButtonType pressedButtons = 0;
            if (CC_inputReader.buttonLDown) pressedButtons |= AttackButtonType.L;
            if (CC_inputReader.buttonMDown) pressedButtons |= AttackButtonType.M;
            if (CC_inputReader.buttonHDown) pressedButtons |= AttackButtonType.H;
            if (CC_inputReader.buttonEXDown) pressedButtons |= AttackButtonType.EX;
            return pressedButtons;
        }

        /// <summary>재시작할 때 호출. 버퍼에 쌓인 입력을 지워서 재시작 전 입력으로 기술이 나가지 않게 함</summary>
        public void ClearBuffer()
        {
            _inputBuffer.Clear();
        }

        private void AddToBuffer(BufferedInput input)
        {
            _inputBuffer.Add(input);

            // _bufferFrames를 넘는 오래된 입력은 버림
            int removeCount = _inputBuffer.Count - _bufferFrames;
            if (removeCount > 0)
                _inputBuffer.RemoveRange(0, removeCount);
        }

        // =====================================================================
        // 커맨드 판정
        // =====================================================================

        private void CheckCommands(AttackButtonType pressedButtons)
        {
            int currentDirection = _inputBuffer[_inputBuffer.Count - 1].direction;
            bool isGrounded = CC_characterMove == null || CC_characterMove.IsGrounded;

            // 한 프레임에 여러 버튼이 동시에 눌리는 경우(예: L, M이 정확히 같은 프레임에 눌림),
            // 같은 콤보 커맨드가 버튼별로 중복 출력되지 않도록 이번 프레임에 이미 채택된 커맨드를 기록해둔다.
            var triggeredThisFrame = new HashSet<CommandData>();

            foreach (var button in _allButtonFlags)
            {
                if ((pressedButtons & button) == 0) continue;

                foreach (var command in _sortedCommandList)
                {
                    if (triggeredThisFrame.Contains(command)) continue;

                    // 지금 쓸 수 없는 커맨드(예: 장풍이 남아 있을 때의 장풍)는 건너뛰고 다음 커맨드를 찾음
                    if (CanUseCommand != null && !CanUseCommand(command)) continue;

                    // 커맨드에 이 버튼이 체크돼 있지 않으면(비트가 없으면) 건너뛴다.
                    if ((command.buttons & button) == 0) continue;

                    // 동시입력(콤보) 커맨드는 buttons에 포함된 버튼이 전부, 서로 comboWindowFrames 이내에 눌려 있어야 한다.
                    if (command.requireAllButtons && !IsButtonComboSatisfied(command)) continue;

                    // 땅 / 점프 중 조건
                    if (command.usableState == UsableState.Ground && !isGrounded) continue;
                    if (command.usableState == UsableState.Air && isGrounded) continue;

                    // 버튼을 누르는 순간의 레버 방향 조건 (예: 2L은 1, 2, 3 중 하나)
                    if (command.holdDirections != null && command.holdDirections.Length > 0 &&
                        Array.IndexOf(command.holdDirections, currentDirection) < 0) continue;

                    // directionSequence가 비어 있으면 방향 조건 없이(버튼 조건만으로) 통과시킨다.
                    bool isMotionMatched = command.directionSequence == null || command.directionSequence.Length == 0
                        || IsMotionInBuffer(command);

                    if (isMotionMatched)
                    {
                        Debug.Log($"[Command] '{command.commandName}' 커맨드 입력 성공! (버튼: {button}, 프레임: {CC_frameCounter})");
                        triggeredThisFrame.Add(command);
                        LastCommand = command;
                        OnCommandTriggered?.Invoke(command, button);
                        break; // 이 버튼에 대해서는 가장 구체적인/우선순위 높은 커맨드 하나만 채택하고 종료
                    }
                }
            }
        }

        // 요구 버튼이 모두 눌려 있고, 버퍼에 남은 마지막 누른 프레임끼리의 차이가 comboWindowFrames 이내인지 확인
        private bool IsButtonComboSatisfied(CommandData command)
        {
            int? minFrame = null;
            int? maxFrame = null;

            foreach (var flag in _allButtonFlags)
            {
                if ((command.buttons & flag) == 0) continue; // 이 커맨드가 요구하는 버튼이 아님

                if (!IsButtonCurrentlyPressed(flag)) return false; // 요구 버튼 중 하나라도 안 눌려 있으면 실패

                int? pressFrame = FindLastPressFrame(flag);
                if (pressFrame == null) return false; // 버퍼 안에서 누른 기록이 없으면(너무 오래전에 누름) 실패

                if (minFrame == null || pressFrame < minFrame) minFrame = pressFrame;
                if (maxFrame == null || pressFrame > maxFrame) maxFrame = pressFrame;
            }

            if (minFrame == null) return false; // buttons에 체크된 버튼이 하나도 없음

            int window = command.comboWindowFrames > 0 ? command.comboWindowFrames : _defaultComboWindowFrames;
            return (maxFrame.Value - minFrame.Value) <= window;
        }

        private int? FindLastPressFrame(AttackButtonType flag)
        {
            for (int i = _inputBuffer.Count - 1; i >= 0; i--)
            {
                if ((_inputBuffer[i].pressedButtons & flag) != 0)
                    return _inputBuffer[i].frame;
            }
            return null;
        }

        private bool IsButtonCurrentlyPressed(AttackButtonType flag)
        {
            switch (flag)
            {
                case AttackButtonType.L: return CC_inputReader.buttonL;
                case AttackButtonType.M: return CC_inputReader.buttonM;
                case AttackButtonType.H: return CC_inputReader.buttonH;
                case AttackButtonType.EX: return CC_inputReader.buttonEX;
                default: return false;
            }
        }

        private int GetGapWindow(CommandData command, int gapIndex)
        {
            var steps = command.stepWindowFrames;
            if (steps != null && steps.Length == command.directionSequence.Length &&
                gapIndex >= 0 && gapIndex < steps.Length && steps[gapIndex] > 0)
            {
                return steps[gapIndex];
            }

            return command.windowFrames > 0 ? command.windowFrames : _defaultCommandWindowFrames;
        }

        // 버퍼를 최신 → 과거 순으로 읽으며, 커맨드의 방향을 뒤에서부터 순서대로 찾는다.
        // 방향을 "새로 누른 프레임"(직전 프레임과 방향이 다른 프레임)만 보므로,
        // 같은 방향을 누르고 있는 동안은 한 번의 입력으로 취급한다. (22는 2를 두 번 눌러야 함)
        private bool IsMotionInBuffer(CommandData command)
        {
            int[] motion = command.directionSequence;
            int motionIndex = motion.Length - 1;
            int lastMatchedFrame = CC_frameCounter; // 버튼이 눌린 현재 프레임을 기준점으로 시작

            // 버퍼의 가장 오래된 입력은 언제 처음 눌렸는지 알 수 없으므로 확인하지 않는다.
            for (int i = _inputBuffer.Count - 1; i >= 1 && motionIndex >= 0; i--)
            {
                BufferedInput entry = _inputBuffer[i];
                if (entry.direction == _inputBuffer[i - 1].direction) continue;
                if (entry.direction != motion[motionIndex]) continue; // 스쳐 지나가는 방향은 건너뜀

                // 직전에 매칭된 시점(버튼 입력 또는 다음 방향)과의 프레임 차가
                // 이 구간에 허용된 유예 프레임을 넘으면 실패로 처리한다.
                if (lastMatchedFrame - entry.frame > GetGapWindow(command, motionIndex))
                    return false;

                lastMatchedFrame = entry.frame;
                motionIndex--;
            }

            return motionIndex < 0; // 모든 방향 단계를 버퍼 안에서 순서대로 다 찾았으면 성공
        }

        private void OnValidate()
        {
            if (_bufferFrames < 2) _bufferFrames = 2;
            if (_defaultCommandWindowFrames < 1) _defaultCommandWindowFrames = 1;
            if (_defaultComboWindowFrames < 0) _defaultComboWindowFrames = 0;
        }
    }
}
