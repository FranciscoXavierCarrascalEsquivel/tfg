using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gestiona l'efecte visual de "glow" (resplendor) i ampliació de botons de la UI en passar-hi el cursor (ButtonGlowHover).
/// Implementa els controladors d'esdeveniments d'Unity IPointerEnterHandler i IPointerExitHandler.
/// Quan el punter entra a l'àrea del botó:
/// 1) S'amplia lleugerament l'escala de l'objecte per donar feedback de prement.
/// 2) S'activa un perfil de contorn gruixut de TextMeshPro (outlineColor/outlineWidth) amb el color de glow.
/// 3) Si està actiu, inicia un bucle de pulsació suau estil respiració (sinusoïdal) en la lluentor.
/// S'utilitzen temps no escalats (unscaledDeltaTime) per a garantir que les animacions de la UI responguin
/// perfectament fins i tot quan el joc està pausat (Time.timeScale = 0).
/// </summary>
public class ButtonGlowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Referències d'Elements")]
    [SerializeField] private Image buttonImage;   // Imatge opcional de fons del botó
    [SerializeField] private TMP_Text buttonText; // Camp de text recomanat per rebre l'efecte de resplendor

    [Header("Configuració de l'Escala")]
    [SerializeField] private float hoverScale = 1.03f; // Escala multiplicadora objectiu en passar per sobre
    [SerializeField] private float smoothTime = 0.12f; // Durada en segons de la transició de mida

    [Header("Efecte Glow (Contorn TMP)")]
    [SerializeField] private bool glowText = true; // Activa o desactiva l'efecte de contorn de text
    [SerializeField] private Color glowColor = new Color(0.4f, 1f, 0.9f, 1f); // Color del resplendor (cian retro per defecte)
    [SerializeField] private float outlineOff = 0.02f;    // Gruix del contorn quan el cursor està fora
    [SerializeField] private float outlineOn = 0.25f;     // Gruix del contorn quan el cursor està a sobre

    [Header("Efecte de Pulsació Respiratòria")]
    [SerializeField] private bool pulse = true; // Si es marca, l'opacitat del contorn fluctuarà mentre el cursor hi sigui a sobre
    [SerializeField] private float pulseSpeed = 2.2f; // Velocitat d'oscil·lació del bucle
    [SerializeField] private float pulseAmount = 0.08f;   // Rang addicional d'amplitud sinusoïdal

    private Vector3 baseScale; // Escala original del botó
    private bool hovering;     // Flag d'estat de si el ratolí està a sobre
    private Coroutine anim;    // Referència a la corrutina d'animació activa

    // Còpies dels valors de color base per a la transició inversa de sortida
    private Color baseImgColor;
    private Color baseTextColor;

    private void Awake()
    {
        // Auto-cerca de components si no s'han associat per inspector
        if (buttonImage == null) buttonImage = GetComponent<Image>();
        if (buttonText == null) buttonText = GetComponentInChildren<TMP_Text>(true);

        baseScale = transform.localScale;

        if (buttonImage != null) baseImgColor = buttonImage.color;
        if (buttonText != null) baseTextColor = buttonText.color;

        // Establim l'estat de contorn inicial apagat
        ApplyGlow(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(Animate(true));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(Animate(false));
    }

    /// <summary>
    /// Corrutina d'animació de transició d'entrada o sortida d'estat hover.
    /// </summary>
    private IEnumerator Animate(bool toHover)
    {
        float t = 0f;

        Vector3 startScale = transform.localScale;
        Vector3 targetScale = baseScale * (toHover ? hoverScale : 1f);

        float startOutline = GetOutline();
        float targetOutline = toHover ? outlineOn : outlineOff;

        // Multipliquem la intensitat del color del text una mica si hi passem per damunt per donar feedback lumínic
        Color startTxt = buttonText ? buttonText.color : Color.white;
        Color targetTxt = buttonText ? (toHover ? MultiplyColor(baseTextColor, 1.08f) : baseTextColor) : Color.white;

        while (t < smoothTime)
        {
            t += Time.unscaledDeltaTime; // Unscaled delta time per garantir moviment amb el joc pausat
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, smoothTime));

            // Interpolació d'escala i contorn de text
            transform.localScale = Vector3.Lerp(startScale, targetScale, k);

            if (buttonText != null)
            {
                buttonText.color = Color.Lerp(startTxt, targetTxt, k);
                SetOutline(Mathf.Lerp(startOutline, targetOutline, k));
            }

            yield return null;
        }

        transform.localScale = targetScale;

        if (buttonText != null)
        {
            buttonText.color = targetTxt;
            SetOutline(targetOutline);
            SetGlowColor(glowColor);
        }

        // --- EFECTE DE PULSACIÓ ACTIVA MENTRE ES REMAN A SOBRE ---
        if (toHover && pulse && buttonText != null)
        {
            while (hovering)
            {
                // Generem una ona sinusoïdal pura entre 0 i 1
                float p = (Mathf.Sin(Time.unscaledTime * pulseSpeed) * 0.5f + 0.5f); 
                float extra = (p - 0.5f) * 2f * pulseAmount; 
                
                // Modulem dinàmicament el gruix de la vora
                SetOutline(outlineOn + extra);
                yield return null;
            }
        }

        // En sortir del botó, ens assegurem de restaurar les propietats de contorn apagades normals
        if (!toHover) ApplyGlow(false);

        anim = null;
    }

    // =========================================================================
    // METODES D'AJUST DE CONTORN NATIV DE TEXTMESHPRO
    // =========================================================================

    private void ApplyGlow(bool on)
    {
        if (buttonText == null || !glowText) return;
        SetGlowColor(glowColor);
        SetOutline(on ? outlineOn : outlineOff);
    }

    private void SetGlowColor(Color c)
    {
        if (buttonText == null || !glowText) return;
        buttonText.outlineColor = c;
    }

    private float GetOutline()
    {
        if (buttonText == null || !glowText) return 0f;
        return buttonText.outlineWidth;
    }

    private void SetOutline(float w)
    {
        if (buttonText == null || !glowText) return;
        buttonText.outlineWidth = Mathf.Clamp01(w);
    }

    /// <summary>
    /// Multiplica els canals vermell, verd i blau d'un color per un coeficient d'intensitat lumínica.
    /// </summary>
    private static Color MultiplyColor(Color c, float m)
    {
        return new Color(
            Mathf.Clamp01(c.r * m),
            Mathf.Clamp01(c.g * m),
            Mathf.Clamp01(c.b * m),
            c.a
        );
    }
}
