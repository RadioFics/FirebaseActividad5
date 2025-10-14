using Firebase.Database;
using Firebase.Extensions;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LeaderBoardManager : MonoBehaviour
{
    [SerializeField] private int topCount = 3;

    private Query leaderboardQuery;

    private void Start()
    {
        leaderboardQuery = FirebaseDatabase.DefaultInstance
            .GetReference("users")
            .OrderByChild("score")
            .LimitToLast(topCount);

        leaderboardQuery.ValueChanged += HandleLeaderboardValueChanged;
    }

    private void OnDestroy()
    {
        if (leaderboardQuery != null)
            leaderboardQuery.ValueChanged -= HandleLeaderboardValueChanged;
    }

    private void HandleLeaderboardValueChanged(object sender, ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError("Firebase DB error: " + args.DatabaseError.Message);
            return;
        }

        DataSnapshot snapshot = args.Snapshot;
        if (snapshot == null || !snapshot.Exists)
        {
            Debug.Log("Leaderboard vacío.");
            return;
        }

        var list = new List<(string uid, string username, int score)>();

        foreach (DataSnapshot child in snapshot.Children)
        {
            string uid = child.Key;
            string username = "Unknown";
            int score = 0;

            var usernameSnap = child.Child("username");
            if (usernameSnap.Exists && usernameSnap.Value != null)
                username = usernameSnap.Value.ToString();

            var scoreSnap = child.Child("score");
            if (scoreSnap.Exists && scoreSnap.Value != null)
                int.TryParse(scoreSnap.Value.ToString(), out score);

            list.Add((uid, username, score));
        }

        var ordered = list.OrderByDescending(x => x.score).Take(topCount).ToList();

        Debug.Log("=== Leaderboard Top " + topCount + " ===");
        for (int i = 0; i < ordered.Count; i++)
        {
            var item = ordered[i];
            Debug.Log($"{i + 1}. {item.username} | {item.score}");
        }
    }
}
