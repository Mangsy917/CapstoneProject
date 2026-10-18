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
    }
}