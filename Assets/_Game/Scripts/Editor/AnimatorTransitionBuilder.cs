using System.Collections.Generic;
using Game.Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor
{
    // 선택한 캐릭터 Animator Controller에 CharacterAnimationPlayer가 쓰는 파라미터와 전이를 만듦
    // - 상태(클립)는 그대로 두고, 파라미터와 전이는 모두 지우고 다시 만듦 (손으로 고친 전이도 지워짐)
    // - 모든 전이는 Any State에서 출발하고 전이 시간 0 (어느 상태에서든 한 프레임에 바로 바뀌도록)
    // - 우선순위: 피격 > 가드 > 공격 > 이동
    // - 공격 상태와 착지 상태는 AttackData 에셋의 AnimationState / LandingAnimationState로 찾음
    public static class AnimatorTransitionBuilder
    {
        private const string MenuPath = "Tools/Animator/선택한 Animator 전이 다시 만들기";

        // 이동 상태 이름
        private const string IdleState = "Idle";
        private const string SitState = "Sit";
        private const string JumpState = "Jump";
        private const string WalkForwardState = "WalkForward";
        private const string WalkBackState = "WalkBack";

        // 피격 모션 상태 이름
        private const string TopHitState = "TopHit";
        private const string MiddleHitState = "MiddleHit";
        private const string LowHitState = "LowHit";
        private const string Top2LowHitState = "Top2LowHit";
        private const string Low2TopHitState = "Low2TopHit";

        // 가드 모션 상태 이름
        private const string TopGuardState = "TopGuard";
        private const string MiddleGuardState = "MiddleGuard";
        private const string BottomGuardState = "BottomGuard";

        private static readonly CharacterMove.MoveState[] _walkForwardMoveStates =
            { CharacterMove.MoveState.WalkForward, CharacterMove.MoveState.ForwardDash, CharacterMove.MoveState.Run };
        private static readonly CharacterMove.MoveState[] _walkBackMoveStates =
            { CharacterMove.MoveState.WalkBackGuard, CharacterMove.MoveState.BackDash };

        [MenuItem(MenuPath)]
        private static void BuildSelected()
        {
            var controller = (AnimatorController)Selection.activeObject;
            string message = $"{controller.name}의 파라미터와 전이를 모두 지우고 다시 만듭니다.\n손으로 고친 전이도 지워집니다.";
            if (!EditorUtility.DisplayDialog("Animator 전이 다시 만들기", message, "만들기", "취소")) return;
            Build(controller);
        }

        [MenuItem(MenuPath, true)]
        private static bool CanBuildSelected()
        {
            return Selection.activeObject is AnimatorController;
        }

        public static void Build(AnimatorController controller)
        {
            Undo.RegisterCompleteObjectUndo(controller, "Animator 전이 다시 만들기");
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

            var states = new Dictionary<string, AnimatorState>();
            foreach (ChildAnimatorState child in stateMachine.states) states[child.state.name] = child.state;

            ClearTransitions(stateMachine);
            controller.parameters = new AnimatorControllerParameter[0];
            AddParameters(controller);

            // 우선순위가 높은 전이부터 추가 (Any State 전이는 위에 있는 것부터 검사함)
            AddHitTransitions(stateMachine, states);
            AddGuardTransitions(stateMachine, states);
            List<string> attackStates = AddAttackTransitions(controller, stateMachine, states);
            AddMoveTransitions(stateMachine, states);

            if (states.TryGetValue(IdleState, out AnimatorState idle)) stateMachine.defaultState = idle;
            ArrangeStates(stateMachine, attackStates);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AnimatorTransitionBuilder] {controller.name}: 파라미터 {controller.parameters.Length}개, Any State 전이 {stateMachine.anyStateTransitions.Length}개를 만들었습니다.");
        }

        private static void ClearTransitions(AnimatorStateMachine stateMachine)
        {
            foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
                stateMachine.RemoveAnyStateTransition(transition);
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                foreach (AnimatorStateTransition transition in child.state.transitions)
                    child.state.RemoveTransition(transition);
            }
        }

        private static void AddParameters(AnimatorController controller)
        {
            controller.AddParameter(CharacterAnimationPlayer.IsGroundedParameter, AnimatorControllerParameterType.Bool);
            controller.AddParameter(CharacterAnimationPlayer.IsCrouchingParameter, AnimatorControllerParameterType.Bool);
            controller.AddParameter(CharacterAnimationPlayer.MoveStateParameter, AnimatorControllerParameterType.Int);
            controller.AddParameter(CharacterAnimationPlayer.IsAttackingParameter, AnimatorControllerParameterType.Bool);
            controller.AddParameter(CharacterAnimationPlayer.IsLandingParameter, AnimatorControllerParameterType.Bool);
            controller.AddParameter(CharacterAnimationPlayer.IsHitStunParameter, AnimatorControllerParameterType.Bool);
            controller.AddParameter(CharacterAnimationPlayer.IsGuardStunParameter, AnimatorControllerParameterType.Bool);
            controller.AddParameter(CharacterAnimationPlayer.HitParameter, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(CharacterAnimationPlayer.GuardParameter, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(CharacterAnimationPlayer.HitMotionParameter, AnimatorControllerParameterType.Int);
            controller.AddParameter(CharacterAnimationPlayer.GuardMotionParameter, AnimatorControllerParameterType.Int);

            // 처음 서 있는 상태
            AnimatorControllerParameter[] parameters = controller.parameters;
            foreach (AnimatorControllerParameter parameter in parameters)
            {
                if (parameter.name == CharacterAnimationPlayer.IsGroundedParameter) parameter.defaultBool = true;
            }
            controller.parameters = parameters;
        }

        // Hit 트리거 + HitMotion 값으로 피격 모션 선택 (Auto와 Middle은 MiddleHit)
        private static void AddHitTransitions(AnimatorStateMachine stateMachine, Dictionary<string, AnimatorState> states)
        {
            AddMotionTransition(stateMachine, states, TopHitState, CharacterAnimationPlayer.HitParameter, CharacterAnimationPlayer.HitMotionParameter, HitMotion.Top);
            AddMotionTransition(stateMachine, states, LowHitState, CharacterAnimationPlayer.HitParameter, CharacterAnimationPlayer.HitMotionParameter, HitMotion.Low);
            AddMotionTransition(stateMachine, states, Top2LowHitState, CharacterAnimationPlayer.HitParameter, CharacterAnimationPlayer.HitMotionParameter, HitMotion.Top2Low);
            AddMotionTransition(stateMachine, states, Low2TopHitState, CharacterAnimationPlayer.HitParameter, CharacterAnimationPlayer.HitMotionParameter, HitMotion.Low2Top);

            AnimatorStateTransition middle = AddAnyTransition(stateMachine, states, MiddleHitState, canTransitionToSelf: true);
            if (middle == null) return;
            middle.AddCondition(AnimatorConditionMode.If, 0f, CharacterAnimationPlayer.HitParameter);
            AddNotEqualConditions(middle, CharacterAnimationPlayer.HitMotionParameter, HitMotion.Top, HitMotion.Low, HitMotion.Top2Low, HitMotion.Low2Top);
        }

        // Guard 트리거 + GuardMotion 값으로 가드 모션 선택 (Top과 Low 외에는 MiddleGuard)
        private static void AddGuardTransitions(AnimatorStateMachine stateMachine, Dictionary<string, AnimatorState> states)
        {
            AddMotionTransition(stateMachine, states, TopGuardState, CharacterAnimationPlayer.GuardParameter, CharacterAnimationPlayer.GuardMotionParameter, HitMotion.Top);
            AddMotionTransition(stateMachine, states, BottomGuardState, CharacterAnimationPlayer.GuardParameter, CharacterAnimationPlayer.GuardMotionParameter, HitMotion.Low);

            AnimatorStateTransition middle = AddAnyTransition(stateMachine, states, MiddleGuardState, canTransitionToSelf: true);
            if (middle == null) return;
            middle.AddCondition(AnimatorConditionMode.If, 0f, CharacterAnimationPlayer.GuardParameter);
            AddNotEqualConditions(middle, CharacterAnimationPlayer.GuardMotionParameter, HitMotion.Top, HitMotion.Low);
        }

        // 공격마다 같은 이름의 트리거를 만들고, 착지 상태가 있으면 공격 → 착지 전이도 만듦
        private static List<string> AddAttackTransitions(AnimatorController controller, AnimatorStateMachine stateMachine, Dictionary<string, AnimatorState> states)
        {
            var attackStates = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:AttackData"))
            {
                var attack = AssetDatabase.LoadAssetAtPath<AttackData>(AssetDatabase.GUIDToAssetPath(guid));
                if (attack == null || string.IsNullOrEmpty(attack.AnimationState)) continue;
                if (!states.TryGetValue(attack.AnimationState, out AnimatorState attackState)) continue; // 다른 캐릭터의 공격
                if (attackStates.Contains(attack.AnimationState)) continue;
                attackStates.Add(attack.AnimationState);

                controller.AddParameter(attack.AnimationState, AnimatorControllerParameterType.Trigger);
                AnimatorStateTransition transition = AddAnyTransition(stateMachine, states, attack.AnimationState, canTransitionToSelf: true);
                transition.AddCondition(AnimatorConditionMode.If, 0f, attack.AnimationState);

                if (string.IsNullOrEmpty(attack.LandingAnimationState)) continue;
                if (!states.TryGetValue(attack.LandingAnimationState, out AnimatorState landingState))
                {
                    Debug.LogWarning($"[AnimatorTransitionBuilder] {attack.name}의 착지 상태 {attack.LandingAnimationState}가 {controller.name}에 없습니다.");
                    continue;
                }
                AnimatorStateTransition landing = attackState.AddTransition(landingState);
                SetInstant(landing);
                landing.AddCondition(AnimatorConditionMode.If, 0f, CharacterAnimationPlayer.IsLandingParameter);
            }
            attackStates.Sort();
            return attackStates;
        }

        // 공격 / 피격 / 가드 중이 아닐 때만 이동 상태로 감
        private static void AddMoveTransitions(AnimatorStateMachine stateMachine, Dictionary<string, AnimatorState> states)
        {
            AnimatorStateTransition jump = AddAnyMoveTransition(stateMachine, states, JumpState);
            jump?.AddCondition(AnimatorConditionMode.IfNot, 0f, CharacterAnimationPlayer.IsGroundedParameter);

            AnimatorStateTransition sit = AddAnyMoveTransition(stateMachine, states, SitState);
            if (sit != null)
            {
                sit.AddCondition(AnimatorConditionMode.If, 0f, CharacterAnimationPlayer.IsGroundedParameter);
                sit.AddCondition(AnimatorConditionMode.If, 0f, CharacterAnimationPlayer.IsCrouchingParameter);
            }

            // 조건 하나에 값 하나만 비교할 수 있어서 MoveState 값마다 전이를 하나씩 만듦
            foreach (CharacterMove.MoveState moveState in _walkForwardMoveStates) AddStandingMoveTransition(stateMachine, states, WalkForwardState, moveState);
            foreach (CharacterMove.MoveState moveState in _walkBackMoveStates) AddStandingMoveTransition(stateMachine, states, WalkBackState, moveState);

            AnimatorStateTransition idle = AddAnyMoveTransition(stateMachine, states, IdleState);
            if (idle == null) return;
            idle.AddCondition(AnimatorConditionMode.If, 0f, CharacterAnimationPlayer.IsGroundedParameter);
            idle.AddCondition(AnimatorConditionMode.IfNot, 0f, CharacterAnimationPlayer.IsCrouchingParameter);
            foreach (CharacterMove.MoveState moveState in _walkForwardMoveStates)
                idle.AddCondition(AnimatorConditionMode.NotEqual, (int)moveState, CharacterAnimationPlayer.MoveStateParameter);
            foreach (CharacterMove.MoveState moveState in _walkBackMoveStates)
                idle.AddCondition(AnimatorConditionMode.NotEqual, (int)moveState, CharacterAnimationPlayer.MoveStateParameter);
        }

        private static void AddStandingMoveTransition(AnimatorStateMachine stateMachine, Dictionary<string, AnimatorState> states, string stateName, CharacterMove.MoveState moveState)
        {
            AnimatorStateTransition transition = AddAnyMoveTransition(stateMachine, states, stateName);
            if (transition == null) return;
            transition.AddCondition(AnimatorConditionMode.If, 0f, CharacterAnimationPlayer.IsGroundedParameter);
            transition.AddCondition(AnimatorConditionMode.IfNot, 0f, CharacterAnimationPlayer.IsCrouchingParameter);
            transition.AddCondition(AnimatorConditionMode.Equals, (int)moveState, CharacterAnimationPlayer.MoveStateParameter);
        }

        private static AnimatorStateTransition AddAnyMoveTransition(AnimatorStateMachine stateMachine, Dictionary<string, AnimatorState> states, string stateName)
        {
            AnimatorStateTransition transition = AddAnyTransition(stateMachine, states, stateName, canTransitionToSelf: false);
            if (transition == null) return null;
            transition.AddCondition(AnimatorConditionMode.IfNot, 0f, CharacterAnimationPlayer.IsAttackingParameter);
            transition.AddCondition(AnimatorConditionMode.IfNot, 0f, CharacterAnimationPlayer.IsHitStunParameter);
            transition.AddCondition(AnimatorConditionMode.IfNot, 0f, CharacterAnimationPlayer.IsGuardStunParameter);
            return transition;
        }

        private static void AddMotionTransition(AnimatorStateMachine stateMachine, Dictionary<string, AnimatorState> states, string stateName, string triggerName, string motionName, HitMotion motion)
        {
            AnimatorStateTransition transition = AddAnyTransition(stateMachine, states, stateName, canTransitionToSelf: true);
            if (transition == null) return;
            transition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
            transition.AddCondition(AnimatorConditionMode.Equals, (int)motion, motionName);
        }

        private static void AddNotEqualConditions(AnimatorStateTransition transition, string parameterName, params HitMotion[] motions)
        {
            foreach (HitMotion motion in motions)
                transition.AddCondition(AnimatorConditionMode.NotEqual, (int)motion, parameterName);
        }

        private static AnimatorStateTransition AddAnyTransition(AnimatorStateMachine stateMachine, Dictionary<string, AnimatorState> states, string stateName, bool canTransitionToSelf)
        {
            if (!states.TryGetValue(stateName, out AnimatorState state))
            {
                Debug.LogWarning($"[AnimatorTransitionBuilder] {stateName} 상태가 없어서 전이를 만들지 않았습니다.");
                return null;
            }
            AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(state);
            SetInstant(transition);
            transition.canTransitionToSelf = canTransitionToSelf;
            return transition;
        }

        // 격투 게임은 프레임 단위로 바뀌어야 하므로 섞지 않고 바로 바꿈
        private static void SetInstant(AnimatorStateTransition transition)
        {
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0f;
            transition.offset = 0f;
            transition.interruptionSource = TransitionInterruptionSource.None;
        }

        // 겹쳐 있는 상태를 이동 / 피격 / 가드 / 공격 / 그 외 열로 펼침
        private static void ArrangeStates(AnimatorStateMachine stateMachine, List<string> attackStates)
        {
            var moveStates = new List<string> { IdleState, WalkForwardState, WalkBackState, SitState, JumpState };
            var hitStates = new List<string> { TopHitState, MiddleHitState, LowHitState, Top2LowHitState, Low2TopHitState };
            var guardStates = new List<string> { TopGuardState, MiddleGuardState, BottomGuardState };
            const float columnWidth = 250f;
            const float rowHeight = 60f;
            const int attackRows = 8;

            stateMachine.anyStatePosition = new Vector3(0f, 0f, 0f);
            stateMachine.entryPosition = new Vector3(0f, 120f, 0f);
            stateMachine.exitPosition = new Vector3(0f, 240f, 0f);

            ChildAnimatorState[] children = stateMachine.states;
            int otherCount = 0;
            for (int i = 0; i < children.Length; i++)
            {
                string stateName = children[i].state.name;
                Vector3 position;
                if (moveStates.Contains(stateName)) position = new Vector3(columnWidth, moveStates.IndexOf(stateName) * rowHeight, 0f);
                else if (hitStates.Contains(stateName)) position = new Vector3(columnWidth * 2f, hitStates.IndexOf(stateName) * rowHeight, 0f);
                else if (guardStates.Contains(stateName)) position = new Vector3(columnWidth * 3f, guardStates.IndexOf(stateName) * rowHeight, 0f);
                else if (attackStates.Contains(stateName))
                {
                    int index = attackStates.IndexOf(stateName);
                    position = new Vector3(columnWidth * (4f + index / attackRows), index % attackRows * rowHeight, 0f);
                }
                else
                {
                    position = new Vector3(columnWidth, (moveStates.Count + 1 + otherCount) * rowHeight, 0f);
                    otherCount++;
                }
                children[i].position = position;
            }
            stateMachine.states = children;
        }
    }
}
