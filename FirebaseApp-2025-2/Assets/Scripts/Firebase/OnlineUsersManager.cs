using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Auth;
using Firebase.Database;
using UnityEngine;

public class OnlineUsersManager : MonoBehaviour
{
    public Transform content;
    public GameObject userRowPrefab;
    public FriendRequestManager friendRequestManager; // Expuesto para asignar en el inspector o autolocalizar

    private DatabaseReference onlineRef;
    private string myUid;
    private Queue<Action> mainQueue = new Queue<Action>();
    private Dictionary<string, GameObject> rows = new Dictionary<string, GameObject>();
    private bool dbSubscribed = false;

    async void Start()
    {
        // intentar auto-asignar el gestor de solicitudes si no se ha asignado en el inspector
        if (friendRequestManager == null)
            friendRequestManager = UnityEngine.Object.FindAnyObjectByType<FriendRequestManager>();

        FirebaseAuth.DefaultInstance.StateChanged += OnAuthStateChanged;
        var current = FirebaseAuth.DefaultInstance.CurrentUser;
        if (current != null) await InitializeForUser(current.UserId);
    }

    private void OnAuthStateChanged(object sender, EventArgs e)
    {
        var current = FirebaseAuth.DefaultInstance.CurrentUser;
        if (current == null)
        {
            EnqueueOnMainThread(UnsubscribeFromOnline); // full cleanup on sign-out
            myUid = null;
            return;
        }
        if (dbSubscribed && myUid == current.UserId) return;
        _ = InitializeForUser(current.UserId);
    }

    private async Task<bool> InitializeForUser(string uid)
    {
        // if previously subscribed, detach listeners only (keep UI until explicit sign-out)
        if (dbSubscribed) EnqueueOnMainThread(() => UnsubscribeFromOnlineListeners()); // fixed: pass Action, not invoke
        myUid = uid;

        Debug.Log("[OnlineUsersManager] Inicializando para uid: " + myUid);

        // Log DB URL (verifica proyecto / instancia)
        try
        {
            var dbUrl = Firebase.FirebaseApp.DefaultInstance?.Options?.DatabaseUrl;
            Debug.Log("[OnlineUsersManager] DatabaseUrl: " + (dbUrl != null ? dbUrl.ToString() : "null"));
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[OnlineUsersManager] Error al leer DatabaseUrl: " + ex);
        }

        // --- CHANGED --- Asegurar token válido antes de subscribir
        var ok = await EnsureAuthToken();
        if (!ok)
        {
            Debug.LogWarning("[OnlineUsersManager] No token válido: abortando suscripción a users-online (manteniendo UI actual).");
            return false;
        }

        try
        {
            onlineRef = FirebaseDatabase.DefaultInstance.GetReference("users-online");
            onlineRef.ChildAdded += HandleChildAdded;
            onlineRef.ChildRemoved += HandleChildRemoved;
            dbSubscribed = true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[OnlineUsersManager] Subscribe failed: " + ex);
            dbSubscribed = false;
            return false;
        }

        try
        {
            var snap = await onlineRef.GetValueAsync();
            EnqueueOnMainThread(() =>
            {
                if (snap != null && snap.Exists)
                {
                    foreach (var c in snap.Children)
                    {
                        AddRowSafe(c.Key, c.Value != null ? c.Value.ToString() : "");
                    }
                }
            });
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[OnlineUsersManager] Initial load failed: " + ex);
            if (ex.Message?.ToLower().Contains("permission") == true) EnqueueOnMainThread(() => UnsubscribeFromOnlineListeners()); // --- CHANGED ---
            return false;
        }
    }

    private void HandleChildAdded(object sender, ChildChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError("This client does not have permission to perform this operation.\n" + args.DatabaseError.Message);
            if (args.DatabaseError.Message?.ToLower().Contains("permission") == true)
            {
                Debug.LogWarning("[OnlineUsersManager] Permission denied on users-online - unsubscribing listeners only.");
                EnqueueOnMainThread(() => UnsubscribeFromOnlineListeners()); // --- CHANGED ---
            }
            return;
        }
        var snap = args.Snapshot;
        EnqueueOnMainThread(() =>
        {
            if (snap == null || !snap.Exists) return;
            AddRowSafe(snap.Key, snap.Value != null ? snap.Value.ToString() : "");
        });
    }

    private void HandleChildRemoved(object sender, ChildChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError("This client does not have permission to perform this operation.\n" + args.DatabaseError.Message);
            if (args.DatabaseError.Message?.ToLower().Contains("permission") == true)
            {
                Debug.LogWarning("[OnlineUsersManager] Permission denied on users-online (remove) - unsubscribing listeners only.");
                EnqueueOnMainThread(() => UnsubscribeFromOnlineListeners()); // --- CHANGED ---
            }
            return;
        }
        var snap = args.Snapshot;
        EnqueueOnMainThread(() =>
        {
            if (snap == null || !snap.Exists) return;
            RemoveRowSafe(snap.Key);
        });
    }

    private void AddRowSafe(string uid, string username)
    {
        if (string.IsNullOrEmpty(uid) || rows.ContainsKey(uid)) return;
        if (userRowPrefab == null || content == null) { Debug.LogWarning("[OnlineUsersManager] prefab/content missing"); return; }

        var go = Instantiate(userRowPrefab);
        go.name = "UserRow_" + uid;
        go.transform.SetParent(content, false);
        rows[uid] = go;

        var ctrl = go.GetComponent<UserRowController>();
        if (ctrl != null)
        {
            try
            {
                // Asegurar friendRequestManager antes de inicializar la fila
                if (friendRequestManager == null)
                    friendRequestManager = UnityEngine.Object.FindAnyObjectByType<FriendRequestManager>();

                bool self = (uid == myUid);
                bool friend = false; // Ajusta según tu lógica de amigos
                bool pending = false; // Ajusta según tu lógica de solicitudes pendientes
                ctrl.Init(uid, username, friendRequestManager, self, friend, pending);
            }
            catch (Exception ex) { Debug.LogError("[OnlineUsersManager] Init row failed: " + ex); }
        }
    }

    private void RemoveRowSafe(string uid)
    {
        if (string.IsNullOrEmpty(uid) || !rows.ContainsKey(uid)) return;
        var go = rows[uid];
        rows.Remove(uid);
        if (go != null) Destroy(go);
    }

    void Update()
    {
        lock (mainQueue)
        {
            while (mainQueue.Count > 0)
            {
                try { mainQueue.Dequeue()?.Invoke(); }
                catch (Exception ex) { Debug.LogError("Exception en mainThreadAction: " + ex); }
            }
        }
    }

    private void EnqueueOnMainThread(Action a) { lock (mainQueue) mainQueue.Enqueue(a); }

    // --- CHANGED: unsubscribe listeners only (no UI clear) ---
    private void UnsubscribeFromOnlineListeners()
    {
        try
        {
            if (onlineRef != null)
            {
                onlineRef.ChildAdded -= HandleChildAdded;
                onlineRef.ChildRemoved -= HandleChildRemoved;
            }
        }
        catch (Exception ex) { Debug.LogWarning("[OnlineUsersManager] Unsubscribe listeners error: " + ex); }
        dbSubscribed = false;
        onlineRef = null;
    }

    private void UnsubscribeFromOnline()
    {
        // full cleanup: listeners + clear UI
        UnsubscribeFromOnlineListeners();

        foreach (var kv in rows.Values) if (kv != null) Destroy(kv);
        rows.Clear();
    }

    private void OnDestroy()
    {
        EnqueueOnMainThread(UnsubscribeFromOnline);
        FirebaseAuth.DefaultInstance.StateChanged -= OnAuthStateChanged;
    }

    // --- CHANGED: EnsureAuthToken helper (copiar igual en otros scripts) ---
    private async Task<bool> EnsureAuthToken()
    {
        try
        {
            var user = FirebaseAuth.DefaultInstance.CurrentUser;
            if (user == null)
            {
                Debug.LogWarning("[OnlineUsersManager] EnsureAuthToken: no hay usuario autenticado.");
                return false;
            }
            var token = await user.TokenAsync(false);
            Debug.Log("[OnlineUsersManager] Token obtenido (len): " + (token?.Length ?? 0));
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[OnlineUsersManager] Token fetch failed: " + ex + " - intentando refresh");
            try
            {
                var user2 = FirebaseAuth.DefaultInstance.CurrentUser;
                if (user2 == null) return false;
                var token2 = await user2.TokenAsync(true);
                Debug.Log("[OnlineUsersManager] Token refresh OK (len): " + (token2?.Length ?? 0));
                return true;
            }
            catch (Exception ex2)
            {
                Debug.LogWarning("[OnlineUsersManager] Token refresh failed: " + ex2);
                return false;
            }
        }
    }
}
