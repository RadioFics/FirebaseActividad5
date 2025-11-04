using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Firebase.Database;

public class SentRequestRowController : MonoBehaviour
{
    public TMP_Text usernameText;
    public Button cancelButton;

    private string targetUid;

    public void Init(string uid, string username)
    {
        targetUid = uid;
        // si username vacío o igual al uid fallback, mostramos uid corto
        if (string.IsNullOrEmpty(username))
            username = ShortUidLabel(uid);

        if (usernameText != null) usernameText.text = username;

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(OnCancelClicked);
            cancelButton.gameObject.SetActive(true);
            cancelButton.interactable = true;
        }
    }

    private async void OnCancelClicked()
    {
        var cur = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser;
        var myUid = cur != null ? cur.UserId : null;
        if (string.IsNullOrEmpty(myUid) || string.IsNullOrEmpty(targetUid))
        {
            Debug.LogWarning("[SentRequestRowController] Cancel: missing uid(s).");
            return;
        }

        if (cancelButton != null) { cancelButton.interactable = false; }

        try
        {
            await FirebaseDatabase.DefaultInstance.GetReference($"users/{myUid}/friendRequests/outbox/{targetUid}").SetValueAsync(null);
            await FirebaseDatabase.DefaultInstance.GetReference($"users/{targetUid}/friendRequests/inbox/{myUid}").SetValueAsync(null);
            Debug.Log($"[SentRequestRowController] Cancelled request to {targetUid}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[SentRequestRowController] Cancel failed: " + ex);
            if (cancelButton != null) cancelButton.interactable = true;
            return;
        }

        Destroy(gameObject);
    }

    public void UpdateUsername(string username)
    {
        if (string.IsNullOrEmpty(username))
            username = ShortUidLabel(targetUid);
        if (usernameText != null) usernameText.text = username;
    }

    private string ShortUidLabel(string uid)
    {
        if (string.IsNullOrEmpty(uid)) return "sin-id";
        return uid.Length > 8 ? uid.Substring(0, 8) + "..." : uid;
    }
}

