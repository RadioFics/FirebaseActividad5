using Firebase.Database;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ButtonSendFriendRequest : MonoBehaviour
{
    [SerializeField] private Button _sendFriendRequestButton;

    [SerializeField] private TMP_InputField _friendUserIdInputField;
    [SerializeField] private DatabaseReference mDatabaseRef;
    [SerializeField] private FriendRequestManager _friendRequestManager;

    [SerializeField] private int _responseStatus; // 1 = accepted, 2 = rejected

    void Reset()
    {
        _sendFriendRequestButton = GetComponent<Button>();
        _friendUserIdInputField = GameObject.Find("InputFieldFriendRequest").GetComponent<TMP_InputField>();
        _friendRequestManager = GameObject.Find("FirebaseController").GetComponent<FriendRequestManager>();
    }

    void Start()
    {
        _sendFriendRequestButton.onClick.AddListener(HandleSendFriendRequestClicked);
        mDatabaseRef = FirebaseDatabase.DefaultInstance.RootReference;
    }

    private async void HandleSendFriendRequestClicked()
    {
        string friendUserId = _friendUserIdInputField.text;
        string friendUsername = await GetUsernameByUid(friendUserId);

        await _friendRequestManager.SendFriendRequest(friendUserId, friendUsername);
    }

    private async Task<string> GetUsernameByUid(string uid)
    {
        Debug.Log("Ruta de la base de datos: users/"+uid+"/username");
        var userSnapshot = await mDatabaseRef.Child("users").Child(uid).Child("username").GetValueAsync();

        if (userSnapshot == null || !userSnapshot.Exists)
        {
            Debug.LogError("No se encontró el usuario con UID: " + uid);
            return null;
        }
        return userSnapshot.Value.ToString();
    }
}
