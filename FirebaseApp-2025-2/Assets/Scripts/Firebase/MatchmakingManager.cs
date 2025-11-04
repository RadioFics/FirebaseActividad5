using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Auth;
using Firebase.Database;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MatchmakingManager : MonoBehaviour
{
    [Header("UI")]
    public Transform onlineContent;
    public GameObject matchRowPrefab;
    public Transform incomingContent;
    public GameObject incomingRowPrefab;

    [Header("Settings")]
    public string usersOnlinePath = "users-online";
    public string matchRequestsPath = "matchRequests";

    private string myUid;
    private string myUsername = "";
    private DatabaseReference onlineRef;
    private DatabaseReference myRequestsRef;
    private Queue<Action> mainQueue = new Queue<Action>();
    private Dictionary<string, GameObject> onlineRows = new Dictionary<string, GameObject>();
    private Dictionary<string, GameObject> incomingRows = new Dictionary<string, GameObject>();

    void Start()
    {
        FirebaseAuth.DefaultInstance.StateChanged += OnAuthStateChanged;
        var current = FirebaseAuth.DefaultInstance.CurrentUser;
        if (current != null) _ = InitializeForUser(current.UserId);
    }

    private void OnAuthStateChanged(object sender, EventArgs e)
    {
        var cur = FirebaseAuth.DefaultInstance.CurrentUser;
        if (cur == null)
        {
            Enqueue(() => Teardown());
            myUid = null;
            return;
        }
        if (myUid == cur.UserId) return;
        _ = InitializeForUser(cur.UserId);
    }

    private async Task InitializeForUser(string uid)
    {
        Enqueue(() => Teardown());
        myUid = uid;

        Debug.Log($"[Matchmaking] Inicializando para uid={myUid}");

        // Log DB URL (útil para verificar que apuntas al proyecto correcto)
        try
        {
            var dbUrl = Firebase.FirebaseApp.DefaultInstance?.Options?.DatabaseUrl;
            Debug.Log("[Matchmaking] DatabaseUrl: " + (dbUrl != null ? dbUrl.ToString() : "null"));
        }
        catch (Exception ex) { Debug.LogWarning("[Matchmaking] Error al leer DatabaseUrl: " + ex); }

        // Asegurar token válido antes de subscribir (importante)
        var ok = await EnsureAuthToken();
        if (!ok)
        {
            Debug.LogWarning("[Matchmaking] No hay token válido: abortando suscripción a matchmaking.");
            return;
        }

        // intentar leer username (si existe)
        try
        {
            var snap = await FirebaseDatabase.DefaultInstance.GetReference($"users/{myUid}/username").GetValueAsync();
            if (snap != null && snap.Exists && snap.Value != null) myUsername = snap.Value.ToString();
            else myUsername = "";
        }
        catch { myUsername = ""; }

        // Subscribe users-online
        try
        {
            onlineRef = FirebaseDatabase.DefaultInstance.GetReference(usersOnlinePath);
            onlineRef.ChildAdded += Online_Added;
            onlineRef.ChildRemoved += Online_Removed;

            var snap = await onlineRef.GetValueAsync();
            Enqueue(() =>
            {
                if (snap != null && snap.Exists)
                {
                    foreach (var c in snap.Children)
                    {
                        AddOnlineRowSafe(c.Key, c.Value != null ? c.Value.ToString() : "");
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Matchmaking] Online subscribe failed: " + ex);
        }

        // Subscribe incoming matchRequests (mi inbox)
        try
        {
            myRequestsRef = FirebaseDatabase.DefaultInstance.GetReference($"users/{myUid}/{matchRequestsPath}");
            myRequestsRef.ChildAdded += Incoming_Added;
            myRequestsRef.ChildRemoved += Incoming_Removed;

            var s2 = await myRequestsRef.GetValueAsync();
            Enqueue(() =>
            {
                if (s2 != null && s2.Exists)
                {
                    foreach (var c in s2.Children)
                    {
                        var senderUid = c.Key;
                        var uname = c.Value != null ? c.Value.ToString() : (c.Child("username").Exists ? c.Child("username").Value.ToString() : "");
                        AddIncomingRowSafe(senderUid, uname);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Matchmaking] Incoming subscribe failed: " + ex);
        }
    }

    // --- Listeners ---
    private void Online_Added(object sender, ChildChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError("[Matchmaking] Online_Added error: " + args.DatabaseError.Message);
            if (args.DatabaseError.Message?.ToLower().Contains("permission") == true) Enqueue(() => UnsubscribeOnlineSafe());
            return;
        }
        var snap = args.Snapshot;
        Enqueue(() =>
        {
            if (snap == null || !snap.Exists) return;
            AddOnlineRowSafe(snap.Key, snap.Value != null ? snap.Value.ToString() : "");
        });
    }

    private void Online_Removed(object sender, ChildChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError("[Matchmaking] Online_Removed error: " + args.DatabaseError.Message);
            if (args.DatabaseError.Message?.ToLower().Contains("permission") == true) Enqueue(() => UnsubscribeOnlineSafe());
            return;
        }
        var snap = args.Snapshot;
        Enqueue(() =>
        {
            if (snap == null) return;
            RemoveOnlineRowSafe(snap.Key);
        });
    }

    private void Incoming_Added(object sender, ChildChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError("[Matchmaking] Incoming_Added error: " + args.DatabaseError.Message);
            if (args.DatabaseError.Message?.ToLower().Contains("permission") == true) Enqueue(() => UnsubscribeIncomingSafe());
            return;
        }
        var snap = args.Snapshot;
        Enqueue(() =>
        {
            if (snap == null || !snap.Exists) return;
            var senderUid = snap.Key;
            var uname = snap.Value != null ? snap.Value.ToString() : (snap.Child("username").Exists ? snap.Child("username").Value.ToString() : "");
            AddIncomingRowSafe(senderUid, uname);
        });
    }

    private void Incoming_Removed(object sender, ChildChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError("[Matchmaking] Incoming_Removed error: " + args.DatabaseError.Message);
            if (args.DatabaseError.Message?.ToLower().Contains("permission") == true) Enqueue(() => UnsubscribeIncomingSafe());
            return;
        }
        var snap = args.Snapshot;
        Enqueue(() =>
        {
            if (snap == null) return;
            RemoveIncomingRowSafe(snap.Key);
        });
    }

    // --- UI helpers ---
    private void AddOnlineRowSafe(string uid, string username)
    {
        if (string.IsNullOrEmpty(uid) || onlineRows.ContainsKey(uid) || uid == myUid) return;
        if (matchRowPrefab == null || onlineContent == null) { Debug.LogWarning("[Matchmaking] prefab/content missing"); return; }

        var go = Instantiate(matchRowPrefab, onlineContent, false);
        go.name = "MatchRow_" + uid;
        var ctrl = go.GetComponent<MatchRowController>();
        if (ctrl != null) ctrl.Init(uid, username, this);
        onlineRows[uid] = go;
    }

    private void RemoveOnlineRowSafe(string uid)
    {
        if (string.IsNullOrEmpty(uid) || !onlineRows.ContainsKey(uid)) return;
        var go = onlineRows[uid];
        onlineRows.Remove(uid);
        if (go != null) Destroy(go);
    }

    private void AddIncomingRowSafe(string uid, string username)
    {
        if (string.IsNullOrEmpty(uid) || incomingRows.ContainsKey(uid)) return;
        if (incomingRowPrefab == null || incomingContent == null) { Debug.LogWarning("[Matchmaking] incoming prefab/content missing"); return; }

        var go = Instantiate(incomingRowPrefab, incomingContent, false);
        go.name = "IncomingMatch_" + uid;
        var ctrl = go.GetComponent<IncomingMatchRowController>();
        if (ctrl != null) ctrl.Init(uid, username, this);
        incomingRows[uid] = go;
    }

    private void RemoveIncomingRowSafe(string uid)
    {
        if (string.IsNullOrEmpty(uid) || !incomingRows.ContainsKey(uid)) return;
        var go = incomingRows[uid];
        incomingRows.Remove(uid);
        if (go != null) Destroy(go);
    }

    // --- Public actions ---
    public async void SendMatchRequest(string targetUid, string targetName)
    {
        if (string.IsNullOrEmpty(myUid) || string.IsNullOrEmpty(targetUid)) return;
        try
        {
            var requestRef = FirebaseDatabase.DefaultInstance
                .GetReference($"users/{targetUid}/{matchRequestsPath}/{myUid}");

            var payload = string.IsNullOrEmpty(myUsername) ? myUid : myUsername;
            await requestRef.SetValueAsync(payload);

            // opcional outbox
            await FirebaseDatabase.DefaultInstance.GetReference($"users/{myUid}/{matchRequestsPath}-outbox/{targetUid}")
                .SetValueAsync(payload);

            Debug.Log($"[Matchmaking] Match request sent to {targetUid}");
        }
        catch (Exception ex)
        {
            Debug.LogError("[Matchmaking] SendMatchRequest failed: " + ex);
        }
    }

    public async void RespondMatchRequest(string senderUid, bool accepted)
    {
        if (string.IsNullOrEmpty(myUid) || string.IsNullOrEmpty(senderUid)) return;
        try
        {
            await FirebaseDatabase.DefaultInstance.GetReference($"users/{myUid}/{matchRequestsPath}/{senderUid}").SetValueAsync(null);

            if (accepted)
            {
                var matchRef = FirebaseDatabase.DefaultInstance.GetReference("matches").Push();
                var matchObj = new Dictionary<string, object>()
                {
                    { "participants", new Dictionary<string, object> { { myUid, true }, { senderUid, true } } },
                    { "state", "pending" },
                    { "createdBy", myUid },
                    { "createdAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
                };
                await matchRef.SetValueAsync(matchObj);
                Debug.Log("[Matchmaking] Match created: " + matchRef.Key);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[Matchmaking] RespondMatchRequest failed: " + ex);
        }
    }

    void Update()
    {
        lock (mainQueue)
        {
            while (mainQueue.Count > 0) mainQueue.Dequeue()?.Invoke();
        }
    }

    private void Enqueue(Action a) { lock (mainQueue) mainQueue.Enqueue(a); }

    private void Teardown()
    {
        UnsubscribeOnlineSafe();
        UnsubscribeIncomingSafe();

        foreach (var kv in onlineRows.Values) if (kv != null) Destroy(kv);
        onlineRows.Clear();
        foreach (var kv in incomingRows.Values) if (kv != null) Destroy(kv);
        incomingRows.Clear();
    }

    private void UnsubscribeOnlineSafe()
    {
        try
        {
            if (onlineRef != null)
            {
                onlineRef.ChildAdded -= Online_Added;
                onlineRef.ChildRemoved -= Online_Removed;
                onlineRef = null;
            }
        }
        catch { }
    }

    private void UnsubscribeIncomingSafe()
    {
        try
        {
            if (myRequestsRef != null)
            {
                myRequestsRef.ChildAdded -= Incoming_Added;
                myRequestsRef.ChildRemoved -= Incoming_Removed;
                myRequestsRef = null;
            }
        }
        catch { }
    }

    private void OnDestroy()
    {
        try { FirebaseAuth.DefaultInstance.StateChanged -= OnAuthStateChanged; } catch { }
        Teardown();
    }

    // --- Helpers: auth token check ---
    private async Task<bool> EnsureAuthToken()
    {
        try
        {
            var user = FirebaseAuth.DefaultInstance.CurrentUser;
            if (user == null)
            {
                Debug.LogWarning("[Matchmaking] EnsureAuthToken: no hay usuario autenticado.");
                return false;
            }
            var token = await user.TokenAsync(false);
            Debug.Log("[Matchmaking] Token obtenido (len): " + (token?.Length ?? 0));
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Matchmaking] Token fetch failed: " + ex.Message + " - intentando refresh.");
            try
            {
                var user2 = FirebaseAuth.DefaultInstance.CurrentUser;
                if (user2 == null) return false;
                var token2 = await user2.TokenAsync(true);
                Debug.Log("[Matchmaking] Token refresh OK (len): " + (token2?.Length ?? 0));
                return true;
            }
            catch (Exception ex2)
            {
                Debug.LogWarning("[Matchmaking] Token refresh failed: " + ex2);
                return false;
            }
        }
    }
}
