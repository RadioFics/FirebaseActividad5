using System;
using System.Collections.Generic;
using Firebase.Auth;
using Firebase.Database;
using UnityEngine;

public class LeaderboardUIManager : MonoBehaviour
{
    public Transform content;
    public GameObject rowPrefab;

    private DatabaseReference leaderboardRef;
    private bool dbSubscribed = false;
    private Queue<Action> mainQueue = new Queue<Action>();

    void Start()
    {
        FirebaseAuth.DefaultInstance.StateChanged += OnAuthStateChanged;
        if (FirebaseAuth.DefaultInstance.CurrentUser != null)
            SubscribeLeaderboard();
    }

    private void OnAuthStateChanged(object sender, EventArgs e)
    {
        var user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user == null)
        {
            EnqueueOnMainThread(UnsubscribeLeaderboard);
            return;
        }
        SubscribeLeaderboard();
    }

    private void SubscribeLeaderboard()
    {
        if (dbSubscribed) return;
        try
        {
            leaderboardRef = FirebaseDatabase.DefaultInstance.GetReference("leaderboard");
            leaderboardRef.ValueChanged += HandleLeaderboardValueChanged;
            dbSubscribed = true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[LeaderboardUIManager] Subscribe failed: " + ex);
            dbSubscribed = false;
        }
    }

    private void UnsubscribeLeaderboard()
    {
        try
        {
            if (leaderboardRef != null && dbSubscribed)
                leaderboardRef.ValueChanged -= HandleLeaderboardValueChanged;
        }
        catch (Exception ex) { Debug.LogWarning("[LeaderboardUIManager] Unsubscribe error: " + ex); }
        dbSubscribed = false;
        leaderboardRef = null;
        // opcional: limpiar UI aquí
    }

    private void HandleLeaderboardValueChanged(object sender, ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError("Firebase DB error: " + args.DatabaseError.Message);
            if (args.DatabaseError.Message?.ToLower().Contains("permission") == true)
                EnqueueOnMainThread(UnsubscribeLeaderboard);
            return;
        }

        var snap = args.Snapshot;
        EnqueueOnMainThread(() =>
        {
            if (snap == null || !snap.Exists) return;
            foreach (var child in snap.Children)
            {
                Debug.Log($"[LeaderboardUIManager] {child.Key} : {child.Value}");
                // Actualizar UI según estructura de su leaderboard
            }
        });
    }

    void Update()
    {
        lock (mainQueue) { while (mainQueue.Count > 0) mainQueue.Dequeue()?.Invoke(); }
    }

    private void EnqueueOnMainThread(Action a)
    {
        lock (mainQueue) mainQueue.Enqueue(a);
    }

    private void OnDestroy()
    {
        FirebaseAuth.DefaultInstance.StateChanged -= OnAuthStateChanged;
        UnsubscribeLeaderboard();
    }
}