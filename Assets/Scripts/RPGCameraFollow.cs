using UnityEngine;
using System.Collections;

/// <summary>
/// Smooth RPG-style camera that follows the player from a fixed angle.
/// Attach this to your Main Camera.
/// </summary>
public class RPGCameraFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The player transform to follow")]
    public Transform target;

    [Header("Camera Position")]
    [Tooltip("Offset from the target (e.g., (0, 10, -8) for top-down angled view)")]
    public Vector3 offset = new Vector3(0f, 10f, -8f);

    [Header("Smoothing")]
    [Tooltip("How smoothly the camera follows (lower = smoother, higher = snappier)")]
    [Range(0.01f, 1f)]
    public float smoothSpeed = 0.125f;

    [Tooltip("Use fixed update for physics-based movement")]
    public bool useFixedUpdate = false;

    [Header("Look At Target")]
    [Tooltip("Should camera always look at the target?")]
    public bool lookAtTarget = true;

    [Tooltip("Vertical offset for look-at point (aim at character's chest, not feet)")]
    public float lookAtHeightOffset = 1f;

    [Header("Zoom Settings")]
    [Tooltip("How much closer the camera moves when zooming in (multiplier, e.g., 0.5 = 50% closer)")]
    [Range(0.1f, 0.9f)]
    public float zoomInMultiplier = 0.6f;

    [Tooltip("How much farther the camera moves when running (multiplier, e.g., 1.3 = 30% farther)")]
    [Range(1.0f, 2.0f)]
    public float runZoomOutMultiplier = 1.3f;

    [Tooltip("How long the zoom transition takes")]
    public float zoomTransitionTime = 0.5f;

    [Tooltip("How smoothly the running zoom adjusts")]
    public float runZoomSmoothSpeed = 3f;

    // For smooth damp
    private Vector3 velocity = Vector3.zero;

    // Zoom state
    private Vector3 baseOffset;
    private Vector3 currentOffset;
    private Vector3 targetOffset;
    private bool isZooming = false;
    private bool isRunning = false;
    private Coroutine zoomCoroutine;

    private void Awake()
    {
        baseOffset = offset;
        currentOffset = offset;
        targetOffset = offset;
    }

    private void LateUpdate()
    {
        if (!useFixedUpdate)
        {
            FollowTarget();
        }
    }

    private void FixedUpdate()
    {
        if (useFixedUpdate)
        {
            FollowTarget();
        }
    }

    private void FollowTarget()
    {
        if (target == null)
        {
            // Try to find player if not assigned
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                return;
            }
        }

        // Smoothly transition offset for running zoom (only when not doing temporary zoom)
        if (!isZooming)
        {
            targetOffset = isRunning ? baseOffset * runZoomOutMultiplier : baseOffset;
            currentOffset = Vector3.Lerp(currentOffset, targetOffset, runZoomSmoothSpeed * Time.deltaTime);
        }

        // Calculate desired position using current offset (may be zoomed)
        Vector3 desiredPosition = target.position + currentOffset;

        // Smoothly move camera to desired position
        Vector3 smoothedPosition = Vector3.SmoothDamp(
            transform.position, 
            desiredPosition, 
            ref velocity, 
            smoothSpeed
        );

        transform.position = smoothedPosition;

        // Optionally look at target
        if (lookAtTarget)
        {
            Vector3 lookAtPoint = target.position + Vector3.up * lookAtHeightOffset;
            transform.LookAt(lookAtPoint);
        }
    }

    /// <summary>
    /// Set whether the character is running (for dynamic zoom)
    /// </summary>
    public void SetRunning(bool running)
    {
        isRunning = running;
    }

    /// <summary>
    /// Zoom in for a specified duration, then zoom back out
    /// </summary>
    /// <param name="duration">How long to stay zoomed in (default 3 seconds)</param>
    public void ZoomInTemporary(float duration = 3f)
    {
        if (zoomCoroutine != null)
        {
            StopCoroutine(zoomCoroutine);
        }
        zoomCoroutine = StartCoroutine(ZoomInOutRoutine(duration));
    }

    private IEnumerator ZoomInOutRoutine(float holdDuration)
    {
        isZooming = true;
        Vector3 zoomedOffset = baseOffset * zoomInMultiplier;

        // Zoom in
        float elapsed = 0f;
        Vector3 startOffset = currentOffset;
        while (elapsed < zoomTransitionTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / zoomTransitionTime;
            t = t * t * (3f - 2f * t); // Smoothstep
            currentOffset = Vector3.Lerp(startOffset, zoomedOffset, t);
            yield return null;
        }
        currentOffset = zoomedOffset;

        // Hold zoom
        yield return new WaitForSeconds(holdDuration);

        // Zoom out
        elapsed = 0f;
        startOffset = currentOffset;
        while (elapsed < zoomTransitionTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / zoomTransitionTime;
            t = t * t * (3f - 2f * t); // Smoothstep
            currentOffset = Vector3.Lerp(startOffset, baseOffset, t);
            yield return null;
        }
        currentOffset = baseOffset;

        isZooming = false;
        zoomCoroutine = null;
    }

    /// <summary>
    /// Instantly snap camera to target position (useful for initialization or teleports)
    /// </summary>
    public void SnapToTarget()
    {
        if (target != null)
        {
            transform.position = target.position + currentOffset;
            if (lookAtTarget)
            {
                Vector3 lookAtPoint = target.position + Vector3.up * lookAtHeightOffset;
                transform.LookAt(lookAtPoint);
            }
        }
    }

    private void OnValidate()
    {
        // Preview in editor when values change
        if (target != null && !Application.isPlaying)
        {
            transform.position = target.position + offset;
            if (lookAtTarget)
            {
                Vector3 lookAtPoint = target.position + Vector3.up * lookAtHeightOffset;
                transform.LookAt(lookAtPoint);
            }
        }
    }
}

