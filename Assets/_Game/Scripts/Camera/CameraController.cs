using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 카메라 매니저.
/// 카메라 오브젝트에 직접 붙이지 않고, 별도의 매니저 오브젝트에 붙여서
/// 인스펙터에 지정한 카메라(Transform)를 움직입니다.
///
/// [동작]
/// - Player + Enemy : x, y는 Boundary 오브젝트(CharStageBoundary가 옮기는 오브젝트)의 위치를 따라가고,
///                    z는 두 캐릭터가 화면 양 끝에서 screenEdgeMargin만큼 떨어져 보이도록 카메라 화각으로 계산함
///                    (캐릭터가 멀어지면 뒤로 빠지고, 가까워지면 앞으로 옴)
///                    계산한 z는 closestZ(가장 앞, 최댓값) ~ farthestZ(가장 뒤, 최솟값) 안으로 제한
///                    뒤로 빠질 때는 캐릭터가 화면 밖으로 나가지 않도록 빠르게, 앞으로 올 때는 천천히 움직임
/// - Player만 있을 때 : Boundary는 멈춰 있으므로 Player를 따라가고, z는 playerOnlyZ로 고정
/// - Boundary 칸이 비어 있으면 Player와 Enemy의 중심을 직접 계산해서 따라감
/// - y는 minY ~ maxY 범위로 제한한 뒤, yOffset(0.2)만큼 위로 올려서 촬영
///
/// CharStageBoundary가 Boundary 위치를 갱신한 뒤에 카메라가 움직이도록 실행 순서를 늦춰 두었습니다.
/// </summary>
[DefaultExecutionOrder(100)]
public class CameraManager : MonoBehaviour
{
    [Header("대상 카메라")]
    [Tooltip("움직일 카메라. Cinemachine 카메라 또는 Main Camera")]
    [SerializeField] private Transform targetCamera;

    [Header("촬영 기준")]
    [Tooltip("카메라가 따라갈 Boundary 오브젝트 (CharStageBoundary의 Boundary 칸에 넣은 오브젝트)")]
    [SerializeField] private Transform boundary;

    [Header("사용 태그")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string enemyTag = "Enemy";

    [Header("Y축 범위")]
    [Tooltip("카메라 y의 최솟값")]
    [SerializeField] private float minY = 2f;
    [Tooltip("카메라 y의 최댓값")]
    [SerializeField] private float maxY = 2.5f;
    [Tooltip("촬영 위치를 위로 올리는 값. 범위 제한(minY ~ maxY)을 적용한 뒤 더해짐")]
    [SerializeField] private float yOffset = 0.2f;

    [Header("Z축 - Player만 있을 때")]
    [Tooltip("Enemy가 없을 때 고정할 z값")]
    [SerializeField] private float playerOnlyZ = -5f;

    [Header("Z축 - Player + Enemy (두 캐릭터가 화면에 들어오도록)")]
    [Tooltip("캐릭터가 가까워졌을 때 카메라가 앞으로 올 수 있는 최댓값 (가장 앞)")]
    [FormerlySerializedAs("nearZ")]
    [SerializeField] private float closestZ = -5f;
    [Tooltip("캐릭터가 멀어졌을 때 카메라가 뒤로 빠질 수 있는 최솟값 (가장 뒤)")]
    [FormerlySerializedAs("farZ")]
    [SerializeField] private float farthestZ = -6.5f;
    [Tooltip("캐릭터 중심에서 화면 왼쪽 / 오른쪽 끝까지 남길 거리(월드 단위).\n" +
             "클수록 같은 거리에서도 카메라가 더 뒤로 빠짐. 캐릭터 절반 폭(약 0.7)보다 커야 몸이 잘리지 않음")]
    [SerializeField] private float screenEdgeMargin = 1.2f;
    [Tooltip("캐릭터가 서 있는 줄의 z값 (화면 폭을 이 깊이에서 계산함)")]
    [SerializeField] private float characterLineZ = 0f;

    [Header("이동")]
    [Tooltip("x, y가 목표 위치까지 따라가는 시간(초). 0이면 즉시 이동")]
    [SerializeField] private float followSmoothTime = 0.15f;
    [Tooltip("캐릭터가 가까워져서 카메라가 앞으로 올 때 걸리는 시간(초). 클수록 천천히 다가옴")]
    [FormerlySerializedAs("zoomSmoothTime")]
    [SerializeField] private float zoomInSmoothTime = 0.5f;
    [Tooltip("캐릭터가 멀어져서 카메라가 뒤로 빠질 때 걸리는 시간(초). 작을수록 캐릭터가 화면 밖으로 덜 나감")]
    [SerializeField] private float zoomOutSmoothTime = 0.15f;

    [Header("검색")]
    [Tooltip("Player / Enemy를 다시 찾는 주기(초)")]
    [SerializeField] private float searchInterval = 0.25f;

    private Transform player;
    private Transform enemy;
    private Camera targetCameraComponent;
    private Vector2 followVelocity;
    private float zoomVelocity;
    private float searchTimer;

    private void Start()
    {
        if (targetCamera == null && Camera.main != null)
        {
            targetCamera = Camera.main.transform;
            Debug.LogWarning("[CameraManager] Target Camera가 비어 있어 Main Camera를 사용합니다.", this);
        }

        if (targetCamera != null)
            targetCameraComponent = targetCamera.GetComponentInChildren<Camera>();

        if (boundary == null)
            Debug.LogWarning("[CameraManager] Boundary가 비어 있어 Player와 Enemy의 중심을 직접 계산합니다.", this);

        FindTargets();

        // 시작 시 바로 목표 위치로 이동 (첫 프레임 튐 방지)
        if (targetCamera != null && player != null)
            targetCamera.position = GetDesiredPosition();
    }

    private void LateUpdate()
    {
        if (targetCamera == null) return;

        // 일정 주기마다, 또는 대상이 사라졌을 때 다시 검색
        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0f || !IsValid(player) || (enemy != null && !IsValid(enemy)))
        {
            FindTargets();
            searchTimer = searchInterval;
        }

        if (player == null) return;

        Vector3 current = targetCamera.position;
        Vector3 desired = GetDesiredPosition();

        // x, y 이동
        Vector2 xy = followSmoothTime <= 0f
            ? new Vector2(desired.x, desired.y)
            : Vector2.SmoothDamp(new Vector2(current.x, current.y), new Vector2(desired.x, desired.y),
                                 ref followVelocity, followSmoothTime);

        // z(줌)는 따로 이동: 뒤로 빠질 때(z가 작아질 때)는 빠르게, 앞으로 올 때는 천천히
        float zoomSmoothTime = desired.z < current.z ? zoomOutSmoothTime : zoomInSmoothTime;
        float z = zoomSmoothTime <= 0f
            ? desired.z
            : Mathf.SmoothDamp(current.z, desired.z, ref zoomVelocity, zoomSmoothTime);

        // y 범위 제한 (보간 중에도 범위를 벗어나지 않도록, 오프셋만큼 올린 범위 기준)
        xy.y = Mathf.Clamp(xy.y, Mathf.Min(minY, maxY) + yOffset, Mathf.Max(minY, maxY) + yOffset);

        targetCamera.position = new Vector3(xy.x, xy.y, z);
    }

    /// <summary>
    /// 천천히 따라가지 않고 목표 위치로 바로 옮김 (재시작 등으로 캐릭터를 순간이동시킨 뒤 호출)
    /// </summary>
    public void SnapToTarget()
    {
        if (targetCamera == null) return;
        if (!IsValid(player)) FindTargets();
        if (player == null) return;

        targetCamera.position = GetDesiredPosition();
        followVelocity = Vector2.zero;
        zoomVelocity = 0f;
    }

    private Vector3 GetDesiredPosition()
    {
        float x, y, z;

        if (enemy == null)
        {
            // Player만 있을 때: Boundary가 멈춰 있으므로 Player를 따라감, z 고정
            Vector3 p = player.position;
            x = p.x;
            y = p.y;
            z = playerOnlyZ;
        }
        else
        {
            // Player + Enemy: Boundary 위치를 촬영 (없으면 두 캐릭터의 중심)
            Vector3 center = boundary != null
                ? boundary.position
                : (player.position + enemy.position) * 0.5f;
            x = center.x;
            y = center.y;

            // 두 캐릭터가 화면 양 끝에서 screenEdgeMargin만큼 떨어져 보이는 z
            float distanceX = Mathf.Abs(player.position.x - enemy.position.x);
            z = GetFramingZ(halfWidth: distanceX * 0.5f + screenEdgeMargin);
        }

        // y 범위 제한 후, 오프셋만큼 위로 올림
        y = Mathf.Clamp(y, Mathf.Min(minY, maxY), Mathf.Max(minY, maxY)) + yOffset;

        return new Vector3(x, y, z);
    }

    /// <summary>
    /// 캐릭터가 서 있는 줄에서 화면 중심~화면 끝 거리가 halfWidth가 되는 카메라 z를 구하고,
    /// closestZ(가장 앞) ~ farthestZ(가장 뒤) 안으로 제한함
    /// (원근 카메라는 멀어질수록 보이는 폭이 거리에 비례해서 넓어짐: 보이는 절반 폭 = 거리 × tan(가로 화각 / 2))
    /// </summary>
    private float GetFramingZ(float halfWidth)
    {
        float frontZ = Mathf.Max(closestZ, farthestZ);
        float backZ = Mathf.Min(closestZ, farthestZ);

        // Start보다 먼저 불렸을 때(SnapToTarget 등)를 위해 여기서도 찾음
        if (targetCameraComponent == null && targetCamera != null)
            targetCameraComponent = targetCamera.GetComponentInChildren<Camera>();

        // 카메라 컴포넌트가 없거나 직교 카메라면 화각으로 계산할 수 없으므로 가장 앞 값을 씀
        if (targetCameraComponent == null || targetCameraComponent.orthographic) return frontZ;

        float halfVerticalFov = targetCameraComponent.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float tanHalfHorizontalFov = Mathf.Tan(halfVerticalFov) * targetCameraComponent.aspect;
        if (tanHalfHorizontalFov <= 0f) return frontZ;

        float distanceToLine = halfWidth / tanHalfHorizontalFov;
        return Mathf.Clamp(characterLineZ - distanceToLine, backZ, frontZ);
    }

    private static bool IsValid(Transform t)
    {
        return t != null && t.gameObject.activeInHierarchy;
    }

    private void FindTargets()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        player = playerObj != null ? playerObj.transform : null;

        enemy = null;
        if (player == null) return;

        // Enemy가 여러 개면 Player와 가장 가까운 것을 사용
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        float closestSqr = float.MaxValue;
        foreach (GameObject e in enemies)
        {
            float sqr = (e.transform.position - player.position).sqrMagnitude;
            if (sqr < closestSqr)
            {
                closestSqr = sqr;
                enemy = e.transform;
            }
        }
    }

    private void OnValidate()
    {
        // 인스펙터에서 잘못된 값이 들어가지 않도록 보정
        if (farthestZ > closestZ) farthestZ = closestZ;
        if (screenEdgeMargin < 0f) screenEdgeMargin = 0f;
        if (searchInterval < 0f) searchInterval = 0f;
        if (followSmoothTime < 0f) followSmoothTime = 0f;
        if (zoomInSmoothTime < 0f) zoomInSmoothTime = 0f;
        if (zoomOutSmoothTime < 0f) zoomOutSmoothTime = 0f;
    }
}