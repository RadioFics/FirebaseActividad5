using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RequestRowController : MonoBehaviour
{
    public TMP_Text usernameText;
    public Button acceptButton;
    public Button rejectButton;

    private string uid;
    private string username;
    private FriendRequestManager friendRequestManager;
    private InboxController parentController;

    public void Init(string uid, string username, FriendRequestManager frm, InboxController parent)
    {
        this.uid = uid;
        this.username = username;
        this.friendRequestManager = frm;
        this.parentController = parent;

        try
        {
            if (usernameText != null) usernameText.text = !string.IsNullOrEmpty(username) ? username : uid;
            if (acceptButton != null)
            {
                acceptButton.onClick.RemoveAllListeners();
                acceptButton.onClick.AddListener(OnAcceptClicked);
                acceptButton.gameObject.SetActive(true);
                acceptButton.interactable = true;
            }
            if (rejectButton != null)
            {
                rejectButton.onClick.RemoveAllListeners();
                rejectButton.onClick.AddListener(OnRejectClicked);
                rejectButton.gameObject.SetActive(true);
                rejectButton.interactable = true;
            }
        }
        catch (Exception ex) { Debug.LogError("[RequestRowController] Init exception: " + ex); }
    }

    private async void OnAcceptClicked()
    {
        if (friendRequestManager == null) { Debug.LogWarning("FriendRequestManager no asignado"); return; }
        await friendRequestManager.RespondFriendRequestAsync(uid, username, 1);
        parentController.OnResponded(uid);
    }

    private async void OnRejectClicked()
    {
        if (friendRequestManager == null) { Debug.LogWarning("FriendRequestManager no asignado"); return; }
        await friendRequestManager.RespondFriendRequestAsync(uid, username, 2);
        parentController.OnResponded(uid);
    }
}
