using UnityEngine;

/// <summary>
/// Controlador didàctic d'enquadrament i seguiment de càmera 2D (CameraBoundedFollow).
/// Desplaça de forma suau i robusta la càrrega de la càmera (Camera.main) darrere del jugador
/// utilitzant Vector3.SmoothDamp (consistent independentment de la taxa de fotogrames/framerate).
/// Restringeix la posició de la càmera (Clamping) a partir d'un volum de caiguda definit per 4 Transforms límits,
/// calculant de forma precisa les dimensions de la pantalla a partir de l'orthographicSize i la ràtio d'aspecte.
/// A més, gestiona de forma automàtica el centrat i escalat dinàmic de l'escenari de fons (Background)
/// perquè s'adapti de forma coherent a monitors de qualsevol resolució o format de pantalla (ex: 21:9 o 16:9).
/// </summary>
public class CameraBoundedFollow : MonoBehaviour
{
    [Header("Seguiment de l'Objectiu")]
    public Transform target;          // Personatge o objectiu a seguir (jugador)
    public float smoothSpeed = 5f;    // Velocitat de suavitzat (a major valor, seguiment més ràpid)
    public Vector3 offset = new Vector3(0, 0, -10); // Desviació espacial de la càmera respecte a l'objectiu

    [Header("Límits Físics de Sala (Transforms buits)")]
    public Transform topLimit;
    public Transform bottomLimit;
    public Transform leftLimit;
    public Transform rightLimit;

    [Header("Seguiment del Fons (Opcional)")]
    public Transform backgroundToFollow;  // L'objecte gràfic del fons que es desplaçarà amb la càmera
    public bool keepBackgroundZ = true;   // Conserva la posició de profunditat (Z) inicial del fons

    private Camera cam;
    private float backgroundZ;
    private Vector3 velocity = Vector3.zero; // Buffer de velocitat intern exigit pel mètode SmoothDamp

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

        // Calculem la posició teòrica ideal a la qual s'hauria de situar la càmera
        Vector3 desiredPosition = target.position + offset;

        // Limitem les coordenades X i Y segons la mida física de la viewport de la càmera respecte als límits
        if (cam != null)
            desiredPosition = GetClampedPosition(desiredPosition);

        // Calculem la interpolació amortiguada (SmoothDamp)
        float smoothTime = 1f / Mathf.Max(0.01f, smoothSpeed);
        Vector3 smoothedPosition = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);
        smoothedPosition.z = offset.z;

        // Apliquem la posició a la càmera
        transform.position = smoothedPosition;

        // --- SISTEMA DE CONTROL DE FONS PERSISTENT ---
        if (backgroundToFollow != null)
        {
            Vector3 bgPos = backgroundToFollow.position;

            bgPos.x = smoothedPosition.x;
            bgPos.y = smoothedPosition.y;

            if (keepBackgroundZ) bgPos.z = backgroundZ;

            backgroundToFollow.position = bgPos;

            // --- ESCALAT DINÀMIC DEL FONS SEGONS ASPECTE DE PANTALLA (RESPONSIVE) ---
            SpriteRenderer sr = backgroundToFollow.GetComponent<SpriteRenderer>();
            if (sr != null && cam != null)
            {
                // Multipliquem per un coeficient de seguretat (2.2) per evitar que es vegin vores buides als costats
                float camHeight = cam.orthographicSize * 2.2f;
                float camWidth = camHeight * cam.aspect;
                Vector2 spriteSize = sr.sprite.bounds.size;
                
                if (spriteSize.x > 0 && spriteSize.y > 0)
                {
                    // Calculem les ràtios de proporció X i Y respecte a la mida del sprite
                    float scaleX = camWidth / spriteSize.x;
                    float scaleY = camHeight / spriteSize.y;
                    float finalScale = Mathf.Max(scaleX, scaleY); // Triem la ràtio més gran per cobrir tota l'àrea
                    
                    backgroundToFollow.localScale = new Vector3(finalScale, finalScale, 1f);
                }
            }
        }
    }

    /// <summary>
    /// Calcula i retorna la posició de la càmera reprimida (Clamped) dins dels límits del mapa,
    /// tenint en compte les dimensions de la pantalla segons la ràtio d'aspecte de la càmera.
    /// </summary>
    private Vector3 GetClampedPosition(Vector3 targetPos)
    {
        float camHeight = cam.orthographicSize;
        float camWidth = camHeight * cam.aspect;

        // Calculem els marges de contenció físics de la lent
        float minX = (leftLimit != null) ? leftLimit.position.x + camWidth : float.MinValue;
        float maxX = (rightLimit != null) ? rightLimit.position.x - camWidth : float.MaxValue;

        float minY = (bottomLimit != null) ? bottomLimit.position.y + camHeight : float.MinValue;
        float maxY = (topLimit != null) ? topLimit.position.y - camHeight : float.MaxValue;

        Vector3 clampedPos = targetPos;

        // Si l'àrea del mapa és inferior a la grandària de la lent de la càmera, la centrem exactament al mig
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

    /// <summary>
    /// Teletransporta i enquadra instantàniament la càmera a sobre de l'objectiu actiu,
    /// anul·lant inèrcies residuals o lliscaments de suavitzat posteriors (útil en teletransports de ZoneChangeTrigger).
    /// </summary>
    public void SnapToTarget()
    {
        if (target == null || cam == null) return;

        Vector3 desiredPosition = target.position + offset;
        Vector3 clampedPosition = GetClampedPosition(desiredPosition);
        clampedPosition.z = offset.z;

        transform.position = clampedPosition;
        velocity = Vector3.zero; // Anul·lem acceleracions residuals de forces de seguiment

        // Teletransportem també el fons
        if (backgroundToFollow != null)
        {
            Vector3 bgPos = backgroundToFollow.position;
            bgPos.x = clampedPosition.x;
            bgPos.y = clampedPosition.y;
            if (keepBackgroundZ) bgPos.z = backgroundZ;
            backgroundToFollow.position = bgPos;

            // Recalculem l'escalat dinàmic per a la nova vista
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

    public void SetBackground(Transform bg)
    {
        backgroundToFollow = bg;
        if (backgroundToFollow != null) backgroundZ = backgroundToFollow.position.z;
    }
}
