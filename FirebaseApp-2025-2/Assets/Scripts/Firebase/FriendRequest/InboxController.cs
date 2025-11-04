using Firebase.Auth;
using Firebase.Database;
using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Threading.Tasks;

public class InboxController : MonoBehaviour
{
    public Transform content; // Content del ScrollView
    public GameObject requestRowPrefab;
    public TMP_Text counterText;
    public FriendRequestManager friendRequestManager; // asignar en inspector o buscar

    private DatabaseReference inboxRef;
    private string myUid;
    private Queue<Action> mainQueue = new Queue<Action>();
    private Dictionary<string, GameObject> rows = new Dictionary<string, GameObject>();
    private bool dbSubscribed = false;

    async void Start()
    {
        // Siempre escuchar cambios de auth (para detectar sign-in y sign-out)
        FirebaseAuth.DefaultInstance.StateChanged += OnAuthStateChanged;

        var current = FirebaseAuth.DefaultInstance.CurrentUser;
        if (current != null)
        {
            Debug.Log("[InboxController] Start: usuario autenticado: " + current.UserId);
            await InitializeForUser(current.UserId);
        }
        else
        {
            Debug.Log("[InboxController] No hay auth aún. Esperando StateChanged...");
        }

        if (friendRequestManager == null)
        {
            var go = GameObject.Find("FirebaseController");
            if (go != null) friendRequestManager = go.GetComponent<FriendRequestManager>();
            else
            {
                var found = UnityEngine.Object.FindFirstObjectByType<FriendRequestManager>();
                if (found != null) friendRequestManager = found;
            }
        }

        if (friendRequestManager != null)
        {
            friendRequestManager.OnInboxRequestAdded += OnInboxRequestAddedFallback;
            friendRequestManager.OnInboxRequestRemoved += OnInboxRequestRemovedFallback;
        }
    }

    private void OnAuthStateChanged(object sender, System.EventArgs e)
    {
        var current = FirebaseAuth.DefaultInstance.CurrentUser;

        // Usuario desconectado -> limpiar listeners y UI
        if (current == null)
        {
            Debug.Log("[InboxController] Usuario desconectado. Limpiando listeners DB y filas UI.");
            EnqueueOnMainThread(() => UnsubscribeFromInbox()); // full cleanup on real sign-out
            myUid = null;
            return;
        }

        // Si ya estamos inicializados para este usuario, ignorar
        if (dbSubscribed && myUid == current.UserId) return;

        // Nuevo usuario autenticado -> inicializar
        _ = InitializeForUser(current.UserId);
    }

    private async Task<bool> InitializeForUser(string uid)
    {
        // Si teníamos listeners para otro usuario, limpiarlos primero (solo listeners)
        if (dbSubscribed)
        {
            EnqueueOnMainThread(() => UnsubscribeFromInboxListeners()); // --- CHANGED ---
        }

        myUid = uid;
        Debug.Log("[InboxController] Inicializando inbox para uid: " + myUid);

        // Mostrar DB URL para verificar que la app apunta al proyecto esperado
        try
        {
            var dbUrl = Firebase.FirebaseApp.DefaultInstance?.Options?.DatabaseUrl;
            Debug.Log("[InboxController] DatabaseUrl: " + (dbUrl != null ? dbUrl.ToString() : "null"));
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[InboxController] Error al leer DatabaseUrl: " + ex);
        }

        // --- CHANGED ---: Asegurarse de que el token de auth esté disponible/actualizado antes de subscribir
        var ok = await EnsureAuthToken();
        if (!ok)
        {
            Debug.LogWarning("[InboxController] No token válido: abortando suscripción a inbox (me quedo con UI actual).");
            return false;
        }

        if (friendRequestManager == null)
        {
            var go = GameObject.Find("FirebaseController");
            if (go != null) friendRequestManager = go.GetComponent<FriendRequestManager>();
            else
            {
                var found = UnityEngine.Object.FindFirstObjectByType<FriendRequestManager>();
                if (found != null) friendRequestManager = found;
            }

            if (friendRequestManager != null)
            {
                friendRequestManager.OnInboxRequestAdded += OnInboxRequestAddedFallback;
                friendRequestManager.OnInboxRequestRemoved += OnInboxRequestRemovedFallback;
            }
        }

        inboxRef = FirebaseDatabase.DefaultInstance.GetReference($"users/{myUid}/friendRequests/inbox");

        if (!dbSubscribed)
        {
            try
            {
                inboxRef.ChildAdded += HandleChildAdded;
                inboxRef.ChildRemoved += HandleChildRemoved;
                dbSubscribed = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InboxController] No se pudo subscribir a inboxRef: " + ex);
                dbSubscribed = false;
                return false;
            }
        }

        // cargar estado actual
        try
        {
            var snap = await inboxRef.GetValueAsync();
            EnqueueOnMainThread(() =>
            {
                // Si ya no estamos autenticados o el uid cambió, ignorar el snapshot
                if (FirebaseAuth.DefaultInstance.CurrentUser == null || FirebaseAuth.DefaultInstance.CurrentUser.UserId != myUid)
                {
                    Debug.Log("[InboxController] Snapshot recibido pero ya no autorizado para este uid; ignorando.");
                    return;
                }

                if (snap != null && snap.Exists)
                {
                    foreach (var c in snap.Children)
                    {
                        string uidChild = c.Key;
                        string username = c.Value != null ? (c.Value is string ? c.Value.ToString() : (c.Child("username").Exists ? c.Child("username").Value.ToString() : "")) : "";
                        AddRow(uidChild, username);
                    }
                }
                UpdateCounter();
            });
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[InboxController] Error cargando inbox inicial: " + ex);
            // Si es por permisos, solo desuscribir listeners para evitar escuchas canceladas (no limpiar UI)
            EnqueueOnMainThread(() => UnsubscribeFromInboxListeners()); // --- CHANGED ---
            return false;
        }
    }

    void Update()
    {
        lock (mainQueue)
        {
            while (mainQueue.Count > 0) mainQueue.Dequeue()?.Invoke();
        }
    }

    private void EnqueueOnMainThread(Action a)
    {
        lock (mainQueue) mainQueue.Enqueue(a);
    }

    private bool IsAuthorizedForInbox()
    {
        return FirebaseAuth.DefaultInstance.CurrentUser != null && myUid != null && FirebaseAuth.DefaultInstance.CurrentUser.UserId == myUid;
    }

    // DB handlers
    private void HandleChildAdded(object sender, ChildChangedEventArgs args)
    {
        Debug.Log($"[InboxController] HandleChildAdded called. auth={FirebaseAuth.DefaultInstance.CurrentUser?.UserId ?? "null"} error={args.DatabaseError?.Message}");
        if (args.DatabaseError != null)
        {
            Debug.LogError("[InboxController] DB Error: " + args.DatabaseError.Message);
            // si es permiso denegado, solo desuscribimos listeners para evitar spam de errores (no limpiamos UI)
            if (args.DatabaseError.Message?.ToLower().Contains("permission") == true)
            {
                Debug.LogWarning("[InboxController] Permission denied when listening to inbox - unsubscribing listeners only.");
                EnqueueOnMainThread(() => UnsubscribeFromInboxListeners()); // --- CHANGED ---
            }
            return;
        }

        var snap = args.Snapshot;
        EnqueueOnMainThread(() =>
        {
            if (!IsAuthorizedForInbox())
            {
                Debug.LogWarning("[InboxController] Evento recibido pero no autorizado para this inbox; ignorando.");
                return;
            }
            if (snap == null || !snap.Exists) { Debug.LogWarning("[InboxController] snapshot null/empty"); return; }
            string uid = snap.Key;
            string uname = snap.Value != null ? (snap.Value is string ? snap.Value.ToString() : (snap.Child("username").Exists ? snap.Child("username").Value.ToString() : "")) : "";
            Debug.Log($"[InboxController] Instantiating row for uid={uid} uname={uname} prefabAssigned={(requestRowPrefab != null)} contentAssigned={(content != null)}");
            AddRow(uid, uname);
        });
    }

    private void HandleChildRemoved(object sender, ChildChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError("[InboxController] DB Error: " + args.DatabaseError.Message);
            if (args.DatabaseError.Message?.ToLower().Contains("permission") == true)
            {
                Debug.LogWarning("[InboxController] Permission denied on remove - unsubscribing listeners only.");
                EnqueueOnMainThread(() => UnsubscribeFromInboxListeners()); // --- CHANGED ---
            }
            return;
        }
        var snap = args.Snapshot;
        EnqueueOnMainThread(() =>
        {
            if (!IsAuthorizedForInbox())
            {
                Debug.LogWarning("[InboxController] Evento Remove recibido pero no autorizado para this inbox; ignorando.");
                return;
            }
            RemoveRow(snap.Key);
        });
    }

    // Fallback events (desde FriendRequestManager)
    private void OnInboxRequestAddedFallback(string senderUid, string senderUsername)
    {
        EnqueueOnMainThread(() =>
        {
            if (!rows.ContainsKey(senderUid)) AddRow(senderUid, senderUsername);
        });
    }

    private void OnInboxRequestRemovedFallback(string senderUid)
    {
        EnqueueOnMainThread(() =>
        {
            if (rows.ContainsKey(senderUid)) RemoveRow(senderUid);
        });
    }

    private void AddRow(string uid, string username)
    {
        if (rows.ContainsKey(uid)) return;
        if (requestRowPrefab == null || content == null)
        {
            Debug.LogWarning("[InboxController] NO se puede AddRow: requestRowPrefab o content no asignados. prefab=" + (requestRowPrefab != null) + " content=" + (content != null));
            return;
        }

        var go = Instantiate(requestRowPrefab);
        go.name = "RequestRow_" + uid;
        go.transform.SetParent(content, false);
        go.SetActive(true);

        var ctrl = go.GetComponent<RequestRowController>();
        if (ctrl != null)
        {
            try { ctrl.Init(uid, username, friendRequestManager, this); }
            catch (Exception ex) { Debug.LogError("[InboxController] Exception en RequestRowController.Init: " + ex); }
        }
        else { Debug.LogWarning("[InboxController] requestRowPrefab no tiene RequestRowController."); }

        rows[uid] = go;

        try { LayoutRebuilder.ForceRebuildLayoutImmediate(content as RectTransform); } catch (Exception) { }

        UpdateCounter();
    }

    public void RemoveRow(string uid)
    {
        if (!rows.ContainsKey(uid)) return;
        var go = rows[uid];
        rows.Remove(uid);
        if (go != null) Destroy(go);
        UpdateCounter();
    }

    private void UpdateCounter()
    {
        if (counterText == null) return;
        counterText.text = $"Solicitudes ({rows.Count})";
    }

    public void OnResponded(string uid)
    {
        RemoveRow(uid);
    }

    // --- CHANGED: split unsubscriber that only detaches listeners (no UI clear) ---
    private void UnsubscribeFromInboxListeners()
    {
        try
        {
            if (inboxRef != null)
            {
                inboxRef.ChildAdded -= HandleChildAdded;
                inboxRef.ChildRemoved -= HandleChildRemoved;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[InboxController] Error al desuscribirse listeners: " + ex);
        }
        dbSubscribed = false;
        inboxRef = null;
    }

    private void UnsubscribeFromInbox()
    {
        // full cleanup: detach listeners + clear UI
        UnsubscribeFromInboxListeners();

        // destruir filas UI existentes (si las hay)
        foreach (var kv in rows.Values)
        {
            if (kv != null) Destroy(kv);
        }
        rows.Clear();
        UpdateCounter();
    }

    private void OnDestroy()
    {
        try
        {
            EnqueueOnMainThread(() => UnsubscribeFromInbox());
        }
        catch { }

        try
        {
            FirebaseAuth.DefaultInstance.StateChanged -= OnAuthStateChanged;
        }
        catch { }

        if (friendRequestManager != null)
        {
            try
            {
                friendRequestManager.OnInboxRequestAdded -= OnInboxRequestAddedFallback;
                friendRequestManager.OnInboxRequestRemoved -= OnInboxRequestRemovedFallback;
            }
            catch { }
        }
    }

    public async void DebugDumpInbox()
    {
        try
        {
            if (string.IsNullOrEmpty(myUid))
            {
                Debug.LogWarning("[InboxController-debug] myUid vacío, no puedo dumpear inbox.");
                return;
            }

            var snap = await FirebaseDatabase.DefaultInstance
                         .GetReference($"users/{myUid}/friendRequests/inbox")
                         .GetValueAsync();
            Debug.Log("[InboxController-debug] Inbox snapshot exists: " + (snap != null && snap.Exists));
            if (snap != null && snap.Exists)
            {
                foreach (var c in snap.Children)
                {
                    Debug.Log($"[InboxController-debug] child: key={c.Key}, val={c.Value}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[InboxController-debug] Dump error: " + ex);
        }
    }

    // --- CHANGED: EnsureAuthToken helper (copiar la misma lógica en otros controladores) ---
    private async Task<bool> EnsureAuthToken()
    {
        try
        {
            var user = FirebaseAuth.DefaultInstance.CurrentUser;
            if (user == null)
            {
                Debug.LogWarning("[InboxController] EnsureAuthToken: no hay usuario autenticado.");
                return false;
            }
            var token = await user.TokenAsync(false);
            Debug.Log("[InboxController] Token obtenido (len): " + (token?.Length ?? 0));
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[InboxController] Token fetch failed: " + ex + " - intentando refresh");
            try
            {
                var user2 = FirebaseAuth.DefaultInstance.CurrentUser;
                if (user2 == null) return false;
                var token2 = await user2.TokenAsync(true);
                Debug.Log("[InboxController] Token refresh OK (len): " + (token2?.Length ?? 0));
                return true;
            }
            catch (Exception ex2)
            {
                Debug.LogWarning("[InboxController] Token refresh failed: " + ex2);
                return false;
            }
        }
    }
}
