using Firebase.Auth;
using Firebase.Database;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ButtonSaveScore : MonoBehaviour
{
    [SerializeField]
    private Button _saveScoreButton;

    [SerializeField]
    private TMP_InputField _scoreInputField;

    private DatabaseReference mDatabaseRef;

    void Reset()
    {
        _saveScoreButton = GetComponent<Button>();
        _scoreInputField = GameObject.Find("InputFieldScore").GetComponent<TMP_InputField>();
    }
    void Start()
    {
        _saveScoreButton.onClick.AddListener(HandleSaveScoreButtonClicked);
        mDatabaseRef = FirebaseDatabase.DefaultInstance.RootReference;
    }

    private void HandleSaveScoreButtonClicked()
    {
        var currentUser = FirebaseAuth.DefaultInstance.CurrentUser;
        if (currentUser != null)
        {
            int score = int.Parse(_scoreInputField.text);
            mDatabaseRef.Child("users").Child(currentUser.UserId).Child("score").SetValueAsync(score);
        }
    }
}
