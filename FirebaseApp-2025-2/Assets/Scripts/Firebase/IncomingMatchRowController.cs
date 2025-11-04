using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class IncomingMatchRowController : MonoBehaviour
{
    public TMP_Text usernameText;
    public Button acceptButton;
    public Button rejectButton;

    private string senderUid;
    private string senderName;
    private MatchmakingManager parent;

    public void Init(string senderUid, string senderName, MatchmakingManager parent)
    {
        this.senderUid = senderUid;
        this.senderName = senderName;
        this.parent = parent;
        if (usernameText != null) usernameText.text = !string.IsNullOrEmpty(senderName) ? senderName : ShortUid(senderUid);
        if (acceptButton != null)
        {
            acceptButton.onClick.RemoveAllListeners();
            acceptButton.onClick.AddListener(OnAccept);
        }
        if (rejectButton != null)
        {
            rejectButton.onClick.RemoveAllListeners();
            rejectButton.onClick.AddListener(OnReject);
        }
    }

    private void OnAccept()
    {
        parent?.RespondMatchRequest(senderUid, true);
        Destroy(gameObject);
    }

    private void OnReject()
    {
        parent?.RespondMatchRequest(senderUid, false);
        Destroy(gameObject);
    }

    private string ShortUid(string u)
    {
        if (string.IsNullOrEmpty(u)) return "sin-id";
        return u.Length > 8 ? u.Substring(0, 8) + "..." : u;
    }
}
