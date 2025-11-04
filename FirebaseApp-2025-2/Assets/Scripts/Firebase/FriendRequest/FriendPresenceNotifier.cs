using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Auth;
using Firebase.Database;
using TMPro;
using UnityEngine;

public class FriendPresenceNotifier : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Prefab de notificación: debe contener un componente NotificationToast (ver archivo).")]
    public GameObject notificationPrefab;
    [Tooltip("Parent donde instanciar notificaciones (un Panel dentro de Canvas).")]
    public RectTransform notificationParent;

    [Header("Settings")]
    [Tooltip("Duración (seg) que muestra cada notificación antes de desaparecer.")]
    public float notificationDuration = 3.0f;
    [Tooltip("Cooldown por uid para evitar spam (segundos).")]
    public float perFriendCooldown = 2.0f;
    [Tooltip("Si true, no mostrará notificaciones por usuarios ya online al inicializar (solo cambios posteriores).")]
    public bool suppressInitialOnlineNotifications = true;

    // internals
    private string myUid;
    private HashSet<string> friendUids = new HashSet<string>();
    private DatabaseReference usersOnlineRef;
    private DatabaseReference myFriendsRef;
    private Dictionary<string, string> lastSeenOnlineName = new Dictionary<string, string>(); // uid -> username
    private Dictionary<string, double> lastNotificationTime = new Dictionary<string, double>(); // uid -> unix time

    private bool initialLoadDone = false;

    async void Start()
    {
        // quick validations
        if (notificationPrefab == null || notificationParent == null)
        {
            Debug.LogWarning("[FriendPresenceNotifier] Asigna notificationPrefab y notificationParent en el Inspector.");
        }

        FirebaseAuth.DefaultInstance.StateChanged += OnAuthStateChanged;
        var current = FirebaseAuth.DefaultInstance.CurrentUser;
        if (current != null)
            await InitializeForUser(current.UserId);
    }

    private void OnAuthStateChanged(object sender, EventArgs e)
    {
        var cur = FirebaseAuth.DefaultInstance.CurrentUser;
        if (cur == null)
        {
            Teardown();
            return;
        }

        // si cambió de usuario, reinit
        if (myUid != cur.UserId)
            _ = InitializeForUser(cur.UserId);
    }

    private async Task InitializeForUser(string uid)
    {
        try
        {
            Teardown();

            myUid = uid;
            Debug.Log("[FriendPresenceNotifier] Inicializando para uid: " + myUid);

            // leer lista de amigos (una vez al inicio) - reglas permiten leer users/{myUid}/friends
            myFriendsRef = FirebaseDatabase.DefaultInstance.GetReference($"users/{myUid}/friends");
            try
            {
                var snap = await myFriendsRef.GetValueAsync();
                friendUids.Clear();
                if (snap != null && snap.Exists)
                {
                    foreach (var c in snap.Children)
                    {
                        friendUids.Add(c.Key);
                    }
                }
                Debug.Log($"[FriendPresenceNotifier] Friends cargados: {friendUids.Count}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[FriendPresenceNotifier] No pude cargar friends iniciales: " + ex);
            }

            // subscribe to users-online
            usersOnlineRef = FirebaseDatabase.DefaultInstance.GetReference("users-online");
            usersOnlineRef.ChildAdded += HandleUserOnlineAdded;
            usersOnlineRef.ChildRemoved += HandleUserOnlineRemoved;

            // opcional: si no quieres notificar por usuarios ya online al arrancar, hacemos una carga inicial y la ignoramos
            if (suppressInitialOnlineNotifications)
            {
                try
                {
                    var snap = await usersOnlineRef.GetValueAsync();
                    if (snap != null && snap.Exists)
                    {
                        foreach (var c in snap.Children)
                        {
                            // guardar mapping para desconexiones posteriores
                            var uidFriend = c.Key;
                            var uname = c.Value != null ? c.Value.ToString() : "";
                            if (!string.IsNullOrEmpty(uname)) lastSeenOnlineName[uidFriend] = uname;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[FriendPresenceNotifier] initial users-online load failed: " + ex);
                }
                initialLoadDone = true;
            }
            else
            {
                initialLoadDone = true; // notificar también si ya estaban (ChildAdded callbacks ocurrirán)
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[FriendPresenceNotifier] InitializeForUser fallo: " + ex);
        }
    }

    private void HandleUserOnlineAdded(object sender, ChildChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError("[FriendPresenceNotifier] users-online ChildAdded error: " + args.DatabaseError.Message);
            return;
        }

        var snap = args.Snapshot;
        if (snap == null || !snap.Exists) return;

        var uid = snap.Key;
        var username = snap.Value != null ? snap.Value.ToString() : uid;

        // guardar nombre para desconexiones futuras
        lastSeenOnlineName[uid] = username;

        // si no terminó la carga inicial y suprimimos notifs, no notificar
        if (!initialLoadDone && suppressInitialOnlineNotifications) return;

        if (friendUids.Contains(uid))
        {
            if (CanNotifyNow(uid))
                ShowNotification($"{username} se ha conectado");
        }
    }

    private void HandleUserOnlineRemoved(object sender, ChildChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError("[FriendPresenceNotifier] users-online ChildRemoved error: " + args.DatabaseError.Message);
            return;
        }

        var snap = args.Snapshot;
        if (snap == null) return;

        var uid = snap.Key;
        string username = null;

        // preferimos el nombre que guardamos cuando se conectó; si no, intentar leer snapshot.Value; si no, usar uid.
        if (lastSeenOnlineName.ContainsKey(uid)) username = lastSeenOnlineName[uid];
        else if (snap.Value != null) username = snap.Value.ToString();
        else username = uid;

        // limpiar el mapa
        if (lastSeenOnlineName.ContainsKey(uid)) lastSeenOnlineName.Remove(uid);

        if (friendUids.Contains(uid))
        {
            if (CanNotifyNow(uid))
                ShowNotification($"{username} se ha desconectado");
        }
    }

    private bool CanNotifyNow(string uid)
    {
        double now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!lastNotificationTime.ContainsKey(uid))
        {
            lastNotificationTime[uid] = now;
            return true;
        }
        var last = lastNotificationTime[uid];
        if (now - last >= perFriendCooldown)
        {
            lastNotificationTime[uid] = now;
            return true;
        }
        return false;
    }

    private void ShowNotification(string message)
    {
        if (notificationPrefab == null || notificationParent == null)
        {
            Debug.Log("[FriendPresenceNotifier] Notificación: " + message + " (prefab/parent no asignado)");
            return;
        }

        var inst = Instantiate(notificationPrefab, notificationParent, false);
        var toast = inst.GetComponent<NotificationToast>();
        if (toast != null)
        {
            toast.SetText(message);
            toast.ShowAndAutoHide(notificationDuration);
        }
        else
        {
            // fallback: si el prefab no tiene NotificationToast, buscar TMP y setear texto + destruir luego
            var tmp = inst.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = message;
            Destroy(inst, notificationDuration + 0.5f);
        }
    }

    // liberar listeners
    private void Teardown()
    {
        try
        {
            if (usersOnlineRef != null)
            {
                usersOnlineRef.ChildAdded -= HandleUserOnlineAdded;
                usersOnlineRef.ChildRemoved -= HandleUserOnlineRemoved;
            }
        }
        catch { }

        friendUids.Clear();
        lastSeenOnlineName.Clear();
        lastNotificationTime.Clear();
        initialLoadDone = false;
    }

    private void OnDestroy()
    {
        try { FirebaseAuth.DefaultInstance.StateChanged -= OnAuthStateChanged; } catch { }
        Teardown();
    }
}
