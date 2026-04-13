using UnityEngine;
using UnityEngine.InputSystem;
/// <summary>
/// Handles all player inputs and exposes them for other components to read.
/// Acts as an abstraction layer between the Unity Input System and gameplay logic.
/// </summary>
public class PlayerInputHandler : MonoBehaviour, PlayerInputActions.IPlayerActions
{
    private PlayerInputActions _inputActions;
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool JumpReleased { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool IsCrouching { get; private set; }
    public bool InteractPressed { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the grab button is currently being held.
    /// </summary>
    public bool IsGrabbing { get; private set; }
    /// <summary>
    /// Gets a value indicating whether the throw button was pressed this frame.
    /// </summary>
    public bool ThrowPressed { get; private set; }
    /// <summary>
    /// Gets a value indicating whether the slow walk toggle is active.
    /// </summary>
    public bool IsWalking { get; private set; }
    private void Awake()
    {
        try
        {
            _inputActions = new PlayerInputActions();
            _inputActions.Player.SetCallbacks(this);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerInputHandler] Failed to initialize PlayerInputActions: {e.Message}");
        }
    }
    private void OnEnable()
    {
        if (_inputActions != null)
        {
            _inputActions.Player.Enable();
        }
    }
    private void OnDisable()
    {
        if (_inputActions != null)
        {
            _inputActions.Player.Disable();
        }
    }
    private void OnDestroy()
    {
        _inputActions?.Dispose();
    }
    private void LateUpdate()
    {
        InteractPressed = false;
        ThrowPressed = false;
        JumpReleased = false;
    }
    /// <summary>
    /// Consumes the jump input, ensuring it is only processed once.
    /// </summary>
    public void ConsumeJump() => JumpPressed = false;
    public void OnMove(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();
    }
    public void OnLook(InputAction.CallbackContext context)
    {
        LookInput = context.ReadValue<Vector2>();
    }
    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.started) JumpPressed = true;
        if (context.canceled) JumpReleased = true;
    }
    public void OnSprint(InputAction.CallbackContext context)
    {
        if (context.started) IsSprinting = true;
        else if (context.canceled) IsSprinting = false;
    }
    public void OnCrouch(InputAction.CallbackContext context)
    {
        if (context.started) IsCrouching = true;
        else if (context.canceled) IsCrouching = false;
    }
    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.started) InteractPressed = true;
    }
    public void OnGrab(InputAction.CallbackContext context)
    {
        if (context.performed) IsGrabbing = true;
        else if (context.canceled) IsGrabbing = false;
    }
    public void OnThrow(InputAction.CallbackContext context)
    {
        if (context.started) ThrowPressed = true;
    }
    public void OnWalk(InputAction.CallbackContext context)
    {
        if (context.started) IsWalking = !IsWalking; 
    }
}