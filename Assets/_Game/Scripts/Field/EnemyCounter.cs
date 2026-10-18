using System.Collections;
using UnityEngine;

public class EnemyCounter : MonoBehaviour
{
    [Header("프리팹")]
    [Tooltip("다시 생성할 Enemy 프리팹")]
    [SerializeField] private GameObject enemyPrefab;

    [Header("태그")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string enemyTag = "Enemy";

    [Header("개수 제한")]
    [Tooltip("필드에 존재할 수 있는 Enemy 최대 개수")]
    [SerializeField] private int maxEnemyCount = 1;

    [Header("재생성")]
    [Tooltip("Enemy가 사라진 뒤 다시 생성되기까지의 시간(초)")]
    [SerializeField] private float respawnDelay = 0.5f;
    [Tooltip("Player 위치 기준 생성 위치 오프셋 (x +7)")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(7f, 0f, 0f);
    [Tooltip("게임 시작 시 Enemy가 없으면 바로 생성할지 여부")]
    [SerializeField] private bool spawnOnStart = false;

    private bool isRespawning;
    private bool hadEnemy;

    /// <summary>현재 필드의 Enemy 수</summary>
    public int CurrentEnemyCount { get; private set; }

    private void Start()
    {
        CurrentEnemyCount = CountAndLimitEnemies();
        hadEnemy = CurrentEnemyCount > 0;

        if (spawnOnStart && CurrentEnemyCount == 0)
            StartCoroutine(RespawnRoutine());
    }

    private void Update()
    {
        CurrentEnemyCount = CountAndLimitEnemies();

        if (CurrentEnemyCount > 0)
        {
            hadEnemy = true;
            return;
        }

        // Enemy가 있다가 사라졌을 때만 재생성 시작
        if (hadEnemy && !isRespawning)
        {
            hadEnemy = false;
            StartCoroutine(RespawnRoutine());
        }
    }

    /// <summary>
    /// Enemy 수를 세고, 최대 개수를 넘는 Enemy는 삭제합니다.
    /// </summary>
    private int CountAndLimitEnemies()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        int limit = Mathf.Max(0, maxEnemyCount);

        // 초과분 삭제 (먼저 찾은 것은 남기고 나머지 삭제)
        for (int i = limit; i < enemies.Length; i++)
        {
            enemies[i].tag = "Untagged"; // 같은 프레임에 다시 세어지지 않도록
            Destroy(enemies[i]);
        }

        return Mathf.Min(enemies.Length, limit);
    }

    private IEnumerator RespawnRoutine()
    {
        isRespawning = true;

        yield return new WaitForSeconds(respawnDelay);

        // 대기하는 동안 다른 곳에서 Enemy가 생겼으면 생성하지 않음
        if (GameObject.FindGameObjectsWithTag(enemyTag).Length < maxEnemyCount)
            SpawnEnemy();

        isRespawning = false;
    }

    private void SpawnEnemy()
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning("[EnemyCounter] Enemy Prefab이 비어 있습니다.", this);
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
        {
            Debug.LogWarning("[EnemyCounter] Player 태그 오브젝트를 찾을 수 없어 Enemy를 생성하지 못했습니다.", this);
            return;
        }

        Vector3 spawnPos = player.transform.position + spawnOffset;
        GameObject newEnemy = Instantiate(enemyPrefab, spawnPos, enemyPrefab.transform.rotation);

        // 프리팹에 태그가 빠져 있어도 Enemy로 인식되도록
        newEnemy.tag = enemyTag;

        hadEnemy = true;
    }

    private void OnValidate()
    {
        if (maxEnemyCount < 0) maxEnemyCount = 0;
        if (respawnDelay < 0f) respawnDelay = 0f;
    }
}