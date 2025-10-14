using UnityEngine;

public class FloorScroll : MonoBehaviour
{
    private void Update()
    {
        transform.Translate(Vector2.left * GameManager.Instance.GetScrollSpeed() * Time.deltaTime);
    }
}
