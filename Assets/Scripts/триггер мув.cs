using UnityEngine;

public class CameraTriggerZone : MonoBehaviour
{
    [Header("Camera")]
    public Transform cameraTransform;

    [Header("New Position (only checked axes will change)")]
    public bool changeX = false;
    public bool changeY = false;
    public bool changeZ = false;

    public float newX = 0f;
    public float newY = 0f;
    public float newZ = 0f;

    [Header("Smooth")]
    public bool smoothMove = false;
    public float duration = 1f;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float elapsed = 0f;
    private bool isMoving = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (cameraTransform == null) return;

        Vector3 current = cameraTransform.localPosition;

        targetPosition = new Vector3(
            changeX ? newX : current.x,
            changeY ? newY : current.y,
            changeZ ? newZ : current.z
        );

        if (smoothMove)
        {
            startPosition = cameraTransform.localPosition;
            elapsed = 0f; // ← сбрасываем таймер, даже если анимация уже шла
            isMoving = true;
        }
        else
        {
            cameraTransform.localPosition = targetPosition;
        }
    }

    private void Update()
    {
        if (!isMoving || cameraTransform == null) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        cameraTransform.localPosition = Vector3.Lerp(startPosition, targetPosition, t);

        if (t >= 1f)
            isMoving = false;
    }
}