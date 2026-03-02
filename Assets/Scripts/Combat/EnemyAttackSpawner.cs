using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyAttackSpawner : MonoBehaviour
{
    [SerializeField] private RectTransform projectilesRoot;
    [SerializeField] private RectTransform arenaRect;

    private GameObject projectilePrefab;
    private EnemyAttackPattern currentPattern;

    public void Configure(GameObject prefab, EnemyAttackPattern pattern)
    {
        projectilePrefab = prefab;
        currentPattern = pattern;
    }

    public IEnumerator Run(float duration)
    {
        switch (currentPattern)
        {
            case EnemyAttackPattern.HorizontalWaves:
                yield return HorizontalWavesRoutine(duration);
                break;
            case EnemyAttackPattern.TargetedHoming:
                yield return TargetedHomingRoutine(duration);
                break;
            case EnemyAttackPattern.CircleBurst:
                yield return CircleBurstRoutine(duration);
                break;
            case EnemyAttackPattern.RandomDrop:
            default:
                yield return RandomDropRoutine(duration);
                break;
        }
    }

    // ===================================
    // 1. RANDOM DROP (Classic)
    // ===================================
    private IEnumerator RandomDropRoutine(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect)
            {
                var go = Instantiate(projectilePrefab, projectilesRoot);
                var rt = go.GetComponent<RectTransform>();
                var proj = go.GetComponent<ProjectileUI>();

                if (rt && proj)
                {
                    // Límits escurçats perquè no surtin a les bores llunyanes
                    float x = Random.Range(-arenaRect.rect.width / 3f, arenaRect.rect.width / 3f);
                    float y = arenaRect.rect.height / 2f - 10f;

                    rt.anchoredPosition = new Vector2(x, y);
                    proj.Init(Vector2.down, 200f);
                }
            }
            yield return new WaitForSeconds(0.45f);
            t += 0.45f;
        }
    }

    // ===================================
    // 2. HORIZONTAL WAVES
    // ===================================
    private IEnumerator HorizontalWavesRoutine(float duration)
    {
        float t = 0f;
        bool leftToRight = true;
        
        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect)
            {
                // Spawn column of 3 vertically spread projectiles on the edge
                float startX = leftToRight ? (-arenaRect.rect.width / 2f + 10f) : (arenaRect.rect.width / 2f - 10f);
                Vector2 dir = leftToRight ? Vector2.right : Vector2.left;

                float h = arenaRect.rect.height;
                float[] waveY = new float[] { -h/3f, 0f, h/3f }; // Top, Mid, Bot
                
                // Hide a random gap by ignoring one of them
                int safeZone = Random.Range(0, 3);

                for (int i = 0; i < 3; i++)
                {
                    if (i == safeZone) continue; 
                    
                    var go = Instantiate(projectilePrefab, projectilesRoot);
                    var rt = go.GetComponent<RectTransform>();
                    var proj = go.GetComponent<ProjectileUI>();

                    if (rt && proj)
                    {
                        rt.anchoredPosition = new Vector2(startX, waveY[i]);
                        proj.Init(dir);
                    }
                }
                
                leftToRight = !leftToRight; // Alternar cantó
            }
            
            yield return new WaitForSeconds(0.8f);
            t += 0.8f;
        }
    }

    // ===================================
    // 3. TARGETED HOMING
    // ===================================
    private IEnumerator TargetedHomingRoutine(float duration)
    {
        float t = 0f;
        var hands = FindObjectsByType<HandController>(FindObjectsSortMode.None);
        
        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect && hands.Length > 0)
            {
                // Pick a hand target randomly
                var targetHand = hands[Random.Range(0, hands.Length)];

                var go = Instantiate(projectilePrefab, projectilesRoot);
                var rt = go.GetComponent<RectTransform>();
                var proj = go.GetComponent<ProjectileUI>();

                if (rt && proj && targetHand != null)
                {
                    // Spawn safely off center, with restricted X limit so it's not out of bounds
                    float startX = Random.Range(-arenaRect.rect.width / 3f, arenaRect.rect.width / 3f);
                    float startY = arenaRect.rect.height / 2f;
                    rt.anchoredPosition = new Vector2(startX, startY);

                    // Calcul de direcció directa al moment del naixement cap a la posició de la mà UI
                    var handRt = targetHand.GetComponent<RectTransform>();
                    if (handRt != null)
                    {
                        // Necessitem les coordenades relatives
                        Vector2 targetPos = handRt.anchoredPosition; 
                        Vector2 dir = (targetPos - rt.anchoredPosition).normalized;
                        proj.Init(dir);
                    }
                }
            }
            
            yield return new WaitForSeconds(0.6f);
            t += 0.6f;
        }
    }

    // ===================================
    // 4. CIRCLE BURST
    // ===================================
    private IEnumerator CircleBurstRoutine(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect)
            {
                int numProjectiles = 8;
                float angleStep = 360f / numProjectiles;
                float startOffset = Random.Range(0f, 360f); // Perquè no surtin sempre al mateix punt

                for (int i = 0; i < numProjectiles; i++)
                {
                    var go = Instantiate(projectilePrefab, projectilesRoot);
                    var rt = go.GetComponent<RectTransform>();
                    var proj = go.GetComponent<ProjectileUI>();

                    if (rt && proj)
                    {
                        rt.anchoredPosition = Vector2.zero; // Centre de la graella
                        float rad = (startOffset + i * angleStep) * Mathf.Deg2Rad;
                        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                        proj.Init(dir);
                    }
                }
            }
            yield return new WaitForSeconds(1.2f);
            t += 1.2f;
        }
    }
}
