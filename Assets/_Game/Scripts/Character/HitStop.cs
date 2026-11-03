using System.Collections.Generic;
using UnityEngine;

namespace Game.Character
{
    // 공격이 맞았을 때 때린 쪽과 맞은 쪽의 움직임을 잠깐 멈춤 (히트스톱, 타격감용)
    // - 게임 시간(Time.timeScale)은 그대로 흐르고, 멈춘 캐릭터만 프레임 진행을 건너뜀
    //   이동(CharacterMove, DummyAction), 공격 프레임(CharacterAttack), 경직 / 히트백(CharacterHitReaction), 애니메이션(CharacterAnimationPlayer)이 IsStopped를 확인
    // - 맞은 쪽의 피격 경직은 멈춘 동안 줄지 않고, 멈춤이 풀린 프레임부터 줄어듦
    // - 맞은 프레임에는 그대로 진행하고, 다음 프레임부터 정한 프레임 수만큼 멈춤
    // - 캐릭터에 미리 붙여두지 않아도 처음 맞을 때 자동으로 붙음
    // - 장풍에 맞았을 때와 가드했을 때는 멈추지 않음
    // 다른 스크립트보다 먼저 이번 프레임에 멈췄는지를 정하도록 실행 순서를 앞으로 당김
    [DefaultExecutionOrder(-50)]
    public class HitStop : MonoBehaviour
    {
        private static readonly List<HitStop> _allHitStops = new List<HitStop>();

        private int _stopFramesLeft; // 앞으로 멈출 프레임 수
        private int _stopStartFrame = -1; // 멈춤을 건 프레임 (그 프레임은 멈추지 않음)

        /// <summary>이번 프레임에 멈춰 있는지</summary>
        public bool IsStopped { get; private set; }

        /// <summary>target이 이번 프레임에 히트스톱으로 멈춰 있는지 (HitStop이 없으면 false)</summary>
        public static bool IsStoppedNow(Component target)
        {
            return target != null && target.TryGetComponent(out HitStop hitStop) && hitStop.IsStopped;
        }

        /// <summary>
        /// target이 다음 프레임에 히트스톱으로 멈출지 (HitStop이 없으면 false).
        /// Animator는 LateUpdate에서 정한 속도로 다음 프레임에 재생되므로, 애니메이션을 멈출지는 이 값으로 정함
        /// </summary>
        public static bool IsStoppedNextFrame(Component target)
        {
            return target != null && target.TryGetComponent(out HitStop hitStop) && hitStop._stopFramesLeft > 0;
        }

        /// <summary>target을 다음 프레임부터 frames 프레임 동안 멈춤 (이미 멈춰 있으면 더 긴 쪽으로)</summary>
        public static void Stop(GameObject target, int frames)
        {
            if (target == null || frames <= 0) return;

            if (!target.TryGetComponent(out HitStop hitStop)) hitStop = target.AddComponent<HitStop>();
            hitStop._stopFramesLeft = Mathf.Max(hitStop._stopFramesLeft, frames);
            hitStop._stopStartFrame = Time.frameCount;
        }

        /// <summary>재시작할 때 호출. 모든 캐릭터의 멈춤을 풂</summary>
        public static void ResetAll()
        {
            foreach (var hitStop in _allHitStops)
            {
                hitStop._stopFramesLeft = 0;
                hitStop.IsStopped = false;
            }
        }

        private void OnEnable()
        {
            _allHitStops.Add(this);
        }

        private void OnDisable()
        {
            _allHitStops.Remove(this);
            _stopFramesLeft = 0;
            IsStopped = false;
        }

        private void Update()
        {
            // 슬로우 모션 중에는 건너뛰는 프레임이 있음 (GameSpeed). 건너뛴 프레임은 멈춘 프레임으로 세지 않음
            if (!Game.Field.GameSpeed.IsTickFrame) return;
            if (_stopStartFrame == Time.frameCount) return;

            IsStopped = _stopFramesLeft > 0;
            if (IsStopped) _stopFramesLeft--;
        }
    }
}
