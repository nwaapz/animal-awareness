using UnityEngine;

/// <summary>
/// Shows a sprite/arrow indicator on the ground where the mouse is pointing.
/// Hides the system cursor and replaces it with a world-space indicator.
/// </summary>
public class GroundCursorIndicator : MonoBehaviour
{
    [Header("Cursor Settings")]
    [Tooltip("The sprite/object to show at the mouse position on ground")]
    public GameObject cursorPrefab;

    [Tooltip("Layer mask for ground detection")]
    public LayerMask groundLayer;

    [Tooltip("Offset above ground to prevent z-fighting")]
    public float heightOffset = 0.05f;

    [Header("Rotation")]
    [Tooltip("Should the cursor rotate to face the player?")]
    public bool rotateTowardsPlayer = true;

    [Tooltip("The player transform (auto-finds if tagged 'Player')")]
    public Transform player;

    [Header("Smoothing")]
    [Tooltip("How smoothly the cursor follows mouse")]
    public float smoothSpeed = 20f;

    // Runtime
    private GameObject cursorInstance;
    private Camera mainCamera;
    private Vector3 targetPosition;

    private void Awake()
    {
        mainCamera = Camera.main;

        // Hide system cursor
        Cursor.visible = false;

        // Create cursor instance
        if (cursorPrefab != null)
        {
            cursorInstance = Instantiate(cursorPrefab);
            cursorInstance.name = "GroundCursor";
        }
        else
        {
            Debug.LogWarning("GroundCursorIndicator: No cursor prefab assigned!");
        }

        // Find player if not assigned
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }
    }

    private void Update()
    {
        if (cursorInstance == null || mainCamera == null) return;

        UpdateCursorPosition();
        UpdateCursorRotation();
    }

    private void UpdateCursorPosition()
    {
        Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
        {
            targetPosition = hit.point + Vector3.up * heightOffset;

            // Smooth movement
            cursorInstance.transform.position = Vector3.Lerp(
                cursorInstance.transform.position,
                targetPosition,
                smoothSpeed * Time.deltaTime
            );
        }
    }

    private void UpdateCursorRotation()
    {
        if (!rotateTowardsPlayer || player == null) return;

        Vector3 directionToPlayer = player.position - cursorInstance.transform.position;
        directionToPlayer.y = 0;

        if (directionToPlayer.sqrMagnitude > 0.001f)
        {
            // Arrow points towards player
            Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
            cursorInstance.transform.rotation = targetRotation;
        }
    }

    private void OnDestroy()
    {
        // Show cursor again when this script is destroyed
        Cursor.visible = true;

        if (cursorInstance != null)
        {
            Destroy(cursorInstance);
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // Keep cursor hidden when window is focused
        Cursor.visible = !hasFocus;
    }
}
