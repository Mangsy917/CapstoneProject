using UnityEngine;

/// <summary>
/// 스테이지 바운더리 매니저.
/// Player 태그와 Enemy 태그 오브젝트의 x, y축 위치를 매 프레임 계산해서,
/// 두 오브젝트의 x, y축 중심에 Boundary 오브젝트를 위치시킵니다.
/// (Boundary 오브젝트에 통과할 수 없는 벽 Collider를 자식으로 달아서 캐릭터 간 거리를 제한하는 용도)
///
/// [동작]
/// - Boundary의 x = (Player.x + Enemy.x) / 2
/// - Boundary의 y = (Player.y + Enemy.y) / 2
/// - Boundary의 z는 씬에 배치한 값 그대로 유지
/// - Enemy가 없으면(예: Enemy 재생성 대기 중) Player의 x, y 위치를 따라감
/// - Player도 없으면 Boundary는 마지막 위치에 그대로 멈춤
/// - EnemyCounter가 Enemy를 새로 만들면 자동으로 다시 찾음
/// - 스테이지 끝 벽(Stage Left Wall / Stage Right Wall)을 Stage Left End / Stage Right End에 세워서
///   캐릭터가 스테이지 밖으로 나가지 못하게 함 (벽은 Boundary와 달리 움직이지 않음)
///
/// [사용법]
/// 1. 빈 GameObject(예: "StageBoundaryManager")를 만들고 이 스크립트를 붙입니다.
/// 2. Boundary 칸에 벽 Collider들을 가진 Boundary 오브젝트를 넣습니다.
///    비워두면 이 스크립트가 붙은 오브젝트 자신을 Boundary로 사용합니다.
/// </summary>
public class CharStageBoundary : MonoBehaviour
{
    [Header("Boundary 오브젝트")]
    [Tooltip("Player와 Enemy의 중심으로 옮길 오브젝트. 비워두면 이 오브젝트 자신을 사용")]
    [SerializeField] private Transform boundary;

    [Header("태그")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string enemyTag = "Enemy";

    [Header("검색")]
    [Tooltip("Player / Enemy를 다시 찾는 주기(초)")]
    [SerializeField] private float searchInterval = 0.25f;

    [Header("스테이지 끝")]
    [Tooltip("스테이지 왼쪽 끝의 x 위치 (시작 위치 설정의 '왼쪽 끝'에서 캐릭터가 이 위치에 붙어 섬)")]
    [SerializeField] private float stageLeftEnd = -100f;
    [Tooltip("스테이지 오른쪽 끝의 x 위치 (시작 위치 설정의 '오른쪽 끝'에서 캐릭터가 이 위치에 붙어 섬)")]
    [SerializeField] private float stageRightEnd = 100f;

    [Header("스테이지 끝 벽")]
    [Tooltip("스테이지 왼쪽 끝에 세울 벽 Collider. 안쪽 면이 Stage Left End에 오도록 자동으로 옮겨짐")]
    [SerializeField] private BoxCollider stageLeftWall;
    [Tooltip("스테이지 오른쪽 끝에 세울 벽 Collider. 안쪽 면이 Stage Right End에 오도록 자동으로 옮겨짐")]
    [SerializeField] private BoxCollider stageRightWall;
    [Tooltip("벽 두께 (x)")]
    [SerializeField] private float stageWallThickness = 1f;
    [Tooltip("벽 높이 (y). 점프로 넘어가지 못하도록 충분히 높게")]
    [SerializeField] private float stageWallHeight = 50f;
    [Tooltip("벽 깊이 (z)")]
    [SerializeField] private float stageWallDepth = 10f;

    /// <summary>스테이지 왼쪽 끝의 x 위치</summary>
    public float StageLeftEnd => stageLeftEnd;

    /// <summary>스테이지 오른쪽 끝의 x 위치</summary>
    public float StageRightEnd => stageRightEnd;

    /// <summary>스테이지 가운데의 x 위치</summary>
    public float StageCenter => (stageLeftEnd + stageRightEnd) * 0.5f;

    private Transform player;
    private Transform enemy;
    private float searchTimer;

    /// <summary>현재 Player와 Enemy의 x축 중심 (Enemy가 없으면 Player의 x, Player도 없으면 Boundary의 마지막 x)</summary>
    public float CenterX { get; private set; }

    /// <summary>현재 Player와 Enemy의 y축 중심 (Enemy가 없으면 Player의 y, Player도 없으면 Boundary의 마지막 y)</summary>
    public float CenterY { get; private set; }

    private void Awake()
    {
        if (boundary == null)
            boundary = transform;

        CenterX = boundary.position.x;
        CenterY = boundary.position.y;

        PlaceStageWalls();
    }

    // 스테이지 끝 벽을 Stage Left End / Stage Right End 위치로 옮기고 크기를 맞춤
    // (벽의 안쪽 면이 끝 위치에 오고, 바닥(y = 0)부터 위로 세움)
    private void PlaceStageWalls()
    {
        PlaceStageWall(stageLeftWall, stageLeftEnd - stageWallThickness * 0.5f);
        PlaceStageWall(stageRightWall, stageRightEnd + stageWallThickness * 0.5f);
    }

    private void PlaceStageWall(BoxCollider wall, float centerX)
    {
        if (wall == null) return;

        wall.transform.SetPositionAndRotation(new Vector3(centerX, 0f, wall.transform.position.z), Quaternion.identity);
        wall.transform.localScale = Vector3.one;
        wall.size = new Vector3(stageWallThickness, stageWallHeight, stageWallDepth);
        wall.center = new Vector3(0f, stageWallHeight * 0.5f, 0f);
    }

    private void Start()
    {
        FindTargets();
        UpdateBoundary();
    }

    private void Update()
    {
        // 주기적으로, 또는 대상이 사라졌을 때 다시 찾기
        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0f || !IsValid(player) || !IsValid(enemy))
        {
            FindTargets();
            searchTimer = searchInterval;
        }
    }

    // 모든 캐릭터 이동(Update)이 끝난 뒤 위치를 갱신
    private void LateUpdate()
    {
        UpdateBoundary();
    }

    /// <summary>
    /// 캐릭터를 순간이동시킨 뒤 호출. Boundary 벽을 바로 두 캐릭터 중심으로 옮기고,
    /// Pushbox가 벽을 넘어간 것으로 보고 캐릭터를 되돌리지 않도록 지금 위치를 기억시키고,
    /// 카메라도 천천히 따라오지 않고 바로 새 위치로 옮김
    /// </summary>
    public static void SyncAfterTeleport()
    {
        foreach (var stageBoundary in FindObjectsByType<CharStageBoundary>(FindObjectsSortMode.None))
        {
            stageBoundary.UpdateBoundary();
        }
        Game.Character.Pushbox.SyncAfterTeleport();

        foreach (var cameraManager in FindObjectsByType<CameraManager>(FindObjectsSortMode.None))
        {
            cameraManager.SnapToTarget();
        }
    }

    private void UpdateBoundary()
    {
        // Player가 없으면 마지막 위치에 그대로 둠
        if (!IsValid(player)) return;

        if (IsValid(enemy))
        {
            // Player + Enemy: 두 캐릭터의 중심
            CenterX = (player.position.x + enemy.position.x) * 0.5f;
            CenterY = (player.position.y + enemy.position.y) * 0.5f;
        }
        else
        {
            // Enemy가 없으면 Player를 따라감
            CenterX = player.position.x;
            CenterY = player.position.y;
        }

        Vector3 pos = boundary.position;
        pos.x = CenterX;
        pos.y = CenterY;
        boundary.position = pos;
    }

    private static bool IsValid(Transform t)
    {
        return t != null && t.gameObject.activeInHierarchy;
    }

    private void FindTargets()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        player = playerObj != null ? playerObj.transform : null;

        // Enemy가 여러 개면 Player와 가장 가까운 것을 사용
        enemy = null;
        if (player == null) return;

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
        if (searchInterval < 0f) searchInterval = 0f;
        if (stageRightEnd < stageLeftEnd) stageRightEnd = stageLeftEnd;
        if (stageWallThickness < 0.01f) stageWallThickness = 0.01f;
        if (stageWallHeight < 0.01f) stageWallHeight = 0.01f;
        if (stageWallDepth < 0.01f) stageWallDepth = 0.01f;

        // 인스펙터에서 값을 바꾸면 에디터에서도 벽이 바로 따라옴
        PlaceStageWalls();
    }

    // 씬 뷰에서 스테이지 끝 위치를 노란 세로선으로 보여줌
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        float z = transform.position.z;
        Gizmos.DrawLine(new Vector3(stageLeftEnd, -1f, z), new Vector3(stageLeftEnd, 10f, z));
        Gizmos.DrawLine(new Vector3(stageRightEnd, -1f, z), new Vector3(stageRightEnd, 10f, z));
    }
}