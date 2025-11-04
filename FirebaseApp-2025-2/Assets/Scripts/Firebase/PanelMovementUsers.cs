using UnityEngine;
using DG.Tweening;

public class ThreePanelSlider : MonoBehaviour
{
    [Header("Paneles")]
    [SerializeField] private RectTransform friendsPanel;
    [SerializeField] private RectTransform onlinePanel;
    [SerializeField] private RectTransform matchPanel;

    [Header("Duración de transición")]
    [SerializeField] private float transitionDuration = 0.5f;

    private int currentIndex = 1; // 0 = Friends, 1 = Online, 2 = Match

    private void Start()
    {
        float width = Screen.width;

        // Posiciones iniciales
        friendsPanel.anchoredPosition = new Vector2(-width, 0);
        onlinePanel.anchoredPosition = Vector2.zero;
        matchPanel.anchoredPosition = new Vector2(width, 0);
    }

    private void MoveTo(int newIndex)
    {
        if (newIndex < 0 || newIndex > 2) return;

        currentIndex = newIndex;
        float width = Screen.width;

        // Matriz de posiciones objetivo según índice
        Vector2 friendsPos = new Vector2((0 - currentIndex) * width, 0);
        Vector2 onlinePos = new Vector2((1 - currentIndex) * width, 0);
        Vector2 matchPos = new Vector2((2 - currentIndex) * width, 0);

        // Animaciones
        friendsPanel.DOAnchorPos(friendsPos, transitionDuration).SetEase(Ease.InOutQuad);
        onlinePanel.DOAnchorPos(onlinePos, transitionDuration).SetEase(Ease.InOutQuad);
        matchPanel.DOAnchorPos(matchPos, transitionDuration).SetEase(Ease.InOutQuad);
    }

    // --- BOTONES ---
    public void GoToFriends() => MoveTo(0);
    public void GoToOnline() => MoveTo(1);
    public void GoToMatch() => MoveTo(2);
}
