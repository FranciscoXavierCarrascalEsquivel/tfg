using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement2D : MonoBehaviour
{
    [SerializeField] private float speed = 5f; // Velocitat del jugador

    private Rigidbody2D rb; // Rigidbody del jugador
    private Vector2 input; // Entrada del jugador

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>(); // Obtenim el Rigidbody del jugador
        rb.gravityScale = 0f; // Desactivem la gravetat
        rb.freezeRotation = true; // Fixem la rotació
        rb.interpolation = RigidbodyInterpolation2D.Interpolate; // Interpolació per a una moviment més suau
    }

    private void Update() // Update es crida cada frame
    {
        // Llegim l'input de l'usuari (eixos horitzontal i vertical)
        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")); 
        
        // Normalitzem el vector per evitar que es mogui més ràpid en diagonal
        if (input.sqrMagnitude > 1f) input = input.normalized; 
    }

    private void FixedUpdate() // FixedUpdate es crida cada fixedDeltaTime
    {
        Vector2 nextPos = rb.position + input * speed * Time.fixedDeltaTime; // Obtenim la posició del jugador i la fem servir per a calcular la posició del jugador en el frame següent
        rb.MovePosition(nextPos); // Movem el jugador a la nova posició
    }
}
