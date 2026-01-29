using UnityEngine;

public class CameraBoundedFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;          // L'objecte que la càmera seguirà (normalment el jugador).
    public float smoothSpeed = 5f;    // Velocitat de suavitzat del seguiment.
    public Vector3 offset = new Vector3(0, 0, -10); // Desplaçament de la càmera respecte l'objectiu.

    [Header("Limits (Empty GameObjects)")]
    public Transform topLimit;    // Punt que defineix el límit superior.
    public Transform bottomLimit; // Punt que defineix el límit inferior.
    public Transform leftLimit;   // Punt que defineix el límit esquerre.
    public Transform rightLimit;  // Punt que defineix el límit dret.

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Posició desitjada sense tenir en compte els límits
        Vector3 desiredPosition = target.position + offset;
        
        // Apliquem el clamp si tenim la referència de la càmera
        if (cam != null)
        {
            desiredPosition = GetClampedPosition(desiredPosition);
        }

        // Suavitzat
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        smoothedPosition.z = offset.z; 

        transform.position = smoothedPosition;
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

        // Clamp X
        if (minX > maxX) 
            clampedPos.x = (leftLimit.position.x + rightLimit.position.x) / 2f;
        else
            clampedPos.x = Mathf.Clamp(targetPos.x, minX, maxX);

        // Clamp Y
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
}
