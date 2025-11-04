using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Auth;
using Firebase.Database;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class OutboxController : MonoBehaviour
{
    [Header("UI - assign in Inspector")]
    public Transform content;
    public GameObject sentRequestRowPrefab;
    public TMP_Text counterText;

    public FriendRequestManager friendRequestManager; // opcional

    private DatabaseReference outboxRef;
    private string myUid;
    private string myUsername = ""; // <-- guardamos el username local para comparar
    private Queue<Action> mainQueue = new Queue<Action>();
    private Dictionary<string, GameObject> rows = new Dictionary<string, GameObject>();
    private bool dbSubscribed = false;

    // cache para evitar lecturas repetidas
    private Dictionary<string, string> uidNameCache = new Dictionary<string, string>(StringComparer.Ordinal);

    async void Start()
    {
        FirebaseAuth.DefaultInstance.StateChanged += OnAuthStateChanged;

        var current = FirebaseAuth.DefaultInstance.CurrentUser;
        if (current != null)
            await InitializeForUser(current.UserId);

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
    }

    private void OnAuthStateChanged(object sender, EventArgs e)
    {
        var current = FirebaseAuth.DefaultInstance.CurrentUser;
        if (current == null)
        {
            EnqueueOnMainThread(UnsubscribeFromOutbox);
            myUid = null;
            myUsername = "";
            return;
        }
        if (dbSubscribed && myUid == current.UserId) return;
        _ = InitializeForUser(current.UserId);
    }

    private async Task<bool> InitializeForUser(string uid)
    {
        if (dbSubscribed) EnqueueOnMainThread(UnsubscribeFromOutbox);
        myUid = uid;
        Debug.Log("[OutboxController] Inicializando outbox para uid: " + myUid);

        // intentar leer username propio (permitido por reglas)
        try
        {
            var snap = await FirebaseDatabase.DefaultInstance.GetReference($"users/{myUid}/username").GetValueAsync();
            if (snap != null && snap.Exists && snap.Value != null) myUsername = snap.Value.ToString();
            else myUsername = "";
            Debug.Log("[OutboxController] myUsername local: " + myUsername);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[OutboxController] No pude leer myUsername: " + ex);
            myUsername = "";
        }

        var ok = await EnsureAuthToken();
        if (!ok)
        {
            Debug.LogWarning("[OutboxController] No token válido: abortando init outbox.");
            return false;
        }

        try
        {
            outboxRef = FirebaseDatabase.DefaultInstance.GetReference($"users/{myUid}/friendRequests/outbox");
            outboxRef.ChildAdded += HandleChildAdded;
            outboxRef.ChildChanged += HandleChildChanged;
            outboxRef.ChildRemoved += HandleChildRemoved;
            dbSubscribed = true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[OutboxController] Subscribe failed: " + ex);
            dbSubscribed = false;
            return false;
        }

        try
        {
            var snap = await outboxRef.GetValueAsync();
            EnqueueOnMainThread(() =>
            {
                if (snap != null && snap.Exists)
                {
                    foreach (var c in snap.Children)
                    {
                        var targetUid = c.Key;
                        var usernameFromOutbox = ParseUsernameFromSnapshot(c);
                        // si el username del outbox es igual a miUsername => no usarlo como nombre del target
                        if (!string.IsNullOrEmpty(usernameFromOutbox) && usernameFromOutbox == myUsername)
                            usernameFromOutbox = ""; // forzar fallback
                        AddOrUpdateRow(targetUid, usernameFromOutbox);
                    }
                }
                UpdateCounter();
            });
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[OutboxController] Initial load failed: " + ex);
            if (ex.Message?.ToLower().Contains("permission") == true) EnqueueOnMainThread(UnsubscribeFromOutbox);
            return false;
        }
    }

    void Update()
    {
        lock (mainQueue)
        {
            while (mainQueue.Count > 0)
            {
                try { mainQueue.Dequeue()?.Invoke(); }
                catch (Exception ex) { Debug.LogError("[OutboxController] mainQueue exception: " + ex); }
            }
        }
    }

    private void EnqueueOnMainThread(Action a) { lock (mainQueue) mainQueue.Enqueue(a); }

    private void HandleChildAdded(object sender, ChildChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError("[OutboxController] DB Error on ChildAdded: " + args.DatabaseError.Message);
            if (args.DatabaseError.Message?.ToLower().Contains("permission") == true) EnqueueOnMainThread(UnsubscribeFromOutbox);
            return;
        }
        var snap = args.Snapshot;
        EnqueueOnMainThread(() =>
        {
            if (snap == null || !snap.Exists) return;
            var uid = snap.Key;
            var username = ParseUsernameFromSnapshot(snap);
            if (!string.IsNullOrEmpty(username) && username == myUsername) username = ""; // no mostrar el username del emisor
            AddOrUpdateRow(uid, username);
        });
    }

    private void HandleChildChanged(object sender, ChildChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError("[OutboxController] DB Error on ChildChanged: " + args.DatabaseError.Message);
            return;
        }
        var snap = args.Snapshot;
        EnqueueOnMainThread(() =>
        {
            if (snap == null || !snap.Exists) return;
            var uid = snap.Key;
            var username = ParseUsernameFromSnapshot(snap);
            if (!string.IsNullOrEmpty(username) && username == myUsername) username = "";
            var status = ParseStatusFromSnapshot(snap);
            if (status != 0)
            {
                RemoveRow(uid);
            }
            else
            {
                AddOrUpdateRow(uid, username);
            }
        });
    }

    private void HandleChildRemoved(object sender, ChildChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError("[OutboxController] DB Error on ChildRemoved: " + args.DatabaseError.Message);
            return;
        }
        var snap = args.Snapshot;
        EnqueueOnMainThread(() =>
        {
            if (snap == null) return;
            RemoveRow(snap.Key);
        });
    }

    // Parse helpers: soporta string value o raw JSON { "username":"X", "status":0 }
    private string ParseUsernameFromSnapshot(DataSnapshot snap)
    {
        if (snap == null || !snap.Exists) return "";
        try
        {
            if (snap.Value is string) return snap.Value.ToString();
            var raw = snap.GetRawJsonValue();
            if (!string.IsNullOrEmpty(raw))
            {
                var fr = JsonUtility.FromJson<OutboxFriendDto>(raw);
                if (fr != null && !string.IsNullOrEmpty(fr.username)) return fr.username;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[OutboxController] ParseUsernameFromSnapshot error: " + ex);
        }
        return "";
    }

    private int ParseStatusFromSnapshot(DataSnapshot snap)
    {
        if (snap == null || !snap.Exists) return 0;
        try
        {
            var raw = snap.GetRawJsonValue();
            if (!string.IsNullOrEmpty(raw))
            {
                var fr = JsonUtility.FromJson<OutboxFriendDto>(raw);
                if (fr != null) return fr.status;
            }
        }
        catch { }
        return 0;
    }

    private void AddOrUpdateRow(string targetUid, string username)
    {
        if (string.IsNullOrEmpty(targetUid)) return;

        // si la fila existe, actualizar
        if (rows.ContainsKey(targetUid))
        {
            var existingRow = rows[targetUid];
            var ctrl = existingRow.GetComponent<SentRequestRowController>();
            if (ctrl != null)
            {
                // si el username viene vacío, mostrar provisionalmente el UID (y luego intentar resolución)
                if (!string.IsNullOrEmpty(username)) ctrl.UpdateUsername(username);
                else ctrl.UpdateUsername(ShortUidLabel(targetUid));
                // intentar resolver nombre real (si las reglas lo permiten)
                _ = ResolveAndSetTargetName(targetUid, existingRow);
            }
            return;
        }

        // creación
        if (sentRequestRowPrefab == null || content == null)
        {
            Debug.LogWarning("[OutboxController] prefab/content missing");
            return;
        }

        var newRow = Instantiate(sentRequestRowPrefab);
        newRow.name = "SentRequestRow_" + targetUid;
        newRow.transform.SetParent(content, false);
        newRow.SetActive(true);

        var ctrlComp = newRow.GetComponent<SentRequestRowController>();
        if (ctrlComp != null)
        {
            try
            {
                // si no tenemos username legible, mostramos UID corto
                if (string.IsNullOrEmpty(username)) username = ShortUidLabel(targetUid);
                ctrlComp.Init(targetUid, username);
                _ = ResolveAndSetTargetName(targetUid, newRow);
            }
            catch (Exception ex) { Debug.LogError("[OutboxController] SentRequestRowController.Init exception: " + ex); }
        }
        else
        {
            Debug.LogWarning("[OutboxController] sentRequestRowPrefab missing SentRequestRowController");
        }

        rows[targetUid] = newRow;

        try { LayoutRebuilder.ForceRebuildLayoutImmediate(content as RectTransform); } catch { }

        UpdateCounter();
    }

    private void RemoveRow(string uid)
    {
        if (string.IsNullOrEmpty(uid)) return;
        if (!rows.ContainsKey(uid)) return;
        var go = rows[uid];
        rows.Remove(uid);
        if (go != null) Destroy(go);
        UpdateCounter();
    }

    private void UpdateCounter()
    {
        if (counterText == null) return;
        counterText.text = $"Enviadas ({rows.Count})";
    }

    private string ShortUidLabel(string uid)
    {
        if (string.IsNullOrEmpty(uid)) return "sin-id";
        return uid.Length > 8 ? uid.Substring(0, 8) + "..." : uid;
    }

    /// <summary>
    /// Intenta resolver username real del target (si las reglas permiten).
    /// Si falla por permisos, deja el valor actual (UID corto).
    /// </summary>
    private async Task ResolveAndSetTargetName(string targetUid, GameObject rowObj)
    {
        if (string.IsNullOrEmpty(targetUid) || rowObj == null) return;

        // probar cache
        if (uidNameCache.TryGetValue(targetUid, out var cachedName) && !string.IsNullOrEmpty(cachedName))
        {
            var ctrlCached = rowObj.GetComponent<SentRequestRowController>();
            if (ctrlCached != null) ctrlCached.UpdateUsername(cachedName);
            return;
        }

        try
        {
            // ESTA LECTURA fallará si las reglas no permiten leer users/{targetUid}
            var snap = await FirebaseDatabase.DefaultInstance.GetReference($"users/{targetUid}/username").GetValueAsync();

            if (snap != null && snap.Exists && snap.Value != null)
            {
                var targetName = snap.Value.ToString();
                uidNameCache[targetUid] = targetName;
                var ctrl = rowObj.GetComponent<SentRequestRowController>();
                if (ctrl != null) ctrl.UpdateUsername(targetName);
            }
            else
            {
                // no hay username registrado en la cuenta objetivo
                uidNameCache[targetUid] = "";
            }
        }
        catch (Exception ex)
        {
            // normalmente Permission denied aquí -> log y continuar con UID
            Debug.LogWarning("[OutboxController] ResolveAndSetTargetName falla (posible permiso): " + ex.Message);
        }
    }

    private async Task<bool> EnsureAuthToken()
    {
        try
        {
            var user = FirebaseAuth.DefaultInstance.CurrentUser;
            if (user == null) return false;
            var token = await user.TokenAsync(false);
            Debug.Log("[OutboxController] Token len: " + (token?.Length ?? 0));
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[OutboxController] Token fetch failed: " + ex);
            try
            {
                var user2 = FirebaseAuth.DefaultInstance.CurrentUser;
                if (user2 == null) return false;
                var token2 = await user2.TokenAsync(true);
                Debug.Log("[OutboxController] Token refresh len: " + (token2?.Length ?? 0));
                return true;
            }
            catch (Exception ex2)
            {
                Debug.LogWarning("[OutboxController] Token refresh failed: " + ex2);
                return false;
            }
        }
    }

    public void UnsubscribeFromOutbox()
    {
        try
        {
            if (outboxRef != null && dbSubscribed)
            {
                outboxRef.ChildAdded -= HandleChildAdded;
                outboxRef.ChildChanged -= HandleChildChanged;
                outboxRef.ChildRemoved -= HandleChildRemoved;
            }
        }
        catch (Exception ex) { Debug.LogWarning("[OutboxController] Unsubscribe error: " + ex); }
        dbSubscribed = false;
        outboxRef = null;

        foreach (var kv in rows.Values) if (kv != null) Destroy(kv);
        rows.Clear();
        UpdateCounter();
    }

    private void OnDestroy()
    {
        try { EnqueueOnMainThread(UnsubscribeFromOutbox); } catch { }
        try { FirebaseAuth.DefaultInstance.StateChanged -= OnAuthStateChanged; } catch { }
    }

    public async void DebugDumpOutbox()
    {
        try
        {
            if (string.IsNullOrEmpty(myUid)) { Debug.LogWarning("[OutboxController] myUid vacío"); return; }
            var snap = await FirebaseDatabase.DefaultInstance.GetReference($"users/{myUid}/friendRequests/outbox").GetValueAsync();
            Debug.Log("[OutboxController] Outbox snapshot exists: " + (snap != null && snap.Exists));
            if (snap != null && snap.Exists)
            {
                foreach (var c in snap.Children) Debug.Log($"outbox child: key={c.Key} valRaw={c.GetRawJsonValue()} val={c.Value}");
            }
        }
        catch (Exception ex) { Debug.LogError("[OutboxController] DebugDumpOutbox error: " + ex); }
    }

    [Serializable]
    private class OutboxFriendDto
    {
        public string username;
        public int status;
    }
}
