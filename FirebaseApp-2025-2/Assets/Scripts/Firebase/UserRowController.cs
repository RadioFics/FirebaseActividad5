using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Componente que controla una fila de usuario en la lista de online.
/// Debe añadirse al prefab UserRow y asignar references en el Inspector.
/// </summary>
public class UserRowController : MonoBehaviour
{
    [Header("UI references")]
    public TMP_Text usernameText;
    public Button sendFriendButton; // botón "Enviar solicitud"
    // opcional: texto del botón
    private TMP_Text sendButtonLabel;

    // Datos del usuario en esta fila
    private string userId;
    private string username;
    private FriendRequestManager friendRequestManager;

    // Estado local
    private bool isSelf;
    private bool isFriend;
    private bool isPending;

    public void Init(string uid, string uname, FriendRequestManager frm, bool self, bool friend, bool pending)
    {
        userId = uid;
        username = uname;

        // asignar gestor de solicitudes: usar el provisto o intentar autolocalizar uno en la escena
        if (frm != null) friendRequestManager = frm;
        else
        {
            // Intento de autolocalización (usar la API recomendada según la versión de Unity)
            friendRequestManager = UnityEngine.Object.FindAnyObjectByType<FriendRequestManager>();
            if (friendRequestManager == null)
            {
                Debug.LogWarning("FriendRequestManager no asignado en UserRowController y no se encontró instancia en la escena.");
            }
        }

        isSelf = self;
        isFriend = friend;
        isPending = pending;

        if (usernameText != null)
        {
            usernameText.text = !string.IsNullOrEmpty(username) ? username : userId;
            if (isSelf) usernameText.text += " (tú)";
        }

        if (sendFriendButton != null)
        {
            sendButtonLabel = sendFriendButton.GetComponentInChildren<TMP_Text>();
            sendFriendButton.onClick.RemoveAllListeners();
            sendFriendButton.onClick.AddListener(() => OnSendFriendClicked());
        }

        ApplyButtonState();
    }

    // Expuesto para que OnlineUsersManager actualice el estado cuando cambian friends/pending
    public void UpdateState(bool self, bool friend, bool pending)
    {
        isSelf = self;
        isFriend = friend;
        isPending = pending;
        ApplyButtonState();
    }

    private void ApplyButtonState()
    {
        if (sendFriendButton == null) return;

        // No mostrar botón si es el propio usuario o ya son amigos
        if (isSelf || isFriend)
        {
            sendFriendButton.gameObject.SetActive(false);
            return;
        }

        // Si no hay FriendRequestManager, mostrar botón deshabilitado para evitar clicks que fallan
        if (friendRequestManager == null)
        {
            sendFriendButton.gameObject.SetActive(true);
            sendFriendButton.interactable = false;
            if (sendButtonLabel != null) sendButtonLabel.text = "No disponible";
            return;
        }

        // Mostrar botón pero desactivado si hay pendiente
        if (isPending)
        {
            sendFriendButton.gameObject.SetActive(true);
            sendFriendButton.interactable = false;
            if (sendButtonLabel != null) sendButtonLabel.text = "Enviado";
            return;
        }

        // Default: botón visible y activo
        sendFriendButton.gameObject.SetActive(true);
        sendFriendButton.interactable = true;
        if (sendButtonLabel != null) sendButtonLabel.text = "Agregar";
    }

    private async void OnSendFriendClicked()
    {
        if (friendRequestManager == null)
        {
            Debug.LogWarning("FriendRequestManager no asignado en UserRowController.");
            return;
        }
        if (string.IsNullOrEmpty(userId)) return;

        // proteger contra dobles clicks: desactivar inmediatamente
        sendFriendButton.interactable = false;
        if (sendButtonLabel != null) sendButtonLabel.text = "Enviando...";

        bool success = false;
        try
        {
            var task = friendRequestManager.SendFriendRequest(userId, username);
            if (task == null)
            {
                Debug.LogError("SendFriendRequest retornó null Task.");
            }
            else
            {
                success = await task;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error enviando solicitud: " + ex);
        }

        if (success)
        {
            isPending = true;
            ApplyButtonState();
            Debug.Log($"Solicitud enviada correctamente a {userId}");
        }
        else
        {
            // fallo -> reactivar botón para reintentar y mostrar texto de error
            sendFriendButton.interactable = true;
            if (sendButtonLabel != null) sendButtonLabel.text = "Error - Reintentar";
            Debug.LogWarning($"Fallo al enviar solicitud a {userId}. Revisa reglas y logs de Firebase.");
        }
    }

}
