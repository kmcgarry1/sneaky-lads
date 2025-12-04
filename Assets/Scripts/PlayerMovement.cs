using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Main player movement controller handling walking, sprinting, crouching, sliding, jumping, and climbing
/// Uses physics-based movement with Rigidbody for realistic interaction with slopes and surfaces
/// </summary>
public class PlayerMovement : MonoBehaviour
{
  [Header("Movement")]
  private float moveSpeed;
  public float walkSpeed;
  public float sprintSpeed;

  public float groundDrag;
  public float climbSpeed;

  [Header("Jumping")]
  public float jumpForce;
  public float jumpCooldown;
  public float airMultiplier;
  bool readyToJump;

  [Header("Crouching")]
  public float crouchSpeed;
  public float crouchYScale;
  private float startYScale;
  private RaycastHit ceilingHit;

  [Header("Sliding")]
  public float maxSlideTime;
  public float slideForce;
  public float slideYScale;
  private float slideTimer;

  [Header("Keybinds")]
  public KeyCode jumpKey = KeyCode.Space;
  public KeyCode sprintKey = KeyCode.LeftShift;
  public KeyCode crouchKey = KeyCode.LeftControl;

  [Header("Ground Check")]
  public float playerHeight;
  public LayerMask groundLayer;
  public bool grounded;

  [HideInInspector]
  public bool checkUncrouch;

  private bool sliding;
  private bool sprinting;
  public bool climbing;

  [Header("Slope Handling")]
  public float maxSlopeAngle;
  private RaycastHit slopeHit;
  private bool exitingSlope;

  [Header("References")]
  public Transform orientation;
  public Climbing climbingScript;

  float horizontalInput;
  float verticalInput;

  Vector3 moveDirection;

  Rigidbody rb;

  public MovementState state;
  public enum MovementState
  {
    walking,
    sprinting,
    crouching,
    air,
    sliding,
    climbing
  }

  private void Start()
  {
    rb = GetComponent<Rigidbody>();
    rb.freezeRotation = true;

    readyToJump = true;

    startYScale = transform.localScale.y;
  }

  private void Update()
  {
    // Check if player is touching the ground by raycasting downward
    grounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, groundLayer);

    MyInput();        // Process player input
    SpeedControl();   // Limit player velocity
    StateHandler();   // Determine current movement state

    // check if we can uncrouch
    if (checkUncrouch)
    {
      if (!CheckCeiling())
      {
        transform.localScale = new Vector3(transform.localScale.x, startYScale, transform.localScale.z);
        rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
        checkUncrouch = false;
      }
    }

    // slide timer and momentum check
    if (sliding)
    {
      slideTimer -= Time.deltaTime;

      // check horizontal velocity (momentum)
      Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

      if (slideTimer <= 0 || flatVel.magnitude < 0.5f)
        StopSlide();
    }

    // handle drag
    if (grounded)
      rb.linearDamping = groundDrag;
    else
      rb.linearDamping = 0;
  }

  private void FixedUpdate()
  {
    MovePlayer();
  }

  /// <summary>
  /// Handles all player input for movement actions (jump, crouch, slide)
  /// </summary>
  private void MyInput()
  {
    // Get WASD or arrow key input
    horizontalInput = Input.GetAxisRaw("Horizontal");
    verticalInput = Input.GetAxisRaw("Vertical");

    // Jump when grounded and ready
    if (Input.GetKey(jumpKey) && readyToJump && grounded)
    {
      readyToJump = false;

      Jump();

      Invoke(nameof(ResetJump), jumpCooldown);
    }

    // start crouch
    if (Input.GetKeyDown(crouchKey) && !sliding)
    {
      transform.localScale = new Vector3(transform.localScale.x, crouchYScale, transform.localScale.z);
      rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
      checkUncrouch = false;
    }

    // stop crouch
    if (Input.GetKeyUp(crouchKey) && !sliding)
    {
      if (!CheckCeiling())
      {
        transform.localScale = new Vector3(transform.localScale.x, startYScale, transform.localScale.z);
      }
      else
      {
        checkUncrouch = true;
      }
    }

    // start slide
    if (Input.GetKeyDown(crouchKey) && grounded && sprinting && (horizontalInput != 0 || verticalInput != 0))
    {
      StartSlide();
    }
  }

  /// <summary>
  /// Determines current movement state and sets appropriate move speed
  /// Priority: Climbing > Sliding > Crouching > Sprinting > Walking > Air
  /// </summary>
  private void StateHandler()
  {
    // Mode - Climbing (highest priority)
    if (climbing)
    {
      state = MovementState.climbing;
      moveSpeed = climbSpeed;
    }
    // Mode - Sliding (locked in once started)
    else if (sliding)
    {
      state = MovementState.sliding;

      if (OnSlope() && rb.linearVelocity.y < 0.1f)
        moveSpeed = slideForce;
      else
        moveSpeed = sprintSpeed;
    }

    // Mode - Crouching
    else if (Input.GetKey(crouchKey))
    {
      state = MovementState.crouching;
      moveSpeed = crouchSpeed;
      sprinting = false;
    }

    // Mode - Sprinting
    else if (grounded && Input.GetKey(sprintKey))
    {
      state = MovementState.sprinting;
      moveSpeed = sprintSpeed;
      sprinting = true;
    }

    // Mode - Walking
    else if (grounded)
    {
      state = MovementState.walking;
      moveSpeed = walkSpeed;
      sprinting = false;
    }

    // Mode - Air
    else
    {
      state = MovementState.air;
      sprinting = false;
    }
  }

  /// <summary>
  /// Applies physics forces to move the player based on current state
  /// Called in FixedUpdate for consistent physics calculations
  /// </summary>
  private void MovePlayer()
  {
    // Don't move while exiting a wall climb
    if (climbingScript.exitingWall)
    { return; }

    // Convert input to world-space movement direction
    moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;

    // sliding
    if (sliding)
    {
      if (OnSlope())
      {
        rb.AddForce(GetSlopeMoveDirection() * slideForce, ForceMode.Force);

        if (rb.linearVelocity.y > 0)
          rb.AddForce(Vector3.down * 80f, ForceMode.Force);
      }
      else
      {
        rb.AddForce(moveDirection.normalized * slideForce, ForceMode.Force);
      }
    }

    // on slope
    else if (OnSlope() && !exitingSlope)
    {
      rb.AddForce(GetSlopeMoveDirection() * moveSpeed * 20f, ForceMode.Force);

      if (rb.linearVelocity.y > 0)
        rb.AddForce(Vector3.down * 80f, ForceMode.Force);
    }

    // on ground
    else if (grounded)
      rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);

    // in air
    else if (!grounded)
      rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);

    // turn gravity off while on slope
    rb.useGravity = !OnSlope();
  }

  /// <summary>
  /// Limits player velocity to prevent exceeding the current move speed
  /// Handles both slope and flat ground velocity capping
  /// </summary>
  private void SpeedControl()
  {
    // Limit total velocity on slopes
    if (OnSlope() && !exitingSlope)
    {
      if (rb.linearVelocity.magnitude > moveSpeed)
        rb.linearVelocity = rb.linearVelocity.normalized * moveSpeed;
    }

    // limiting speed on ground or in air
    else
    {
      Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

      // limit velocity if needed
      if (flatVel.magnitude > moveSpeed)
      {
        Vector3 limitedVel = flatVel.normalized * moveSpeed;
        rb.linearVelocity = new Vector3(limitedVel.x, rb.linearVelocity.y, limitedVel.z);
      }
    }
  }

  /// <summary>
  /// Executes a jump by resetting vertical velocity and applying upward force
  /// </summary>
  private void Jump()
  {
    exitingSlope = true;

    // Reset y velocity to ensure consistent jump height
    rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

    rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
  }
  private void ResetJump()
  {
    readyToJump = true;

    exitingSlope = false;
  }

  /// <summary>
  /// Checks if player is standing on a slope
  /// </summary>
  /// <returns>True if on a slope within the max angle threshold</returns>
  private bool OnSlope()
  {
    if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight * 0.5f + 0.3f))
    {
      float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
      return angle < maxSlopeAngle && angle != 0;
    }

    return false;
  }

  /// <summary>
  /// Projects the movement direction onto the slope surface
  /// Ensures player moves along the slope instead of into it
  /// </summary>
  private Vector3 GetSlopeMoveDirection()
  {
    return Vector3.ProjectOnPlane(moveDirection, slopeHit.normal).normalized;
  }

  /// <summary>
  /// Checks if there's a ceiling above the player
  /// Used to prevent standing up from crouch/slide when blocked
  /// </summary>
  public bool CheckCeiling()
  {
    if (Physics.Raycast(transform.position, Vector3.up, out ceilingHit, startYScale * 0.5f + 0.2f))
    {
      return true;
    }
    return false;
  }

  /// <summary>
  /// Initiates slide by shrinking player and starting timer
  /// </summary>
  private void StartSlide()
  {
    sliding = true;
    transform.localScale = new Vector3(transform.localScale.x, slideYScale, transform.localScale.z);
    rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
    slideTimer = maxSlideTime;
  }

  /// <summary>
  /// Ends slide and returns player to standing height (if no ceiling)
  /// </summary>
  private void StopSlide()
  {
    sliding = false;

    if (!CheckCeiling())
    {
      transform.localScale = new Vector3(transform.localScale.x, startYScale, transform.localScale.z);
      rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
    }
    else
    {
      checkUncrouch = true;
    }
  }
}