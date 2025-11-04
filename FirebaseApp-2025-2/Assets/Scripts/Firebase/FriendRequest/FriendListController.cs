using Firebase.Auth;
using Firebase.Database;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;

public class FriendsListController : MonoBehaviour
{
    public Transform content;
    public GameObject friendRowPrefab;
    public TMP_Text counterText;

    private string myUid;
    private DatabaseReference friendsRef;
    private Queue<Action> mainQueue = new Queue<Action>();
    private Dictionary<string, GameObject> rows = new Dictionary<string, GameObject>();
    private bool dbSubscribed = false;

    async void Start()
    {
        var cur = FirebaseAuth.DefaultInstance.CurrentUser;
        if (cur == null) return;
        myUid = cur.UserId;

        if (friendRowPrefab == null || content == null)
        {
            Debug.LogWarning("[FriendsListController] friendRowPrefab o content no asignados en Inspector.");
            return;
        }

        // Debug: verificar la instancia de DB a la que apunta la app
        try
        {
            var dbUrl = Firebase.FirebaseApp.DefaultInstance?.Options?.DatabaseUrl;
            Debug.Log("[FriendsListController] DatabaseUrl: " + (dbUrl != null ? dbUrl.ToString() : "null"));
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[FriendsListController] Error al leer DatabaseUrl: " + ex);
        }

        // Asegurarse de que hay token válido antes de subscribir
        var ok = await EnsureAuthToken();
        if (!ok)
        {
            Debug.LogWarning("[FriendsListController] No hay token de auth válido: abortando suscripción a friends.");
            return;
        }

        try
        {
            friendsRef = FirebaseDatabase.DefaultInstance.GetReference($"users/{myUid}/friends");
            friendsRef.ChildAdded += HandleChildAdded;
            friendsRef.ChildRemoved += HandleChildRemoved;
            dbSubscribed = true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[FriendsListController] Subscribe failed: " + ex);
            dbSubscribed = false;
            return;
        }

        try
        {
            var snap = await friendsRef.GetValueAsync();
            EnqueueOnMainThread(() =>
            {
                if (snap != null && snap.Exists)
                {
                    foreach (var c in snap.Children)
                    {
                        AddRow(c.Key, c.Value != null ? c.Value.ToString() : "");
                    }
                }
                UpdateCounter();
            });
        }
        catch (Exception ex) { Debug.LogWarning("Friends load: " + ex); }
    }

    void Update()
    {
        lock (mainQueue)
        {
            while (mainQueue.Count > 0) mainQueue.Dequeue()?.Invoke();
        }
    }

    private void EnqueueOnMainThread(Action a) { lock (mainQueue) mainQueue.Enqueue(a); }

    private void HandleChildAdded(object s, ChildChangedEventArgs a)
    {
        if (a.DatabaseError != null)
        {
            Debug.LogError("[FriendsListController] DB Error: " + a.DatabaseError.Message);
            if (a.DatabaseError.Message?.ToLower().Contains("permission") == true)
            {
                EnqueueOnMainThread(() =>
                {
                    Debug.LogWarning("[FriendsListController] Permission denied: unsubscribing to avoid spam and will require reinit.");
                    UnsubscribeFromFriends();
                });
            }
            return;
        }
        EnqueueOnMainThread(() =>
        {
            if (!IsAuthorizedForFriends())
            {
                Debug.LogWarning("[FriendsListController] Evento recibido pero no autorizado para este usuario; ignorando.");
                return;
            }
            if (a.Snapshot == null) return;
            AddRow(a.Snapshot.Key, a.Snapshot.Value != null ? a.Snapshot.Value.ToString() : "");
        });
    }

    private void HandleChildRemoved(object s, ChildChangedEventArgs a)
    {
        if (a.DatabaseError != null)
        {
            Debug.LogError("[FriendsListController] DB Error: " + a.DatabaseError.Message);
            if (a.DatabaseError.Message?.ToLower().Contains("permission") == true)
            {
                EnqueueOnMainThread(() =>
                {
                    Debug.LogWarning("[FriendsListController] Permission denied on remove: unsubscribing.");
                    UnsubscribeFromFriends();
                });
            }
            return;
        }
        EnqueueOnMainThread(() =>
        {
            if (!IsAuthorizedForFriends())
            {
                Debug.LogWarning("[FriendsListController] Evento Remove recibido pero no autorizado; ignorando.");
                return;
            }
            RemoveRow(a.Snapshot.Key);
        });
    }

    private void AddRow(string uid, string username)
    {
        if (rows.ContainsKey(uid)) return;
        if (friendRowPrefab == null || content == null) return;
        var go = Instantiate(friendRowPrefab, content);
        go.name = "FriendRow_" + uid;
        var ctrl = go.GetComponent<FriendRowController>();
        if (ctrl != null) ctrl.Init(uid, username, this);
        rows[uid] = go;
        UpdateCounter();
        try { LayoutRebuilder.ForceRebuildLayoutImmediate(content as RectTransform); } catch { }
    }

    public void RemoveRow(string uid)
    {
        if (!rows.ContainsKey(uid)) return;
        Destroy(rows[uid]);
        rows.Remove(uid);
        UpdateCounter();
    }

    private void UpdateCounter()
    {
        if (counterText == null) return;
        counterText.text = $"Amigos ({rows.Count})";
    }

    public void Unfriend(string otherUid)
    {
        if (string.IsNullOrEmpty(myUid) || string.IsNullOrEmpty(otherUid)) return;
        FirebaseDatabase.DefaultInstance.GetReference($"users/{myUid}/friends/{otherUid}").SetValueAsync(null);
        FirebaseDatabase.DefaultInstance.GetReference($"users/{otherUid}/friends/{myUid}").SetValueAsync(null);
    }

    private void OnDestroy()
    {
        UnsubscribeFromFriends();
    }

    private bool IsAuthorizedForFriends()
    {
        return FirebaseAuth.DefaultInstance.CurrentUser != null && myUid != null && FirebaseAuth.DefaultInstance.CurrentUser.UserId == myUid;
    }

    private void UnsubscribeFromFriends()
    {
        try
        {
            if (friendsRef != null && dbSubscribed)
            {
                friendsRef.ChildAdded -= HandleChildAdded;
                friendsRef.ChildRemoved -= HandleChildRemoved;
            }
        }
        catch (Exception ex) { Debug.LogWarning("[FriendsListController] Unsubscribe error: " + ex); }
        dbSubscribed = false;
        friendsRef = null;

        foreach (var kv in rows.Values) if (kv != null) Destroy(kv);
        rows.Clear();
        UpdateCounter();
    }

    private async Task<bool> EnsureAuthToken()
    {
        try
        {
            var user = FirebaseAuth.DefaultInstance.CurrentUser;
            if (user == null) return false;
            var token = await user.TokenAsync(false);
            Debug.Log("[FriendsListController] Token obtenido (len): " + (token?.Length ?? 0));
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[FriendsListController] fallo al obtener token, intentando refresh: " + ex);
            try
            {
                var user2 = FirebaseAuth.DefaultInstance.CurrentUser;
                if (user2 == null) return false;
                var token2 = await user2.TokenAsync(true);
                Debug.Log("[FriendsListController] Token refresh OK (len): " + (token2?.Length ?? 0));
                return true;
            }
            catch (Exception ex2)
            {
                Debug.LogWarning("[FriendsListController] Token refresh falló: " + ex2);
                return false;
            }
        }
    }
}