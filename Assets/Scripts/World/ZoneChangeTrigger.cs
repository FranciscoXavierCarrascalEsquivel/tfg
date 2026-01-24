using System.Collections;
using UnityEngine;

public class ZoneChangeTrigger : MonoBehaviour
{
    [SerializeField] private Transform targetSpawn;     // on va el jugador
    [SerializeField] private Transform cameraTarget;    // on va la càmera (referència antiga, potser ja no cal si usem limits)
    
    [Header("New Zone Limits")]
    [SerializeField] private Transform targetLimitTop;
    [SerializeField] private Transform targetLimitBottom;
    [SerializeField] private Transform targetLimitLeft;
    [SerializeField] private Transform targetLimitRight;

    [SerializeField] private Camera cam;                // si no l’assignes, agafa Camera.main
    [SerializeField] private ScreenFader fader;

    private bool busy;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (busy) return;
        if (!other.CompareTag("Player")) return;

        StartCoroutine(DoTransition(other.transform));
    }

    private IEnumerator DoTransition(Transform player)
    {
        busy = true;

        if (fader != null) yield return fader.FadeOutToBlack();

        // Mou jugador
        player.position = targetSpawn.position;

        // Actualitza límits càmera
        var camFollow = cam.GetComponent<CameraBoundedFollow>();
        if (camFollow != null)
        {
            camFollow.SetLimits(targetLimitTop, targetLimitBottom, targetLimitLeft, targetLimitRight);
            camFollow.SnapToTarget();
        }
        else if (cameraTarget != null) 
        {
             // Fallback a comportament, antic si no hi ha script de limits
            Vector3 p = cameraTarget.position;
            cam.transform.position = new Vector3(p.x, p.y, cam.transform.position.z);
        }

        if (fader != null) yield return fader.FadeInFromBlack();

        busy = false;
    }
}
