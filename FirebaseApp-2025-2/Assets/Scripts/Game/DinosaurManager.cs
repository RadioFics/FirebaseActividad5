using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class DinosaurManager : MonoBehaviour
{
    [Header("Salto inmediato + altura variable")]
    [SerializeField] private float initialJumpForce = 8f;            
    [SerializeField] private float extraJumpForcePerSecond = 12f;    
    [SerializeField] private float maxExtraJumpTime = 0.35f;        

    [Header("Caída rápida")]
    [SerializeField] private float fastFallImpulse = 10f;
    [SerializeField] private float fallGravityMultiplier = 2.2f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float radius = 0.1f;
    [SerializeField] private LayerMask ground;

    [Header("Colliders para cambiar al agacharse (2 colliders)")]
    [SerializeField] private Collider2D standingCollider;
    [SerializeField] private Collider2D crouchCollider;

    [Header("Ajustes y helpers")]
    [SerializeField] private bool enableBottomSnap = true; 

    private Rigidbody2D rb;
    private Animator animator;

    private bool isJumpingVar = false;    
    private float extraJumpTimer = 0f;       
    private bool jumpHeld = false;  

    private bool isDead = false;
    private bool isBend = false;
    private bool lastCrouchState = false;
    private float bottomShift = 0f;
    private float originalGravityScale;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        originalGravityScale = rb.gravityScale;

        if (standingCollider != null) standingCollider.enabled = true;
        if (crouchCollider != null) crouchCollider.enabled = false;

        if (enableBottomSnap && standingCollider != null && crouchCollider != null)
        {
            bool prevStanding = standingCollider.enabled;
            bool prevCrouch = crouchCollider.enabled;

            standingCollider.enabled = true;
            crouchCollider.enabled = true;

            Bounds bStand = standingCollider.bounds;
            Bounds bCrouch = crouchCollider.bounds;
            bottomShift = bStand.min.y - bCrouch.min.y;

            standingCollider.enabled = prevStanding;
            crouchCollider.enabled = prevCrouch;
        }
    }

    void Update()
    {
        if (isDead) return;

        bool isGrounded = Physics2D.OverlapCircle(groundCheck.position, radius, ground);
        animator?.SetBool("isGrounded", isGrounded);

        bool jumpDown = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
        jumpHeld = Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0) || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        bool jumpReleased = Input.GetKeyUp(KeyCode.Space) || Input.GetMouseButtonUp(0) || Input.GetKeyUp(KeyCode.W) || Input.GetKeyUp(KeyCode.UpArrow);

        bool wantToCrouch = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.LeftControl);

        if (jumpDown && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            rb.AddForce(Vector2.up * initialJumpForce, ForceMode2D.Impulse);

            isJumpingVar = true;
            extraJumpTimer = 0f;
        }

        if (jumpReleased)
        {
            isJumpingVar = false;
        }

        isBend = wantToCrouch && isGrounded;
        animator?.SetBool("isBend", isBend);
        ApplyCrouchState(isBend);
    }

    void FixedUpdate()
    {
        if (isDead) return;

        bool isGrounded = Physics2D.OverlapCircle(groundCheck.position, radius, ground);
        bool wantToCrouch = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.LeftControl);

        if (isJumpingVar && jumpHeld && extraJumpTimer < maxExtraJumpTime)
        {
            float addForce = extraJumpForcePerSecond * Time.fixedDeltaTime;
            rb.AddForce(Vector2.up * addForce, ForceMode2D.Force);

            extraJumpTimer += Time.fixedDeltaTime;

            if (rb.linearVelocity.y <= 0f)
            {
                isJumpingVar = false;
            }
        }
        else
        {
            if (isGrounded)
            {
                isJumpingVar = false;
            }
        }

        if (!isGrounded && wantToCrouch)
        {
            if (rb.linearVelocity.y > 0.1f)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.25f);
                rb.AddForce(Vector2.down * fastFallImpulse, ForceMode2D.Impulse);
            }
            rb.gravityScale = originalGravityScale * fallGravityMultiplier;
        }
        else
        {
            rb.gravityScale = originalGravityScale;
        }
    }

    private void ApplyCrouchState(bool crouched)
    {
        if (standingCollider != null && crouchCollider != null)
        {
            if (crouched != lastCrouchState)
            {
                if (enableBottomSnap && bottomShift != 0f)
                {
                    if (crouched)
                        transform.position = new Vector3(transform.position.x, transform.position.y - bottomShift, transform.position.z);
                    else
                        transform.position = new Vector3(transform.position.x, transform.position.y + bottomShift, transform.position.z);
                }

                standingCollider.enabled = !crouched;
                crouchCollider.enabled = crouched;
                lastCrouchState = crouched;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (groundCheck != null) Gizmos.DrawWireSphere(groundCheck.position, radius);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;
        if (collision.gameObject.CompareTag("Obstacles") || collision.gameObject.CompareTag("Birds"))
        {
            isDead = true;
            GameManager.Instance.ShowGameOverScreen();
            animator?.SetTrigger("Die");
            Time.timeScale = 0f;
        }
    }
}
