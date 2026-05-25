using UnityEngine;

/// <summary>
/// Component de moviment físic bàsic en 2D en vista de dalt a baix (Top-Down).
/// Aquest script llegeix els valors clàssics d'eixos (Horizontal i Vertical) de forma directa,
/// normalitza la direcció per evitar velocitats diagonals excessives i desplaça el cos
/// físic (Rigidbody2D) mitjançant forces de col·lisió suaus a la funció FixedUpdate.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement2D : MonoBehaviour
{
    [Header("Configuració de Velocitat")]
    [SerializeField] private float speed = 5f; // Velocitat a la qual es desplaça el jugador en unitats/segon.

    private Rigidbody2D rb; // Component de físiques de Unity.
    private Vector2 input;  // Vector bidimensional per emmagatzemar l'input d'entrada.

    private void Awake()
    {
        // Recuperem el Rigidbody2D i forcem els paràmetres físics desitjats per a moviment 2D net.
        rb = GetComponent<Rigidbody2D>(); 
        rb.gravityScale = 0f; // Evitem caigudes, ja que és un mapa en vista superior.
        rb.freezeRotation = true; // Impedim que el personatge roti sobre l'eix Z en col·lidir amb cantonades.
        rb.interpolation = RigidbodyInterpolation2D.Interpolate; // Suavitza el desplaçament físic segons els fotogrames de render.
    }

    private void Update()
    {
        // Llegim l'input horitzontal (tecles A/D, Esquerra/Dreta) i vertical (W/S, Dalt/Abaix).
        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")); 
        
        // Si es pitgen dues tecles alhora, el mòdul del vector superaria 1. 
        // Normalitzem per assegurar que la velocitat diagonal sigui equivalent a la cardinal.
        if (input.sqrMagnitude > 1f) 
            input = input.normalized; 
    }

    private void FixedUpdate()
    {
        // Calculem la posició següent i desplacem el Rigidbody respectant col·lisions i obstacles del motor físic.
        Vector2 nextPos = rb.position + input * speed * Time.fixedDeltaTime; 
        rb.MovePosition(nextPos); 
    }
}
