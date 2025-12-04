using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerMovement : MonoBehaviour
{
  [Header("Movement")]
  private float moveSpeed;
  public float walkSpeed;
  public float sprintSpeed;

  public float groundDrag;

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

  [Header("Slope Handling")]
  public float maxSlopeAngle;
  private RaycastHit slopeHit;
  private bool exitingSlope;


  public Transform orientation;

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
    sliding
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
    // ground check
    grounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, groundLayer);

    MyInput();
    SpeedControl();
    StateHandler();

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

  private void MyInput()
  {
    horizontalInput = Input.GetAxisRaw("Horizontal");
    verticalInput = Input.GetAxisRaw("Vertical");

    // when to jump
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

  private void StateHandler()
  {
    // Mode - Sliding
    if (sliding)
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

  private void MovePlayer()
  {
    // calculate movement direction
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

  private void SpeedControl()
  {
    // limiting speed on slope
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

  private void Jump()
  {
    exitingSlope = true;

    // reset y velocity
    rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

    rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
  }
  private void ResetJump()
  {
    readyToJump = true;

    exitingSlope = false;
  }

  private bool OnSlope()
  {
    if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight * 0.5f + 0.3f))
    {
      float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
      return angle < maxSlopeAngle && angle != 0;
    }

    return false;
  }

  private Vector3 GetSlopeMoveDirection()
  {
    return Vector3.ProjectOnPlane(moveDirection, slopeHit.normal).normalized;
  }

  public bool CheckCeiling()
  {
    if (Physics.Raycast(transform.position, Vector3.up, out ceilingHit, startYScale * 0.5f + 0.2f))
    {
      return true;
    }
    return false;
  }

  private void StartSlide()
  {
    sliding = true;
    transform.localScale = new Vector3(transform.localScale.x, slideYScale, transform.localScale.z);
    rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
    slideTimer = maxSlideTime;
  }

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