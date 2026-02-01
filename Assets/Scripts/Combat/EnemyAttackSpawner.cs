using UnityEngine;
using System.Collections;

public class EnemyAttackSpawner : MonoBehaviour
{
    [SerializeField] private RectTransform projectilesRoot;
    [SerializeField] private RectTransform arenaRect;

    private GameObject projectilePrefab;

    public void Configure(GameObject prefab) => projectilePrefab = prefab;

    public IEnumerator Run(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            SpawnDown();
            yield return new WaitForSeconds(0.25f);
            t += 0.25f;
        }
    }

    private void SpawnDown()
    {
        if (!projectilePrefab || !projectilesRoot || !arenaRect) return;

        var go = Instantiate(projectilePrefab, projectilesRoot);
        var rt = go.GetComponent<RectTransform>();
        var proj = go.GetComponent<ProjectileUI>();

        if (!rt || !proj)
        {
            Destroy(go);
            return;
        }

        float x = Random.Range(-arenaRect.rect.width / 2f + 20f, arenaRect.rect.width / 2f - 20f);
        float y = arenaRect.rect.height / 2f - 10f;

        rt.anchoredPosition = new Vector2(x, y);
        proj.Init(Vector2.down);
    }
}
