using Firebase.Database;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class LeaderboardUIManager : MonoBehaviour
{
    [Header("Firebase")]
    [SerializeField] private int topCount = 3;

    [Header("UI References")]
    [SerializeField] private Transform entryContainer; // Contenedor del leaderboard
    [SerializeField] private GameObject entryPrefab;   // Prefab con dos TMP_Text (username y score)

    private Query leaderboardQuery;
    private List<GameObject> spawnedEntries = new List<GameObject>();

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

        if (!args.Snapshot.Exists)
        {
            ClearEntries();
            return;
        }

        var list = new List<(string username, int score)>();

        foreach (DataSnapshot child in args.Snapshot.Children)
        {
            string username = child.Child("username").Value?.ToString() ?? "Unknown";
            int score = int.TryParse(child.Child("score").Value?.ToString(), out int s) ? s : 0;
            list.Add((username, score));
        }

        var ordered = list.OrderByDescending(x => x.score).Take(topCount).ToList();
        UpdateLeaderboardUI(ordered);
    }

    private void ClearEntries()
    {
        foreach (var go in spawnedEntries)
            Destroy(go);
        spawnedEntries.Clear();
    }

    private void UpdateLeaderboardUI(List<(string username, int score)> orderedList)
    {
        ClearEntries();

        foreach (var item in orderedList)
        {
            GameObject entryGO = Instantiate(entryPrefab, entryContainer);
            spawnedEntries.Add(entryGO);

            TMP_Text[] texts = entryGO.GetComponentsInChildren<TMP_Text>();
            if (texts.Length >= 2)
            {
                texts[0].text = item.username;
                texts[1].text = item.score.ToString();
            }
        }
    }
}
