using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ButtonGlowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Refs")]
    [SerializeField] private Image buttonImage;   // opcional
    [SerializeField] private TMP_Text buttonText; // recomanat

    [Header("Scale")]
    [SerializeField] private float hoverScale = 1.03f;
    [SerializeField] private float smoothTime = 0.12f;

    [Header("Glow (real) - Outline TMP")]
    [SerializeField] private bool glowText = true;
    [SerializeField] private Color glowColor = new Color(0.4f, 1f, 0.9f, 1f);
    [SerializeField] private float outlineOff = 0.02f;    // quan NO està hover
    [SerializeField] private float outlineOn = 0.25f;     // quan està hover

    [Header("Pulse (suau)")]
    [SerializeField] private bool pulse = true;
    [SerializeField] private float pulseSpeed = 2.2f;
    [SerializeField] private float pulseAmount = 0.08f;   // intensitat pulse sobre outlineOn

    private Vector3 baseScale;
    private bool hovering;
    private Coroutine anim;

    // Guardem valors base
    private Color baseImgColor;
    private Color baseTextColor;

    private void Awake()
    {
        if (buttonImage == null) buttonImage = GetComponent<Image>();
        if (buttonText == null) buttonText = GetComponentInChildren<TMP_Text>(true);

        baseScale = transform.localScale;

        if (buttonImage != null) baseImgColor = buttonImage.color;
        if (buttonText != null) baseTextColor = buttonText.color;

        // Estat inicial del “glow”
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

    private IEnumerator Animate(bool toHover)
    {
        float t = 0f;

        Vector3 startScale = transform.localScale;
        Vector3 targetScale = baseScale * (toHover ? hoverScale : 1f);

        // Per fer transició suau del glow
        float startOutline = GetOutline();
        float targetOutline = toHover ? outlineOn : outlineOff;

        // Si vols també una mica de “lift” al color (opc)
        Color startTxt = buttonText ? buttonText.color : Color.white;
        Color targetTxt = buttonText ? (toHover ? MultiplyColor(baseTextColor, 1.08f) : baseTextColor) : Color.white;

        while (t < smoothTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, smoothTime));

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

        // Pulse suau del glow mentre estàs a sobre
        if (toHover && pulse && buttonText != null)
        {
            while (hovering)
            {
                float p = (Mathf.Sin(Time.unscaledTime * pulseSpeed) * 0.5f + 0.5f); // 0..1
                float extra = (p - 0.5f) * 2f * pulseAmount; // -pulseAmount..+pulseAmount
                SetOutline(outlineOn + extra);
                yield return null;
            }
        }

        // Quan surt, assegura tornar a base
        if (!toHover) ApplyGlow(false);

        anim = null;
    }

    // ---------- TMP Glow helpers ----------
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
