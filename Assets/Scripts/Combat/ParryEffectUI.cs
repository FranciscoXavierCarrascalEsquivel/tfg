using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple UI animation script that expands and fades an Image, self-destructing when finished.
/// Intended to be attached to the Parry Particle Prefab object containing an Image component.
/// </summary>
public class ParryEffectUI : MonoBehaviour
{
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private float endScale = 2.5f;
    
    private Image img;
    private float timer = 0f;

    private void Awake()
    {
        img = GetComponent<Image>();
    }

    private void Update()
    {
        timer += Time.deltaTime;
        float t = timer / duration;

        // Anima el tamany cap enfora (explosió expansiva)
        transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * endScale, t);
        
        // Anima la transparència fins fer-lo invisible
        if (img != null)
        {
            Color c = img.color;
            c.a = Mathf.Lerp(1f, 0f, t);
            img.color = c;
        }

        // Es destrueix a sí mateix un cop ha acabat l'animació
        if (t >= 1f) Destroy(gameObject);
    }
}
