using UnityEngine;

public class CameraBoundedFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;          
    public float smoothSpeed = 5f;    
    public Vector3 offset = new Vector3(0, 0, -10);

    [Header("Limits (Empty GameObjects)")]
    public Transform topLimit;
    public Transform bottomLimit;
    public Transform leftLimit;
    public Transform rightLimit;

    [Header("Follow Background (optional)")]
    public Transform backgroundToFollow;  // <-- Arrossega aquí el GO del fons
    public bool keepBackgroundZ = true;   // Manté el Z original del fons

    private Camera cam;
    private float backgroundZ;
    private Vector3 velocity = Vector3.zero; // Velocity buffer per SmoothDamp

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        if (backgroundToFollow != null)
            backgroundZ = backgroundToFollow.position.z;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Posició desitjada
        Vector3 desiredPosition = target.position + offset;

        // Clamp
        if (cam != null)
            desiredPosition = GetClampedPosition(desiredPosition);

        // Suavitzat amb SmoothDamp (consistent independentment del framerate)
        float smoothTime = 1f / Mathf.Max(0.01f, smoothSpeed);
        Vector3 smoothedPosition = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);
        smoothedPosition.z = offset.z;

        // Apliquem a la càmera
        transform.position = smoothedPosition;

        // --- FONS: es mou igual que la càmera (mateixa posició final) ---
        if (backgroundToFollow != null)
        {
            Vector3 bgPos = backgroundToFollow.position;

            bgPos.x = smoothedPosition.x;
            bgPos.y = smoothedPosition.y;

            if (keepBackgroundZ) bgPos.z = backgroundZ;

            backgroundToFollow.position = bgPos;

            // --- ESCALAT DINÀMIC DEL FONS ---
            SpriteRenderer sr = backgroundToFollow.GetComponent<SpriteRenderer>();
            if (sr != null && cam != null)
            {
                float camHeight = cam.orthographicSize * 2.2f;
                float camWidth = camHeight * cam.aspect;
                Vector2 spriteSize = sr.sprite.bounds.size;
                if (spriteSize.x > 0 && spriteSize.y > 0)
                {
                    float scaleX = camWidth / spriteSize.x;
                    float scaleY = camHeight / spriteSize.y;
                    float finalScale = Mathf.Max(scaleX, scaleY);
                    backgroundToFollow.localScale = new Vector3(finalScale, finalScale, 1f);
                }
            }
        }
    }

    private Vector3 GetClampedPosition(Vector3 targetPos)
    {
        float camHeight = cam.orthographicSize;
        float camWidth = camHeight * cam.aspect;

        float minX = (leftLimit != null) ? leftLimit.position.x + camWidth : float.MinValue;
        float maxX = (rightLimit != null) ? rightLimit.position.x - camWidth : float.MaxValue;

        float minY = (bottomLimit != null) ? bottomLimit.position.y + camHeight : float.MinValue;
        float maxY = (topLimit != null) ? topLimit.position.y - camHeight : float.MaxValue;

        Vector3 clampedPos = targetPos;

        if (minX > maxX)
            clampedPos.x = (leftLimit.position.x + rightLimit.position.x) / 2f;
        else
            clampedPos.x = Mathf.Clamp(targetPos.x, minX, maxX);

        if (minY > maxY)
            clampedPos.y = (bottomLimit.position.y + topLimit.position.y) / 2f;
        else
            clampedPos.y = Mathf.Clamp(targetPos.y, minY, maxY);

        return clampedPos;
    }

    public void SnapToTarget()
    {
        if (target == null || cam == null) return;

        Vector3 desiredPosition = target.position + offset;
        Vector3 clampedPosition = GetClampedPosition(desiredPosition);
        clampedPosition.z = offset.z;

        transform.position = clampedPosition;
        velocity = Vector3.zero; // Reset velocity after snap per evitar inèrcia residual

        // també "snap" del fons
        if (backgroundToFollow != null)
        {
            Vector3 bgPos = backgroundToFollow.position;
            bgPos.x = clampedPosition.x;
            bgPos.y = clampedPosition.y;
            if (keepBackgroundZ) bgPos.z = backgroundZ;
            backgroundToFollow.position = bgPos;

            // --- ESCALAT DINÀMIC DEL FONS ---
            SpriteRenderer sr = backgroundToFollow.GetComponent<SpriteRenderer>();
            if (sr != null && cam != null)
            {
                float camHeight = cam.orthographicSize * 2.2f; // Una mica de marge extra per seguretat
                float camWidth = camHeight * cam.aspect;

                Vector2 spriteSize = sr.sprite.bounds.size;
                if (spriteSize.x > 0 && spriteSize.y > 0)
                {
                    float scaleX = camWidth / spriteSize.x;
                    float scaleY = camHeight / spriteSize.y;
                    float finalScale = Mathf.Max(scaleX, scaleY);
                    backgroundToFollow.localScale = new Vector3(finalScale, finalScale, 1f);
                }
            }
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void SetLimits(Transform top, Transform bottom, Transform left, Transform right)
    {
        topLimit = top;
        bottomLimit = bottom;
        leftLimit = left;
        rightLimit = right;
    }

    // (Opcional) per canviar el fons per codi
    public void SetBackground(Transform bg)
    {
        backgroundToFollow = bg;
        if (backgroundToFollow != null) backgroundZ = backgroundToFollow.position.z;
    }
}
