using UnityEngine;
using Firebase.Auth;
using Firebase.Extensions;
using TMPro;
using UnityEngine.UI;

public class ForgetPassword : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_InputField emailInputField;
    [SerializeField] private Button sendButton;
    [SerializeField] private TextMeshProUGUI feedbackText;

    private FirebaseAuth auth;

    void Start()
    {
        auth = FirebaseAuth.DefaultInstance;
        sendButton.onClick.AddListener(HandleSendButtonClicked);
    }

    private void HandleSendButtonClicked()
    {
        string email = emailInputField.text.Trim();

        if (string.IsNullOrEmpty(email))
        {
            ShowFeedback("Por favor, ingresa un correo válido.");
            return;
        }

        SendPasswordReset(email);
    }

    private void SendPasswordReset(string email)
    {
        auth.SendPasswordResetEmailAsync(email).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled)
            {
                ShowFeedback("El envío fue cancelado. Inténtalo nuevamente.");
                Debug.LogError("SendPasswordResetEmailAsync was canceled.");
                return;
            }
            if (task.IsFaulted)
            {
                ShowFeedback("No se pudo enviar el correo. Verifica el correo ingresado.");
                Debug.LogError("SendPasswordResetEmailAsync encountered an error: " + task.Exception);
                return;
            }

            ShowFeedback("Correo de recuperación enviado con éxito. Revisa tu bandeja de entrada.");
            Debug.Log("Password reset email sent successfully.");
        });
    }

    private void ShowFeedback(string message)
    {
        if (feedbackText != null)
        {
            feedbackText.text = message;
        }
    }
}
