using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class HandController : MonoBehaviour
{
    public enum InputMode
    {
        WASD,
        Arrows
    }

    [SerializeField] private InputMode inputMode = InputMode.WASD;
    [SerializeField] private float speed = 500f;
    public float speedMultiplier = 1f;

    private float immunityTimer = 0f;
    private Coroutine blinkCoroutine;
    public bool IsImmune => immunityTimer > 0f;
    
    [Header("Manual Boundaries (Transform Limits)")]
    [Tooltip("Empty Object placed at the Left Limit")]
    [SerializeField] private RectTransform leftBound;
    [Tooltip("Empty Object placed at the Right Limit")]
    [SerializeField] private RectTransform rightBound;
    [Tooltip("Empty Object placed at the Bottom Limit")]
    [SerializeField] private RectTransform bottomBound;
    [Tooltip("Empty Object placed at the Top Limit")]
    [SerializeField] private RectTransform topBound;
    
    // Controlled by CombatManager
    public bool canMove = false; 

    public bool IsMoving { get; private set; }

    private RectTransform rt;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (immunityTimer > 0f)
        {
            immunityTimer -= Time.deltaTime;
        }

        if (!canMove) 
        {
            IsMoving = false;
            return;
        }

        float h = 0f;
        float v = 0f;

        if (inputMode == InputMode.WASD)
        {
            if (Input.GetKey(KeyCode.W)) v += 1f;
            if (Input.GetKey(KeyCode.S)) v -= 1f;
            if (Input.GetKey(KeyCode.D)) h += 1f;
            if (Input.GetKey(KeyCode.A)) h -= 1f;
        }
        else if (inputMode == InputMode.Arrows)
        {
            if (Input.GetKey(KeyCode.UpArrow)) v += 1f;
            if (Input.GetKey(KeyCode.DownArrow)) v -= 1f;
            if (Input.GetKey(KeyCode.RightArrow)) h += 1f;
            if (Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
        }
        
        Vector2 input = new Vector2(h, v).normalized;
        transform.position += (Vector3)(input * (speed * speedMultiplier) * Time.deltaTime);

        IsMoving = input.sqrMagnitude > 0.01f;

        Vector3 pos = transform.position;

        if (leftBound != null && rightBound != null && rt != null)
        {
            float boundLeftEdge = Mathf.Min(leftBound.position.x, rightBound.position.x);
            float boundRightEdge = Mathf.Max(leftBound.position.x, rightBound.position.x);

            Vector3[] myCorners = new Vector3[4];
            rt.GetWorldCorners(myCorners);
            float myMinX = Mathf.Min(myCorners[0].x, myCorners[1].x, myCorners[2].x, myCorners[3].x);
            float myMaxX = Mathf.Max(myCorners[0].x, myCorners[1].x, myCorners[2].x, myCorners[3].x);

            float offsetLeft = Mathf.Abs(transform.position.x - myMinX);
            float offsetRight = Mathf.Abs(myMaxX - transform.position.x);

            float safeMinX = boundLeftEdge + offsetLeft;
            float safeMaxX = boundRightEdge - offsetRight;

            // In some configurations (hand bigger than space), safeMinX could be > safeMaxX.
            if (safeMinX > safeMaxX) safeMaxX = safeMinX;

            pos.x = Mathf.Clamp(pos.x, safeMinX, safeMaxX);
        }

        if (bottomBound != null && topBound != null && rt != null)
        {
            float boundBottomEdge = Mathf.Min(bottomBound.position.y, topBound.position.y);
            float boundTopEdge = Mathf.Max(bottomBound.position.y, topBound.position.y);

            Vector3[] myCorners = new Vector3[4];
            rt.GetWorldCorners(myCorners);
            float myMinY = Mathf.Min(myCorners[0].y, myCorners[1].y, myCorners[2].y, myCorners[3].y);
            float myMaxY = Mathf.Max(myCorners[0].y, myCorners[1].y, myCorners[2].y, myCorners[3].y);

            float offsetBottom = Mathf.Abs(transform.position.y - myMinY);
            float offsetTop = Mathf.Abs(myMaxY - transform.position.y);

            float safeMinY = boundBottomEdge + offsetBottom;
            float safeMaxY = boundTopEdge - offsetTop;

            if (safeMinY > safeMaxY) safeMaxY = safeMinY;

            pos.y = Mathf.Clamp(pos.y, safeMinY, safeMaxY);
        }

        transform.position = pos;
    }

    private void OnDisable()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }
        var images = GetComponentsInChildren<Image>(true);
        if (images.Length == 0) images = GetComponents<Image>();
        foreach (var img in images)
        {
            if (img != null && img.gameObject.name != "HandCollider") 
            {
                img.enabled = true;
                Color c = img.color;
                c.a = 1f;
                img.color = c;
            }
        }
    }

    public void TriggerImmunity(float duration)
    {
        immunityTimer = duration;
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
        }
        blinkCoroutine = StartCoroutine(BlinkRoutine(duration));
    }

    private IEnumerator BlinkRoutine(float duration)
    {
        var images = GetComponentsInChildren<Image>(true);
        if (images.Length == 0) images = GetComponents<Image>();

        List<Image> validImages = new List<Image>();
        foreach (var img in images)
        {
            if (img != null && img.gameObject.name != "HandCollider")
            {
                validImages.Add(img);
            }
        }

        float elapsed = 0f;
        float fadeSpeed = 25f; // Velocitat del "batec"

        while (elapsed < duration)
        {
            // Calcula un valor suau entre 0.2 i 1 utilitzant una funció cosinus
            float alpha = Mathf.Lerp(0.2f, 1f, (Mathf.Cos(elapsed * fadeSpeed) + 1f) / 2f);

            foreach (var img in validImages)
            {
                Color c = img.color;
                c.a = alpha;
                img.color = c;
            }
            
            yield return null; // Executa a cada frame per a una transició fluida
            elapsed += Time.deltaTime;
        }

        // Restaura l'opacitat al màxim al final
        foreach (var img in validImages)
        {
            Color c = img.color;
            c.a = 1f;
            img.color = c;
        }
        blinkCoroutine = null;
    }
}
