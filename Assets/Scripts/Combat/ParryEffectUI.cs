using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Efecte Visual d'Impacte i Parada de Bales (ParryEffectUI).
/// Petit script utilitari d'animació que s'encarrega d'expandir de forma explosiva
/// i desvanir (fade-out) una imatge de cercle o escut en el frame exacte que es realitza
/// una parada amb èxit (Parry) en el combat.
/// 
/// DISSENY DELS FX DEL TFG:
/// - **Pop-up expansiu**: Incrementa l'escala local tridimensional (Scale) de forma exponencial.
///   fins assolir el límit especificat (`endScale`).
/// - **Fosa transparent**: Redueix el canal Alfa de la textura a zero al llarg de la seva curta vida.
/// - **Alliberament neta de recursos**: Es destrueix a si mateix en finalitzar la transició per no saturar la memòria.
/// </summary>
public class ParryEffectUI : MonoBehaviour
{
    [SerializeField] private float duration = 0.5f; // Durada dels microsegons de l'escut
    [SerializeField] private float endScale = 2.5f;   // Escala d'expansió límit (efecte explosió de xoc)
    
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

        // Anima la mida de l'escut cap enfora simulant l'ona expansiva del tall
        transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * endScale, t);
        
        // Esvaeix gradualment l'opacitat de l'ona de xoc celest
        if (img != null)
        {
            Color c = img.color;
            c.a = Mathf.Lerp(1f, 0f, t);
            img.color = c;
        }

        // Destrucció asíncrona higiènica en culminar
        if (t >= 1f) Destroy(gameObject);
    }
}
