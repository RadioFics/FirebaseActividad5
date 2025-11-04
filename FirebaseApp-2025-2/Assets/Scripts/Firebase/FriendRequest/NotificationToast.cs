using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NotificationToast : MonoBehaviour
{
    public TextMeshProUGUI messageText;
    public CanvasGroup canvasGroup;
    [Tooltip("Tiempo de cross-fade al aparecer y desaparecer")]
    public float fadeTime = 0.25f;

    private Coroutine autoHideCoroutine;

    private void Reset()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        messageText = GetComponentInChildren<TextMeshProUGUI>();
    }

    public void SetText(string text)
    {
        if (messageText != null) messageText.text = text;
    }

    public void ShowAndAutoHide(float totalDuration)
    {
        if (autoHideCoroutine != null) StopCoroutine(autoHideCoroutine);
        autoHideCoroutine = StartCoroutine(ShowAndHideCoroutine(totalDuration));
    }

    private IEnumerator ShowAndHideCoroutine(float total)
    {
        // appear
        if (canvasGroup != null)
        {
            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                canvasGroup.alpha = Mathf.Clamp01(t / fadeTime);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        // wait (minus fade out)
        float wait = Mathf.Max(0f, total - fadeTime);
        yield return new WaitForSeconds(wait);

        // fade out
        if (canvasGroup != null)
        {
            float t = 0f;
            float startAlpha = canvasGroup.alpha;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t / fadeTime);
                yield return null;
            }
            canvasGroup.alpha = 0f;
        }

        Destroy(gameObject);
    }
}
