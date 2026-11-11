using System.Collections.Generic;
using Game.Character;
using Game.UI.Setting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Field
{
    // 공격이 맞았을 때의 타격감 연출 (GameManager에 붙여서 사용)
    // CharacterHitReaction.TargetHit을 받아서 아래 세 가지를 보여줌 (가드는 제외)
    // 1) 히트스톱 흔들림: 히트스톱으로 멈춘 동안 맞은 쪽이 좌우로 떨림 (점점 작아짐)
    // 2) 흰 번쩍: 맞은 쪽 스프라이트가 잠깐 하얗게 칠해짐 (옵션 > 그래픽 설정 > 이펙트를 끄면 안 함)
    // 3) 화면 흔들림: 카메라가 흔들림. 노말 히트 < 카운터 < 퍼니시 카운터 < KO 순서로 세게
    //    (옵션 > 게임 설정 > 화면 흔들림을 끄면 안 함)
    // - 흔들림은 화면을 그리는 동안에만 위치를 옮기고 그린 뒤 바로 되돌림
    //   (캐릭터 위치, 판정, 카메라 이동 계산에는 영향을 주지 않음)
    // - 흰 번쩍과 히트스톱 흔들림은 게임 프레임으로 셈 (슬로우 모션 / KO 멈춤 동안에는 줄지 않음)
    // - 화면 흔들림은 실제 시간으로 셈 (KO로 게임이 멈춰 있어도 흔들림)
    public class HitFeedback : MonoBehaviour
    {
        [Header("히트스톱 흔들림 (맞은 쪽)")]
        [Tooltip("히트스톱으로 멈춘 동안 맞은 쪽이 좌우로 떨리는 거리 (월드 단위). 0이면 떨리지 않음")]
        [SerializeField] private float _hitStopShakeDistance = 0.05f;
        [Tooltip("1초에 좌우로 몇 번 떨리는지")]
        [SerializeField] private float _hitStopShakeRate = 30f;

        [Header("흰 번쩍 (이펙트)")]
        [Tooltip("하얗게 칠할 때 쓸 머티리얼 (H_SpriteFlash 셰이더)")]
        [SerializeField] private Material _flashMaterial;
        [Tooltip("하얗게 보이는 프레임 수 (60프레임 = 1초)")]
        [SerializeField] private int _flashFrames = 3;
        [Tooltip("KO 공격일 때 하얗게 보이는 프레임 수 (KO 멈춤 동안은 줄지 않음)")]
        [SerializeField] private int _knockOutFlashFrames = 6;

        [Header("화면 흔들림")]
        [Tooltip("노말 히트일 때 카메라가 흔들리는 거리 (월드 단위)")]
        [SerializeField] private float _normalShakeDistance = 0.03f;
        [Tooltip("카운터일 때 카메라가 흔들리는 거리")]
        [SerializeField] private float _counterShakeDistance = 0.06f;
        [Tooltip("퍼니시 카운터일 때 카메라가 흔들리는 거리")]
        [SerializeField] private float _punishCounterShakeDistance = 0.1f;
        [Tooltip("흔들리는 시간(초, 실제 시간). 점점 약해짐")]
        [SerializeField] private float _screenShakeTime = 0.15f;
        [Tooltip("KO 공격일 때 카메라가 흔들리는 거리")]
        [SerializeField] private float _knockOutShakeDistance = 0.2f;
        [Tooltip("KO 공격일 때 흔들리는 시간(초, 실제 시간)")]
        [SerializeField] private float _knockOutShakeTime = 0.5f;

        // 하얗게 칠한 스프라이트와 원래 머티리얼, 남은 프레임
        private class FlashTarget
        {
            public SpriteRenderer FlashRenderer;
            public Material OriginalMaterial;
            public int FramesLeft;
            public int HitFrame;
        }

        // 히트스톱 동안 떨리는 캐릭터
        private class ShakingTarget
        {
            public Transform ShakenTransform;
            public int FramesLeft;
            public int ShakeFrames;
            public int HitFrame;
            public Vector3 AppliedOffset; // 화면을 그리는 동안 옮긴 거리 (그린 뒤 되돌림)
        }

        private readonly List<FlashTarget> _flashTargets = new List<FlashTarget>();
        private readonly List<ShakingTarget> _shakingTargets = new List<ShakingTarget>();

        private float _screenShakeDistance;
        private float _screenShakeTotalTime;
        private float _screenShakeTimeLeft;
        private Transform _shakenCamera;
        private Vector3 _appliedCameraOffset;

        private void OnEnable()
        {
            CharacterHitReaction.TargetHit += PlayHitFeedback;
            RenderPipelineManager.beginCameraRendering += ApplyShakeOffsets;
            RenderPipelineManager.endCameraRendering += RestoreShakeOffsets;
        }

        private void OnDisable()
        {
            CharacterHitReaction.TargetHit -= PlayHitFeedback;
            RenderPipelineManager.beginCameraRendering -= ApplyShakeOffsets;
            RenderPipelineManager.endCameraRendering -= RestoreShakeOffsets;

            for (int i = _flashTargets.Count - 1; i >= 0; i--) EndFlash(i);
            _shakingTargets.Clear();
            _screenShakeTimeLeft = 0f;
        }


        private void PlayHitFeedback(GameObject target, HitType hitType, bool isKnockOut)
        {
            if (target == null) return;

            StartHitStopShake(target);
            if (EffectSettings.IsEffectOn) StartFlash(target, isKnockOut ? _knockOutFlashFrames : _flashFrames);

            if (isKnockOut) StartScreenShake(_knockOutShakeDistance, _knockOutShakeTime);
            else StartScreenShake(GetShakeDistance(hitType), _screenShakeTime);
        }

        private float GetShakeDistance(HitType hitType)
        {
            switch (hitType)
            {
                case HitType.Counter: return _counterShakeDistance;
                case HitType.PunishCounter: return _punishCounterShakeDistance;
                default: return _normalShakeDistance;
            }
        }

        private void Update()
        {
            // 슬로우 모션 / KO 멈춤 동안 건너뛰는 프레임은 세지 않음
            bool isTickFrame = GameSpeed.IsTickFrame;

            for (int i = _flashTargets.Count - 1; i >= 0; i--)
            {
                FlashTarget flash = _flashTargets[i];
                if (flash.FlashRenderer == null) { _flashTargets.RemoveAt(i); continue; }
                if (!isTickFrame || flash.HitFrame == Time.frameCount) continue;

                flash.FramesLeft--;
                if (flash.FramesLeft <= 0) EndFlash(i);
            }

            for (int i = _shakingTargets.Count - 1; i >= 0; i--)
            {
                ShakingTarget shaking = _shakingTargets[i];
                if (shaking.ShakenTransform == null) { _shakingTargets.RemoveAt(i); continue; }
                if (!isTickFrame || shaking.HitFrame == Time.frameCount) continue;

                shaking.FramesLeft--;
                if (shaking.FramesLeft <= 0) _shakingTargets.RemoveAt(i);
            }

            if (_screenShakeTimeLeft > 0f) _screenShakeTimeLeft -= Time.unscaledDeltaTime;
        }

        // ===== 흰 번쩍 =====

        private void StartFlash(GameObject target, int frames)
        {
            if (_flashMaterial == null || frames <= 0) return;
            if (!target.TryGetComponent(out SpriteRenderer spriteRenderer)) return;

            // 이미 번쩍이는 중이면 남은 프레임만 다시 채움
            foreach (FlashTarget flash in _flashTargets)
            {
                if (flash.FlashRenderer != spriteRenderer) continue;
                flash.FramesLeft = Mathf.Max(flash.FramesLeft, frames);
                flash.HitFrame = Time.frameCount;
                return;
            }

            _flashTargets.Add(new FlashTarget
            {
                FlashRenderer = spriteRenderer,
                OriginalMaterial = spriteRenderer.sharedMaterial,
                FramesLeft = frames,
                HitFrame = Time.frameCount,
            });
            spriteRenderer.sharedMaterial = _flashMaterial;
        }

        private void EndFlash(int index)
        {
            FlashTarget flash = _flashTargets[index];
            if (flash.FlashRenderer != null) flash.FlashRenderer.sharedMaterial = flash.OriginalMaterial;
            _flashTargets.RemoveAt(index);
        }

        // ===== 히트스톱 흔들림 =====

        // 맞은 프레임부터 히트스톱이 풀릴 때까지 떨림
        private void StartHitStopShake(GameObject target)
        {
            if (_hitStopShakeDistance <= 0f) return;
            if (!HitStop.IsStoppedNextFrame(target.transform)) return; // 히트스톱이 없는 공격(장풍)은 떨리지 않음

            int frames = GetHitStopFramesLeft(target);
            foreach (ShakingTarget shaking in _shakingTargets)
            {
                if (shaking.ShakenTransform != target.transform) continue;
                shaking.FramesLeft = frames;
                shaking.ShakeFrames = frames;
                shaking.HitFrame = Time.frameCount;
                return;
            }

            _shakingTargets.Add(new ShakingTarget
            {
                ShakenTransform = target.transform,
                FramesLeft = frames,
                ShakeFrames = frames,
                HitFrame = Time.frameCount,
            });
        }

        private static int GetHitStopFramesLeft(GameObject target)
        {
            return target.TryGetComponent(out HitStop hitStop) ? hitStop.FramesLeft : 0;
        }

        // ===== 화면 흔들림 =====

        private void StartScreenShake(float distance, float time)
        {
            if (!GameSettings.IsScreenShakeOn || distance <= 0f || time <= 0f) return;

            // 지금 흔들리는 것보다 약한 흔들림은 무시
            float currentDistance = _screenShakeTimeLeft > 0f ? _screenShakeDistance * (_screenShakeTimeLeft / _screenShakeTotalTime) : 0f;
            if (distance < currentDistance) return;

            _screenShakeDistance = distance;
            _screenShakeTotalTime = time;
            _screenShakeTimeLeft = time;
        }

        // ===== 그리는 동안만 위치 옮기기 =====

        private void ApplyShakeOffsets(ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType != CameraType.Game) return;

            // 맞은 쪽 떨림: 좌우로 번갈아 움직이고 끝날수록 작아짐
            int side = Mathf.FloorToInt(Time.unscaledTime * _hitStopShakeRate) % 2 == 0 ? 1 : -1;
            foreach (ShakingTarget shaking in _shakingTargets)
            {
                if (shaking.ShakenTransform == null) continue;
                float strength = (float)shaking.FramesLeft / Mathf.Max(1, shaking.ShakeFrames);
                shaking.AppliedOffset = new Vector3(side * _hitStopShakeDistance * strength, 0f, 0f);
                shaking.ShakenTransform.position += shaking.AppliedOffset;
            }

            // 화면 흔들림: 아무 방향으로 조금씩, 끝날수록 작아짐
            _shakenCamera = null;
            if (_screenShakeTimeLeft > 0f && GameSettings.IsScreenShakeOn)
            {
                float strength = _screenShakeTimeLeft / _screenShakeTotalTime;
                Vector2 random = Random.insideUnitCircle * (_screenShakeDistance * strength);
                _shakenCamera = camera.transform;
                _appliedCameraOffset = _shakenCamera.right * random.x + _shakenCamera.up * random.y;
                _shakenCamera.position += _appliedCameraOffset;
            }
        }

        private void RestoreShakeOffsets(ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType != CameraType.Game) return;

            foreach (ShakingTarget shaking in _shakingTargets)
            {
                if (shaking.ShakenTransform != null) shaking.ShakenTransform.position -= shaking.AppliedOffset;
                shaking.AppliedOffset = Vector3.zero;
            }

            if (_shakenCamera != null) _shakenCamera.position -= _appliedCameraOffset;
            _shakenCamera = null;
            _appliedCameraOffset = Vector3.zero;
        }
    }
}
