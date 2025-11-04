using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MatchRowController : MonoBehaviour
{
    public TMP_Text usernameText;
    public Button challengeButton;

    private string uid;
    private string username;
    private MatchmakingManager parent;

    public void Init(string uid, string username, MatchmakingManager parent)
    {
        this.uid = uid;
        this.username = username;
        this.parent = parent;
        if (usernameText != null) usernameText.text = !string.IsNullOrEmpty(username) ? username : ShortUid(uid);
        if (challengeButton != null)
        {
            challengeButton.onClick.RemoveAllListeners();
            challengeButton.onClick.AddListener(OnChallengeClicked);
        }
    }

    private void OnChallengeClicked()
    {
        if (parent != null && !string.IsNullOrEmpty(uid))
        {
            challengeButton.interactable = false;
            parent.SendMatchRequest(uid, username);
            // opcional: reactivar tras timeout o actualizar por outbox listener
        }
    }

    private string ShortUid(string u)
    {
        if (string.IsNullOrEmpty(u)) return "sin-id";
        return u.Length > 8 ? u.Substring(0, 8) + "..." : u;
    }
}
