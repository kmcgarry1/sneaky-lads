using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple script to keep the camera object at a specific position
/// Follows a target transform (usually positioned at the player's head)
/// </summary>
public class MoveCamera : MonoBehaviour
{
  public Transform cameraPosition;

  void Update()
  {
    // Match the camera position to the target every frame
    transform.position = cameraPosition.position;
  }
}
