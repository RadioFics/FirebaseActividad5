using Firebase.Auth;
using Firebase.Database;
using System;
using System.Threading.Tasks;
using UnityEngine;

public class FriendRequestManager : MonoBehaviour
{
    private DatabaseReference mDatabaseUsersRef;
    private string myUsername;
    private string myUserId;

    private const string inboxRef = "friendRequests/inbox";
    private const string outboxRef = "friendRequests/outbox";

    // eventos públicos que UI puede suscribirse
    public event Action<string, string> OnInboxRequestAdded;
    public event Action<string> OnInboxRequestRemoved;

    async void Start()
    {
        mDatabaseUsersRef = FirebaseDatabase.DefaultInstance.GetReference("users");

        var current = FirebaseAuth.DefaultInstance.CurrentUser;
        if (current == null)
        {
            Debug.Log("[FriendRequestManager] No auth aún. Esperando StateChanged...");
            FirebaseAuth.DefaultInstance.StateChanged += OnAuthStateChanged;
            return;
        }

        await InitializeForUser(current.UserId);
    }

    private void OnAuthStateChanged(object sender, EventArgs e)
    {
        var current = FirebaseAuth.DefaultInstance.CurrentUser;
        if (current == null) return;
        FirebaseAuth.DefaultInstance.StateChanged -= OnAuthStateChanged;
        _ = InitializeForUser(current.UserId);
    }

    private async Task InitializeForUser(string uid)
    {
        try
        {
            myUserId = uid;
            myUsername = await GetUsername();

            Debug.Log("[FriendRequestManager] Inicializado para userId=" + myUserId + " username=" + myUsername);

            var inboxDatabaseRef = mDatabaseUsersRef.Child(myUserId).Child(inboxRef);
            var outboxDatabaseRef = mDatabaseUsersRef.Child(myUserId).Child(outboxRef);

            inboxDatabaseRef.ChildAdded += HandleFriendRequestAdded;
            inboxDatabaseRef.ChildRemoved += HandleFriendRequestRemoved;

            outboxDatabaseRef.ChildAdded += HandleFriendResponseAdded;
            outboxDatabaseRef.ChildChanged += HandleFriendResponseChanged;
            outboxDatabaseRef.ChildRemoved += HandleFriendResponseRemoved;
        }
        catch (Exception ex)
        {
            Debug.LogError("[FriendRequestManager] InitializeForUser failed: " + ex);
        }
    }

    public async Task<bool> SendFriendRequest(string friendUserId, string friendUsername)
    {
        try
        {
            // escribir inbox del receptor (string username)
            await mDatabaseUsersRef.Child(friendUserId).Child(inboxRef).Child(myUserId).SetValueAsync(myUsername);
            Debug.Log("Inbox escrita para receptor: " + friendUserId);

            // preparar outbox JSON
            string friendRequestJson = JsonUtility.ToJson(new FriendResponse
            {
                username = myUsername,
                status = 0 // pending
            });

            // escribir outbox del emisor
            await mDatabaseUsersRef.Child(myUserId).Child(outboxRef).Child(friendUserId).SetRawJsonValueAsync(friendRequestJson);
            Debug.Log("Outbox escrita para " + friendUserId);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"SendFriendRequest fallo para {friendUserId}: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }

    public async Task RespondFriendRequestAsync(string friendUserId, string friendUserName, int ResponseStatus)
    {
        try
        {
            // asegurar myUserId y referencia DB
            if (string.IsNullOrEmpty(myUserId))
            {
                var cur = FirebaseAuth.DefaultInstance.CurrentUser;
                if (cur == null)
                {
                    Debug.LogWarning("RespondFriendRequestAsync: no hay usuario autenticado.");
                    return;
                }
                myUserId = cur.UserId;
            }
            if (mDatabaseUsersRef == null) mDatabaseUsersRef = FirebaseDatabase.DefaultInstance.GetReference("users");

            // NOTA: con las reglas actuales el cliente NO puede escribir en el nodo de otro usuario.
            // Intentar actualizar users/{friendUserId}/... desde la cuenta del receptor causará permiso denegado.
            // Por eso NO intentamos actualizar el outbox del emisor desde el cliente.
            Debug.Log($"RespondFriendRequestAsync: skipping client-side write to outbox of {friendUserId} due to DB rules. Use a server-side function to notify the sender if needed.");

            // si se acepta, guardar el amigo en la cuenta del receptor (operación permitida)
            if (ResponseStatus == 1)
            {
                SaveFriend(friendUserId, friendUserName);
                Debug.Log($"RespondFriendRequestAsync: agregando {friendUserId} a mis friends");
            }

            // eliminar la solicitud del inbox mio (operación permitida por las reglas)
            await mDatabaseUsersRef
                .Child(myUserId)
                .Child(inboxRef)
                .Child(friendUserId)
                .SetValueAsync(null);

            Debug.Log($"RespondFriendRequestAsync: eliminado inbox/{friendUserId} en mi cuenta");

            // notificar listeners locales
            try { OnInboxRequestRemoved?.Invoke(friendUserId); } catch { }
        }
        catch (Exception ex)
        {
            Debug.LogError($"RespondFriendRequestAsync fallo al responder solicitud ({friendUserId}): {ex.Message}\n{ex.StackTrace}");
        }
    }

    public void RespondFriendRequest(string friendUserId, string friendUsername, int ResponseStatus)
    {
        _ = RespondFriendRequestAsync(friendUserId, friendUsername, ResponseStatus);
    }

    private void SaveFriend(string friendUserId, string friendUsername)
    {
        if (string.IsNullOrEmpty(myUserId)) return;
        mDatabaseUsersRef.Child(myUserId).Child("friends").Child(friendUserId).SetValueAsync(friendUsername);
    }

    private async Task<string> GetUsername()
    {
        try
        {
            if (string.IsNullOrEmpty(myUserId)) return "";
            var snap = await FirebaseDatabase.DefaultInstance
               .GetReference("users/" + myUserId + "/username")
               .GetValueAsync();
            if (snap != null && snap.Exists) return snap.Value != null ? snap.Value.ToString() : "";
        }
        catch (Exception ex) { Debug.LogWarning("[FriendRequestManager] GetUsername: " + ex); }
        return "";
    }

    // Outbox handlers
    private void HandleFriendResponseChanged(object sender, ChildChangedEventArgs args)
    {
        if (args.DatabaseError != null) { Debug.LogError(args.DatabaseError.Message); return; }
        var friendResponse = GetFriendResponseFromSnapshot(args.Snapshot);
        ProcessFriendResponse(friendResponse);
    }

    private void HandleFriendResponseAdded(object sender, ChildChangedEventArgs args)
    {
        if (args.DatabaseError != null) { Debug.LogError(args.DatabaseError.Message); return; }
        var friendResponse = GetFriendResponseFromSnapshot(args.Snapshot);
        if (friendResponse == null) return;
        if (friendResponse.status == 0)
        {
            Debug.Log("Friend request to " + friendResponse.username + " is still pending.");
            return;
        }
        ProcessFriendResponse(friendResponse);
    }

    private void HandleFriendResponseRemoved(object sender, ChildChangedEventArgs args) { /* opcional */ }

    private FriendResponse GetFriendResponseFromSnapshot(DataSnapshot snapshot)
    {
        if (!snapshot.Exists) return null;
        var friendRequest = JsonUtility.FromJson<FriendResponse>(snapshot.GetRawJsonValue());
        friendRequest.userId = snapshot.Key;
        return friendRequest;
    }

    private void ProcessFriendResponse(FriendResponse friendResponse)
    {
        if (friendResponse == null) return;
        if (friendResponse.status == 0) return;
        if (friendResponse.status == 1)
        {
            Debug.Log(" your friend request to " + friendResponse.username + " has been accepted.");
            SaveFriend(friendResponse.userId, friendResponse.username);
        }
        else if (friendResponse.status == 2)
        {
            Debug.Log(" your friend request to " + friendResponse.username + " has been rejected.");
        }
        if (!string.IsNullOrEmpty(myUserId) && friendResponse.userId != null)
            mDatabaseUsersRef.Child(myUserId).Child(outboxRef).Child(friendResponse.userId).SetValueAsync(null);
    }

    // Inbox handlers
    private void HandleFriendRequestAdded(object sender, ChildChangedEventArgs args)
    {
        if (args.DatabaseError != null) { Debug.LogError(args.DatabaseError.Message); return; }
        if (!args.Snapshot.Exists) return;
        var friendUserId = args.Snapshot.Key;
        var friendUsername = args.Snapshot.Value != null ? args.Snapshot.Value.ToString() : "";
        Debug.Log("Friend request from " + friendUsername + ", userId " + friendUserId);
        try { OnInboxRequestAdded?.Invoke(friendUserId, friendUsername); }
        catch (Exception ex) { Debug.LogWarning("OnInboxRequestAdded threw: " + ex); }
    }

    private void HandleFriendRequestRemoved(object sender, ChildChangedEventArgs e)
    {
        if (e.DatabaseError != null) { Debug.LogError(e.DatabaseError.Message); return; }
        var removedUid = e.Snapshot.Key;
        Debug.Log("Friend request removed for: " + removedUid);
        try { OnInboxRequestRemoved?.Invoke(removedUid); }
        catch (Exception ex) { Debug.LogWarning("OnInboxRequestRemoved threw: " + ex); }
    }
}

[Serializable]
public class FriendResponse
{
    public string userId;
    public string username;
    public int status; // 0 = pending, 1 = accepted, 2 = rejected
}
