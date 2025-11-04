using System;
using System.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;

public class AuthMonitor : MonoBehaviour
{
    private void Start()
    {
        FirebaseAuth.DefaultInstance.StateChanged += OnAuthStateChanged;
        Debug.Log($"[AuthMonitor] Start - CurrentUser: {FirebaseAuth.DefaultInstance.CurrentUser?.UserId ?? "null"}");
    }

    private void OnAuthStateChanged(object sender, EventArgs e)
    {
        var cur = FirebaseAuth.DefaultInstance.CurrentUser;
        Debug.Log($"[AuthMonitor] StateChanged -> CurrentUser: {(cur != null ? cur.UserId : "null")}  displayName: {cur?.DisplayName}  email:{cur?.Email}");
        // Intentar leer token y loggear fallback
        _ = LogTokenInfo();
    }

    private async Task LogTokenInfo()
    {
        try
        {
            var user = FirebaseAuth.DefaultInstance.CurrentUser;
            if (user == null) { Debug.Log("[AuthMonitor] No user to get token"); return; }
            var token = await user.TokenAsync(false);
            Debug.Log($"[AuthMonitor] Token len={token?.Length ?? 0}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AuthMonitor] Token fetch failed: {ex}");
            try
            {
                var user = FirebaseAuth.DefaultInstance.CurrentUser;
                if (user == null) return;
                var token2 = await user.TokenAsync(true);
                Debug.Log($"[AuthMonitor] Token refresh len={token2?.Length ?? 0}");
            }
            catch (Exception e2)
            {
                Debug.LogError($"[AuthMonitor] Token refresh failed: {e2}");
            }
        }
    }

    private void OnDestroy()
    {
        try { FirebaseAuth.DefaultInstance.StateChanged -= OnAuthStateChanged; } catch { }
    }
}
