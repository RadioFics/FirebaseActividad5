using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using System;

public class GameManager : MonoBehaviour
{
    [SerializeField] private GameObject gameOverScreen;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private float initialScrollSpeed;

    private int score;
    private float timer, scrollSpeed;

    public static GameManager Instance { get; private set; }

    private DatabaseReference mDatabaseRef;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }

        mDatabaseRef = FirebaseDatabase.DefaultInstance.RootReference;
    }

    private void Update()
    {
        UpdateScore();
        UpdateSpeed();
    }

    public void ShowGameOverScreen()
    {
        SaveScoreToFirebase(score);
        gameOverScreen.SetActive(true);
    }

    public void RestartScene()
    {
        try
        {
            FirebaseAuth.DefaultInstance.SignOut();
            Debug.Log("Usuario deslogueado por RestartScene.");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Error al hacer SignOut en RestartScene: " + ex.Message);
        }

        SceneManager.LoadScene("MainScene");
        gameOverScreen.SetActive(false);
        Time.timeScale = 1f;
    }

    private void UpdateScore()
    {
        int scorePerSecond = 10;

        timer += Time.deltaTime;
        score = (int)(timer * scorePerSecond);
        scoreText.text = string.Format("{0:000000}", score);
    }

    public float GetScrollSpeed()
    {
        return scrollSpeed;
    }

    public int GetScore()
    {
        return score;
    }

    private void UpdateSpeed()
    {
        float speedDivider = 10f;
        scrollSpeed = initialScrollSpeed + timer / speedDivider;
    }

    private void SaveScoreToFirebase(int newScore)
    {
        var currentUser = FirebaseAuth.DefaultInstance.CurrentUser;
        if (currentUser == null)
        {
            Debug.Log("SaveScoreToFirebase: no hay usuario autenticado, no se guarda el score.");
            return;
        }

        string uid = currentUser.UserId;
        var scoreRef = FirebaseDatabase.DefaultInstance.GetReference($"users/{uid}/score");

        scoreRef.GetValueAsync().ContinueWithOnMainThread(task => {
            if (task.IsFaulted)
            {
                Debug.LogError("Error leyendo score previo: " + task.Exception);

                scoreRef.SetValueAsync(newScore).ContinueWithOnMainThread(t2 => {
                    if (t2.IsCompleted) Debug.Log($"Score guardado (fallback) {newScore} para {uid}");
                });
            }
            else if (task.IsCompleted)
            {
                DataSnapshot snapshot = task.Result;
                int existingScore = 0;
                if (snapshot.Exists && snapshot.Value != null)
                {
                    int.TryParse(snapshot.Value.ToString(), out existingScore);
                }

                if (newScore > existingScore)
                {
                    scoreRef.SetValueAsync(newScore).ContinueWithOnMainThread(setTask => {
                        if (setTask.IsCompleted)
                        {
                            Debug.Log($"Nuevo highscore guardado: {newScore} (prev {existingScore}) para {uid}");
                        }
                        else if (setTask.IsFaulted)
                        {
                            Debug.LogError("Error guardando score: " + setTask.Exception);
                        }
                    });
                }
                else
                {
                    Debug.Log($"Score {newScore} no supera el existente {existingScore}, no se actualiza.");
                }
            }
        });
    }
}
