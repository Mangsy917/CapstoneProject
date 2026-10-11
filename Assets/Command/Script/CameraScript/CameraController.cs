using UnityEngine;

/// <summary>
/// 카메라 매니저.
/// 카메라 오브젝트에 직접 붙이지 않고, 별도의 매니저 오브젝트에 붙여서
/// 인스펙터에 지정한 카메라(Transform)를 움직입니다.
///
/// [동작]
/// - Player + Enemy : x, y는 Boundary 오브젝트(CharStageBoundary가 옮기는 오브젝트)의 위치를 따라가고,
///                    z는 Player와 Enemy 사이 거리에 비례해서 멀어질수록 멀리서(nearZ → farZ) 찍음
/// - Player만 있을 때 : Boundary는 멈춰 있으므로 Player를 따라가고, z는 playerOnlyZ로 고정
/// - Boundary 칸이 비어 있으면 Player와 Enemy의 중심을 직접 계산해서 따라감
/// - y는 항상 minY ~ maxY 범위로 제한
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

    [Header("Z축 - Player만 있을 때")]
    [Tooltip("Enemy가 없을 때 고정할 z값")]
    [SerializeField] private float playerOnlyZ = -5f;

    [Header("Z축 - Player + Enemy (거리에 비례)")]
    [Tooltip("가장 가까이서 찍을 때의 z값")]
    [SerializeField] private float nearZ = -5f;
    [Tooltip("가장 멀리서 찍을 때의 z값")]
    [SerializeField] private float farZ = -6.5f;
    [Tooltip("이 거리 이하이면 nearZ (가장 가까이)")]
    [SerializeField] private float nearDistance = 2.5f;
    [Tooltip("이 거리 이상이면 farZ (가장 멀리). nearDistance와 차이가 클수록 z가 완만하게 변함")]
    [SerializeField] private float farDistance = 8f;

    [Header("이동")]
    [Tooltip("x, y가 목표 위치까지 따라가는 시간(초). 0이면 즉시 이동")]
    [SerializeField] private float followSmoothTime = 0.15f;
    [Tooltip("z(줌)가 목표 값까지 변하는 시간(초). 클수록 부드럽게 멀어지고 가까워짐")]
    [SerializeField] private float zoomSmoothTime = 0.5f;

    [Header("검색")]
    [Tooltip("Player / Enemy를 다시 찾는 주기(초)")]
    [SerializeField] private float searchInterval = 0.25f;

    private Transform player;
    private Transform enemy;
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

        // z(줌)는 따로 천천히 이동
        float z = zoomSmoothTime <= 0f
            ? desired.z
            : Mathf.SmoothDamp(current.z, desired.z, ref zoomVelocity, zoomSmoothTime);

        // y 범위 제한 (보간 중에도 범위를 벗어나지 않도록)
        xy.y = Mathf.Clamp(xy.y, Mathf.Min(minY, maxY), Mathf.Max(minY, maxY));

        targetCamera.position = new Vector3(xy.x, xy.y, z);
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

            // 두 캐릭터 사이 거리에 비례해서 z 결정 (멀어질수록 멀리서 찍음)
            // nearDistance → 0, farDistance → 1 사이의 비율을 구하고, 그 비율만큼 nearZ에서 farZ로 이동
            float distance = Vector3.Distance(player.position, enemy.position);
            float t = Mathf.InverseLerp(nearDistance, farDistance, distance);
            z = Mathf.Lerp(nearZ, farZ, t);
        }

        // y 범위 제한
        y = Mathf.Clamp(y, Mathf.Min(minY, maxY), Mathf.Max(minY, maxY));

        return new Vector3(x, y, z);
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
        if (farDistance <= nearDistance) farDistance = nearDistance + 0.01f;
        if (searchInterval < 0f) searchInterval = 0f;
        if (followSmoothTime < 0f) followSmoothTime = 0f;
        if (zoomSmoothTime < 0f) zoomSmoothTime = 0f;
    }
}