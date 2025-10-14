using UnityEngine;
using DG.Tweening;

public class PanelMovement : MonoBehaviour
{
    [Header("Paneles")]
    [SerializeField] private RectTransform loginPanel;
    [SerializeField] private RectTransform registerPanel;
    [SerializeField] private RectTransform forgotPasswordPanel;
    [SerializeField] private RectTransform leaderboardPanel;

    [Header("Duración de transición")]
    [SerializeField] private float transitionDuration = 0.5f;

    private RectTransform currentPanel;
    private bool isTransitioning = false;

    private void Start()
    {
        currentPanel = loginPanel;

        loginPanel.gameObject.SetActive(true);
        registerPanel.gameObject.SetActive(false);
        forgotPasswordPanel.gameObject.SetActive(false);
        leaderboardPanel.gameObject.SetActive(false);

        loginPanel.anchoredPosition = Vector2.zero;
    }

    private void TransitionTo(RectTransform targetPanel)
    {
        if (targetPanel == currentPanel) return;
        if (isTransitioning) return;

        isTransitioning = true;

        RectTransform prevPanel = currentPanel;

        targetPanel.gameObject.SetActive(true);

        prevPanel.DOKill();
        targetPanel.DOKill();

        Vector2 offScreenRight = new Vector2(Screen.width, 0);
        Vector2 offScreenLeft = new Vector2(-Screen.width, 0);

        bool movingForward = (targetPanel == registerPanel ||
                              targetPanel == forgotPasswordPanel ||
                              targetPanel == leaderboardPanel);

        Vector2 exitPos = movingForward ? offScreenLeft : offScreenRight;
        Vector2 entryPos = movingForward ? offScreenRight : offScreenLeft;

        targetPanel.anchoredPosition = entryPos;

        prevPanel.DOAnchorPos(exitPos, transitionDuration)
            .SetEase(Ease.InOutQuad)
            .OnComplete(() =>
            {
                prevPanel.gameObject.SetActive(false);
            });

        targetPanel.DOAnchorPos(Vector2.zero, transitionDuration)
            .SetEase(Ease.InOutQuad)
            .OnComplete(() =>
            {
                currentPanel = targetPanel;
                isTransitioning = false;
            });
    }
    public void GoToRegisterPanel() => TransitionTo(registerPanel);
    public void GoToLoginPanel() => TransitionTo(loginPanel);
    public void GoToForgotPasswordPanel() => TransitionTo(forgotPasswordPanel);
    public void GoToLeaderboardPanel() => TransitionTo(leaderboardPanel);
}
