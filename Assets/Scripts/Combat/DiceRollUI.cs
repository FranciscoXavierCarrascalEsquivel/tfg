using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A realistic 2D Dice/Number rolling simulator.
/// Uses scale popping, random rotation tilting, and exponential slowdown to create a "juicy" UI wheel effect.
/// </summary>
public class DiceRollUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The text component that displays the flowing numbers")]
    [SerializeField] private TMP_Text numberText;
    [Tooltip("The parent RectTransform that will bounce and tilt (can be the dice background)")]
    [SerializeField] private RectTransform diceRect;

    [Header("Roll Settings")]
    [SerializeField] private float rollDuration = 2f;
    [SerializeField] private int visualMinNumber = 1;
    [SerializeField] private int visualMaxNumber = 6;
    
    [Header("Audio (Optional)")]
    [SerializeField] private AudioClip tickSound;
    [SerializeField] private AudioClip landSound;
    private AudioSource audioSource;

    private void Awake()
    {
        if (diceRect == null) diceRect = GetComponent<RectTransform>();
        
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    public IEnumerator RollRoutine(int finalResult)
    {
        float timer = 0f;
        float switchInterval = 0.05f;
        float nextSwitchTime = 0f;

        Vector3 originalScale = Vector3.one;
        if (diceRect != null) originalScale = diceRect.localScale;

        // Bucle de rodar
        while (timer < rollDuration)
        {
            timer += Time.deltaTime;

            if (timer >= nextSwitchTime)
            {
                // Canviem el número
                if (numberText != null)
                {
                    numberText.text = Random.Range(visualMinNumber, visualMaxNumber + 1).ToString();
                }

                // Animació de sacsejada (Juice)
                Vector3 punchScale = originalScale * 1.3f;
                Vector3 punchEuler = new Vector3(0, 0, Random.Range(-15f, 15f));
                
                if (diceRect != null)
                {
                    diceRect.localScale = punchScale;
                    diceRect.localEulerAngles = punchEuler;
                }
                
                if (numberText != null)
                {
                    numberText.transform.localScale = punchScale;
                    numberText.transform.localEulerAngles = punchEuler;
                }

                // So de tick
                if (tickSound != null && audioSource != null)
                {
                    audioSource.pitch = Random.Range(0.9f, 1.1f);
                    audioSource.PlayOneShot(tickSound);
                }

                // Reduïm la velocitat exponencialment a mesura que avança el temps
                float progress = timer / rollDuration;
                switchInterval = Mathf.Lerp(0.04f, 0.5f, progress * progress * progress);
                nextSwitchTime = timer + switchInterval;
            }

            // Suavitzem l'escala de tornada a la normalitat
            Vector3 targetLerp = Vector3.Lerp(diceRect != null ? diceRect.localScale : 
                                             (numberText != null ? numberText.transform.localScale : originalScale), 
                                              originalScale, Time.deltaTime * 15f);
            
            if (diceRect != null) diceRect.localScale = targetLerp;
            if (numberText != null) numberText.transform.localScale = targetLerp;

            yield return null;
        }

        // RESULTAT FINAL
        Color originalColor = Color.white;
        if (numberText != null) 
        {
            originalColor = numberText.color;
            numberText.text = finalResult.ToString();
            numberText.color = new Color(1f, 0.2f, 0.2f); // Impact Red
        }
        
        if (landSound != null && audioSource != null)
        {
            audioSource.pitch = 1f;
            audioSource.PlayOneShot(landSound);
        }

        // Gran impacte visual al decantar-se pel número final
        Vector3 landScale = originalScale * 2.2f;
        if (diceRect != null)
        {
            diceRect.localScale = landScale;
            diceRect.localEulerAngles = Vector3.zero;
        }
        if (numberText != null)
        {
            numberText.transform.localScale = landScale;
            numberText.transform.localEulerAngles = Vector3.zero;
        }

        // Animació de repòs final
        float endTimer = 0f;
        while (endTimer < 0.3f)
        {
            endTimer += Time.deltaTime;
            float t = endTimer / 0.3f;
            
            if (diceRect != null)
            {
                diceRect.localScale = Vector3.Lerp(diceRect.localScale, originalScale, t);
            }
            if (numberText != null)
            {
                numberText.transform.localScale = Vector3.Lerp(numberText.transform.localScale, originalScale, t);
                numberText.color = Color.Lerp(new Color(1f, 0.2f, 0.2f), originalColor, t);
            }
            yield return null;
        }
        
        // Assegurem que queda net
        if (numberText != null)
        {
            numberText.transform.localScale = originalScale;
            numberText.transform.localEulerAngles = Vector3.zero;
        }
    }
}
