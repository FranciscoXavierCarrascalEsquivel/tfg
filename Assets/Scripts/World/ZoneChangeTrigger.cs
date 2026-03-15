using System.Collections;
using UnityEngine;

public class ZoneChangeTrigger : MonoBehaviour
{
    [SerializeField] private Transform targetSpawn;     // Punt on apareix el jugador a la nova zona.
    [SerializeField] private Transform cameraTarget;    // Referència antiga per la càmera (fallback).
    
    [Header("New Zone Limits")]
    [SerializeField] private Transform targetLimitTop;    // Limit superior de la càmera.
    [SerializeField] private Transform targetLimitBottom; // Limit inferior de la càmera.
    [SerializeField] private Transform targetLimitLeft;   // Limit esquerre de la càmera.
    [SerializeField] private Transform targetLimitRight;  // Limit dret de la càmera.

    [SerializeField] private Camera cam;                // Càmera que s'actualitzarà (per defecte Camera.main).
    [SerializeField] private ScreenFader fader;         // Script per fer l'efecte de fos a negre.
    
    [Header("Music")]
    [Tooltip("Música de l'escena que es pararà quan s'entri al trigger (opcional)")]
    [SerializeField] private SceneMusic sceneMusic;     // Referència a la música de l'escena.

    private bool busy;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (busy) return;
        if (!other.CompareTag("Player")) return;

        // Si el jugador entra, iniciem la transició de zona
        StartCoroutine(DoTransition(other.transform));
    }

    private IEnumerator DoTransition(Transform player)
    {
        busy = true;

        var ctrl = player.GetComponent<PlayerController2D>();
        if (ctrl != null) 
        {
            ctrl.LockMovement();
            ctrl.SuppressEncounters(10f); // Bloqueig temporal llarg durant la càrrega
        }

        // Parem la música de l'escena si hi ha una referència
        if (sceneMusic != null)
        {
            sceneMusic.StopMusic();
        }

        if (fader != null) yield return fader.FadeOutToBlack();

        // Movem el jugador a la nova posició
        player.position = targetSpawn.position;
        if (ctrl != null) ctrl.SuppressEncounters(5f); // Reiniciem lastPos un altre cop just després de moure'l

        // Actualitzem els límits de la càmera per a la nova zona
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

        // Evitem que surtin monstres els primers 2 segons després de la transició real
        if (ctrl != null) 
        {
            ctrl.UnlockMovement();
            ctrl.SuppressEncounters(2f);
        }

        busy = false;
    }
}
