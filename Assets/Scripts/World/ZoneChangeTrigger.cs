using System.Collections;
using UnityEngine;

/// <summary>
/// Trigger 2D encarregat de realitzar transicions de canvi de zona o teletransports a l'Overworld.
/// Quan el jugador hi entra, el script activa un fos a negre (ScreenFader), bloqueja els controls,
/// teletransporta el personatge, actualitza els nous límits físics de la càmera (CameraBoundedFollow)
/// per enquadrar la nova sala o mapa, i finalment torna a aclarir la pantalla donant-li un temps de gràcia
/// de seguretat contra trobades de combats aleatoris.
/// </summary>
public class ZoneChangeTrigger : MonoBehaviour
{
    // Esdeveniment delegat per a notificar a altres sistemes externs que ha començat un canvi de zona
    public delegate void ZoneTransitionHandler();
    public event ZoneTransitionHandler OnZoneTransition;

    [Header("Destí de Teletransport")]
    [SerializeField] private Transform targetSpawn;     // Punt exacte on es col·locarà el jugador a la nova zona.
    [SerializeField] private Transform cameraTarget;    // Referència secundària de seguretat per a orientar la càmera (fallback).
    
    [Header("Nous Límits de Càmera (Bounding Box)")]
    [SerializeField] private Transform targetLimitTop;    // Objecte de control que marca el límit superior.
    [SerializeField] private Transform targetLimitBottom; // Objecte de control que marca el límit inferior.
    [SerializeField] private Transform targetLimitLeft;   // Objecte de control que marca el límit esquerre.
    [SerializeField] private Transform targetLimitRight;  // Objecte de control que marca el límit dret.

    [Header("Referències a Elements Globals")]
    [SerializeField] private Camera cam;                // Càmera que s'actualitzarà (per defecte, la principal).
    [SerializeField] private ScreenFader fader;         // Panell de Canvas per a realitzar el fos a negre.
    
    [Header("Música (Opcional)")]
    [Tooltip("Música de l'escena que s'aturarà en entrar al trigger")]
    [SerializeField] private SceneMusic sceneMusic;     // Referència a la música activa del mapa actual.

    private bool busy; // Control intern per evitar que la transició s'executi molts cops si el jugador entra ràpid

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (busy) return;
        if (!other.CompareTag("Player")) return;

        // Disparem l'esdeveniment de transició per si altres sistemes s'hi volen acoblar
        OnZoneTransition?.Invoke();

        // Iniciem la seqüència de transició gràfica i física
        StartCoroutine(DoTransition(other.transform));
    }

    /// <summary>
    /// Corrutina seqüencial que controla tota l'experiència de viatge/canvi de sala.
    /// </summary>
    private IEnumerator DoTransition(Transform player)
    {
        busy = true;

        var ctrl = player.GetComponent<PlayerController2D>();
        if (ctrl != null) 
        {
            // Bloquegem immediatament els inputs del jugador per evitar que continuï caminant
            ctrl.LockMovement();
            // Immunitat forçada llarga preventiva per evitar que es dispari un combat al mig del fader
            ctrl.SuppressEncounters(10f); 
        }

        // Aturem la música si escau
        if (sceneMusic != null)
        {
            sceneMusic.StopMusic();
        }

        // 1) Realitzem el fos a negre i esperem que finalitzi
        if (fader != null) yield return fader.FadeOutToBlack();

        // 2) Teletransportem el personatge al nou Spawn
        player.position = targetSpawn.position;
        // Reiniciem la memòria cau de posició (lastPos) just després del moviment per a evitar salts estranys de distància
        if (ctrl != null) ctrl.SuppressEncounters(5f); 

        // 3) Re-assignem els límits de contenció de càmera perquè no mostri zones fora de mapa
        var camFollow = cam.GetComponent<CameraBoundedFollow>();
        if (camFollow != null)
        {
            camFollow.SetLimits(targetLimitTop, targetLimitBottom, targetLimitLeft, targetLimitRight);
            camFollow.SnapToTarget(); // Reposiciona la càmera de forma instantània (sense suavitzat temporal)
        }
        else if (cameraTarget != null) 
        {
             // Fallback clàssic si no està configurat el script de límits
            Vector3 p = cameraTarget.position;
            cam.transform.position = new Vector3(p.x, p.y, cam.transform.position.z);
        }

        // 4) Desfem el fos a negre
        if (fader != null) yield return fader.FadeInFromBlack();

        // 5) Donem 2 segons d'immunitat de combat un cop la pantalla ja és completament visible
        if (ctrl != null) 
        {
            ctrl.UnlockMovement();
            ctrl.SuppressEncounters(2f);
        }

        busy = false;
    }
}
