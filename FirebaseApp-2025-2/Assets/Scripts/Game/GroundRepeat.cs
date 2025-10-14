using UnityEngine;

public class GroundRepeat : MonoBehaviour
{
    private float spriteWidth;
    void Start()
    {
        BoxCollider2D groundCollider = GetComponent<BoxCollider2D>();
        spriteWidth = groundCollider.size.x;
    }

    void Update()
    {
        if(transform.position.x < -spriteWidth)
        {
            transform.Translate(Vector2.right * 2f * spriteWidth);

        }
    }
}
