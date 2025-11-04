using UnityEngine;

public class FriendsUIManager : MonoBehaviour
{
    public GameObject inboxPanel;
    public GameObject outboxPanel;
    public GameObject friendsListPanel;

    void Start()
    {
        ShowInbox();
    }

    public void ShowInbox()
    {
        inboxPanel.SetActive(true);
        outboxPanel.SetActive(false);
        friendsListPanel.SetActive(false);
    }

    public void ShowOutbox()
    {
        inboxPanel.SetActive(false);
        outboxPanel.SetActive(true);
        friendsListPanel.SetActive(false);
    }

    public void ShowFriends()
    {
        inboxPanel.SetActive(false);
        outboxPanel.SetActive(false);
        friendsListPanel.SetActive(true);
    }
}
