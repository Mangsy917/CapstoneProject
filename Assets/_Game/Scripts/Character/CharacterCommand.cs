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

        [Serializable]
        public class CommandData
        {
            public string commandName;
            public int[] directionSequence;
            public AttackButtonType buttons;
            public bool requireAllButtons = false;
            public int comboWindowFrames = 2;
            public bool requireNeutralDirection = false;
            public int windowFrames = 0;
            public int[] stepWindowFrames;
            public int priority = 0;
            public AttackData attack;
        }

        [Header("입력")]
        [Tooltip("방향, 버튼 입력을 읽어올 InputReader. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private InputReader CC_inputReader;
        [Tooltip("바라보는 방향을 알려줄 CharacterMove. 비워두면 같은 오브젝트에서 찾음")]
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
        [Tooltip("commandName은 커맨드 이름, " +
            "directionSequence은 레버 커맨드 숫자, " +
            "buttons은 눌러야 인식되는 버튼, " +
            "requireAllButtons은 버튼에서 선택한 버튼들을 전부 눌러야 기술이 나가도록 하는 기능, " +
            "comboWindowFrames은 버튼을 물리적으로 완벽하게 동시에 누를 수 없으니, 동시에 누를 수 있는 유예 프레임 시간, " +
            "requireNeutralDirection은 레버 입력 없이 버튼만 눌러도 나가는 기술들일 때 사용, " +
            "windowFrames은 각 버튼이나 레버 사이에 연속적으로 이어 질 수 있도록 유예 프레임 시간을 뜻함(숫자가 높을수록 널널한판정, 숫자가 낮을수록 빡빡한판정, " +
            "stepWindowFrames은 레버 방향 입력 시 다음 입력이 몇프레임 이내에 이어져야 커맨드가 성공인지, " +
            "priority는 기술을 읽을때의 우선도, " +
            "attack은 커맨드가 성공하면 나갈 공격(AttackData)")]
        [SerializeField]
        private List<CommandData> CC_commandList = new List<CommandData>
        {
            new CommandData
            {
                commandName = "전인파동권",
                directionSequence = new int[] { 2, 3, 6, 2, 3, 6 },
                buttons = AttackButtonType.L | AttackButtonType.M | AttackButtonType.H | AttackButtonType.EX,
                windowFrames = 5,
                stepWindowFrames = new int[] { 0, 0, 12, 0, 0, 0 },
                priority = 20
            },
            new CommandData
            {
                commandName = "전인파장격",
                directionSequence = new int[] { 2, 1, 4, 2, 1, 4 },
                buttons = AttackButtonType.L | AttackButtonType.M | AttackButtonType.H | AttackButtonType.EX,
                windowFrames = 5,
                stepWindowFrames = new int[] { 0, 0, 12, 0, 0, 0 },
                priority = 20
            },
            new CommandData
            {
                commandName = "약승룡권",
                directionSequence = new int[] { 6, 2, 3 },
                buttons = AttackButtonType.L,
                windowFrames = 4,
                stepWindowFrames = new int[] { 10, 8, 8 },
                priority = 10
            },
            new CommandData
            {
                commandName = "중승룡권",
                directionSequence = new int[] { 6, 2, 3 },
                buttons = AttackButtonType.M,
                windowFrames = 4,
                stepWindowFrames = new int[] { 10, 8, 8 },
                priority = 10
            },
            new CommandData
            {
                commandName = "강승룡권",
                directionSequence = new int[] { 6, 2, 3 },
                buttons = AttackButtonType.H,
                windowFrames = 5,
                stepWindowFrames = new int[] { 10, 8, 8 },
                priority = 10
            },
            new CommandData
            {
                commandName = "EX승룡권",
                directionSequence = new int[] { 6, 2, 3 },
                buttons = AttackButtonType.EX,
                windowFrames = 5,
                stepWindowFrames = new int[] { 10, 8, 8 },
                priority = 10
            },
            new CommandData
            {
                commandName = "약파동권",
                directionSequence = new int[] { 2, 3, 6 },
                buttons = AttackButtonType.L,
                windowFrames = 5,
                priority = 0
            },
            new CommandData
            {
                commandName = "중파동권",
                directionSequence = new int[] { 2, 3, 6 },
                buttons = AttackButtonType.M,
                windowFrames = 5,
                priority = 0
            },
            new CommandData
            {
                commandName = "강파동권",
                directionSequence = new int[] { 2, 3, 6 },
                buttons = AttackButtonType.H,
                windowFrames = 5,
                priority = 0
            },
            new CommandData
            {
                commandName = "EX파동권",
                directionSequence = new int[] { 2, 3, 6 },
                buttons = AttackButtonType.EX,
                windowFrames = 5,
                priority = 0
            },
            new CommandData
            {
                commandName = "약파장격",
                directionSequence = new int[] { 2, 1, 4 },
                buttons = AttackButtonType.L,
                windowFrames = 5,
                priority = 0
            },
            new CommandData
            {
                commandName = "중파장격",
                directionSequence = new int[] { 2, 1, 4 },
                buttons = AttackButtonType.M,
                windowFrames = 5,
                priority = 0
            },
            new CommandData
            {
                commandName = "강파장격",
                directionSequence = new int[] { 2, 1, 4 },
                buttons = AttackButtonType.H,
                windowFrames = 5,
                priority = 0
            },
            new CommandData
            {
                commandName = "EX파장격",
                directionSequence = new int[] { 2, 1, 4 },
                buttons = AttackButtonType.EX,
                windowFrames = 5,
                priority = 0
            },
            new CommandData
            {
                commandName = "전인스택",
                directionSequence = new int[] { 2, 2 },
                buttons = AttackButtonType.L | AttackButtonType.M | AttackButtonType.H | AttackButtonType.EX,
                windowFrames = 10,
                priority = 5
            },
            new CommandData
            {
                commandName = "잡기",
                directionSequence = new int[0],
                buttons = AttackButtonType.L | AttackButtonType.M,
                requireAllButtons = true,
                comboWindowFrames = 2,
                requireNeutralDirection = true,
                priority = 5
            },
            new CommandData
            {
                commandName = "도발",
                directionSequence = new int[0],
                buttons = AttackButtonType.L | AttackButtonType.M | AttackButtonType.H | AttackButtonType.EX,
                requireAllButtons = true,
                comboWindowFrames = 2,
                requireNeutralDirection = true,
                priority = 10
            }
        };

        /// <summary>가장 최근에 성공한 커맨드</summary>
        public CommandData LastCommand { get; private set; }

        /// <summary>커맨드 입력에 성공했을 때 호출되는 이벤트 (공격/애니메이션 스크립트에서 구독)</summary>
        public event Action<CommandData> OnCommandTriggered;

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

            // 한 프레임에 여러 버튼이 동시에 눌리는 경우(예: L, M이 정확히 같은 프레임에 눌림),
            // 같은 콤보 커맨드가 버튼별로 중복 출력되지 않도록 이번 프레임에 이미 채택된 커맨드를 기록해둔다.
            var triggeredThisFrame = new HashSet<CommandData>();

            foreach (var button in _allButtonFlags)
            {
                if ((pressedButtons & button) == 0) continue;

                foreach (var command in _sortedCommandList)
                {
                    if (triggeredThisFrame.Contains(command)) continue;

                    // 커맨드에 이 버튼이 체크돼 있지 않으면(비트가 없으면) 건너뛴다.
                    if ((command.buttons & button) == 0) continue;

                    // 동시입력(콤보) 커맨드는 buttons에 포함된 버튼이 전부, 서로 comboWindowFrames 이내에 눌려 있어야 한다.
                    if (command.requireAllButtons && !IsButtonComboSatisfied(command)) continue;

                    // 방향이 중립일 때만 발동하는 커맨드(예: 중립 잡기) 조건 체크
                    if (command.requireNeutralDirection && currentDirection != 5) continue;

                    // directionSequence가 비어 있으면 방향 조건 없이(버튼 조건만으로) 통과시킨다.
                    bool isMotionMatched = command.directionSequence == null || command.directionSequence.Length == 0
                        || IsMotionInBuffer(command);

                    if (isMotionMatched)
                    {
                        Debug.Log($"[Command] '{command.commandName}' 커맨드 입력 성공! (버튼: {button}, 프레임: {CC_frameCounter})");
                        triggeredThisFrame.Add(command);
                        LastCommand = command;
                        OnCommandTriggered?.Invoke(command);
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
