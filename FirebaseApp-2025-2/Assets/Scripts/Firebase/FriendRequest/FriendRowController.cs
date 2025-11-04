using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FriendRowController : MonoBehaviour
{
    public TMP_Text usernameText;
    public Button unfriendButton;

    private string uid;
    private FriendsListController parent;

    public void Init(string uid, string username, FriendsListController parentController)
    {
        this.uid = uid;
        this.parent = parentController;
        if (usernameText != null) usernameText.text = username;
        if (unfriendButton != null)
        {
            unfriendButton.onClick.RemoveAllListeners();
            unfriendButton.onClick.AddListener(OnUnfriendClicked);
        }
    }

    private void OnUnfriendClicked()
    {
        parent.Unfriend(uid);
    }
}
