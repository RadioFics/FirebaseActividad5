using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEngine.Rendering.GPUSort;

public class LeaderboardListener : MonoBehaviour
{
    private void Start()
    {
        FirebaseDatabase.DefaultInstance
            .GetReference("users")
            .ValueChanged += HandleValueChanged;
    }

    void HandleValueChanged(object sender, ValueChangedEventArgs args)
    {
        if(args.DatabaseError != null)
        {
            Debug.LogError("Firebase DB error: " + args.DatabaseError.Message);
            return;
        }

        DataSnapshot snapshot = args.Snapshot;

        var users = (Dictionary<string, object>)snapshot.Value;
        foreach (var userEntry in users)
        {
            var userData = (Dictionary<string, object>)userEntry.Value;
            Debug.Log(users["username"] + " | " + users[""]);
        }
    }
}
