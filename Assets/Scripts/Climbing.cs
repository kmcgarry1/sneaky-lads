using UnityEngine;

/// <summary>
/// Wall climbing mechanic with stamina timer and climb jumping
/// Allows player to climb vertical surfaces while looking at them and pressing forward
/// Includes jump-off mechanic with limited mid-air climb jumps
/// </summary>
public class Climbing : MonoBehaviour
{
  [Header("References")]
  public Transform orientation;
  public Rigidbody rb;
  public LayerMask wallLayer;
  public PlayerMovement pm;

  [Header("Climbing")]
  public float climbSpeed;
  public float maxClimbTime;
  private float climbTimer;

  private bool climbing;

  [Header("ClimbJumping")]
  public float climbJumpForce;
  public float climbJumpBackForce;

  public KeyCode climbJumpKey = KeyCode.Space;
  public int climbJumps;
  private int climbJumpsLeft;

  [Header("Detection")]
  public float detectionLength;
  public float sphereCastRadius;
  public float maxWallLookAngle;

  private float wallLookAngle;

  private RaycastHit frontWallHit;
  private bool wallFront;

  private Transform lastWall;
  private Vector3 lastWallNormal;
  public float minWallNormalAngleChange;

  [Header("Exiting")]
  public bool exitingWall;
  public float exitWallTime;
  private float exitWallTimer;

  // Start is called once before the first execution of Update after the MonoBehaviour is created
  void Start()
  {

  }

  void Update()
  {
    WallCheck();       // Detect walls in front of player
    StateMachine();    // Handle climbing state transitions

    // Apply climbing movement if actively climbing
    if (climbing && !exitingWall)
    {
      ClimbingMovement();
    }
  }

  /// <summary>
  /// Manages climbing state based on wall detection, timer, and input
  /// Handles starting, stopping, and climb jumping
  /// </summary>
  private void StateMachine()
  {
    if (wallFront && wallLookAngle < maxWallLookAngle && Input.GetKey(KeyCode.W) && !exitingWall)
    {
      if (!climbing && climbTimer > 0)
      {
        StartClimbing();
      }
      if (climbTimer > 0)
      {
        climbTimer -= Time.deltaTime;
      }
      if (climbTimer <= 0)
      {
        StopClimbing();
      }
    }
    else if (exitingWall)
    {
      if (climbing)
      {
        StopClimbing();
      }

      if (exitWallTimer > 0)
      {
        exitWallTimer -= Time.deltaTime;
      }
      if (exitWallTimer <= 0)
      {
        exitingWall = false;
      }
    }
    else
    {
      if (climbing)
      {
        StopClimbing();
      }
    }



    if (wallFront && Input.GetKeyDown(climbJumpKey) && climbJumpsLeft > 0)
    {
      ClimbJump();
    }

  }

  /// <summary>
  /// Detects walls in front of player using sphere cast
  /// Resets climb timer when grounded or on new wall
  /// </summary>
  private void WallCheck()
  {
    // Check if player switched to a different wall or changed angle significantly
    bool newWall = frontWallHit.transform != lastWall || Vector3.Angle(frontWallHit.normal, lastWallNormal) > minWallNormalAngleChange;
    // Cast sphere forward to detect walls
    wallFront = Physics.SphereCast(transform.position, sphereCastRadius, orientation.forward, out frontWallHit, detectionLength, wallLayer);
    // Calculate angle between player look direction and wall
    wallLookAngle = Vector3.Angle(orientation.forward, -frontWallHit.normal);
    if (pm.grounded)
    {
      climbTimer = maxClimbTime;
    }

    if ((!wallFront || newWall) && !pm.grounded)
    {
      climbJumpsLeft = climbJumps;
      climbTimer = maxClimbTime / 2;
    }
  }

  /// <summary>
  /// Initiates climbing and saves wall reference
  /// </summary>
  private void StartClimbing()
  {
    pm.climbing = true;
    climbing = true;
    climbTimer = maxClimbTime;
    lastWall = frontWallHit.transform;
    lastWallNormal = frontWallHit.normal;
  }

  /// <summary>
  /// Stops climbing and updates player state
  /// </summary>
  private void StopClimbing()
  {
    pm.climbing = false;
    climbing = false;
  }

  /// <summary>
  /// Applies upward velocity while climbing
  /// Stops when timer expires
  /// </summary>
  private void ClimbingMovement()
  {
    rb.linearVelocity = new Vector3(rb.linearVelocity.x, climbSpeed, rb.linearVelocity.z);
    climbTimer -= Time.deltaTime;

    if (climbTimer <= 0)
    {
      StopClimbing();
    }
  }

  /// <summary>
  /// Launches player up and away from wall
  /// Consumes one climb jump charge
  /// </summary>
  private void ClimbJump()
  {
    exitingWall = true;
    exitWallTimer = exitWallTime;
    Vector3 forceToApply = transform.up * climbJumpForce + frontWallHit.normal * climbJumpBackForce;

    rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
    rb.AddForce(forceToApply, ForceMode.Impulse);
    climbJumpsLeft--;
    StopClimbing();
  }
}
