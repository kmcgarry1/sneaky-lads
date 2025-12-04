using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// First-person camera controller
/// Handles mouse look with sensitivity settings and rotation clamping
/// </summary>
public class PlayerCam : MonoBehaviour
{
  public float sensX;
  public float sensY;
  public Transform orientation;

  float xRotation;
  float yRotation;
  // Start is called once before the first execution of Update after the MonoBehaviour is created
  void Start()
  {
    Cursor.lockState = CursorLockMode.Locked;
    Cursor.visible = false;
  }

  void Update()
  {
    // Get mouse input and apply sensitivity
    float mouseX = Input.GetAxisRaw("Mouse X") * Time.deltaTime * sensX;
    float mouseY = Input.GetAxisRaw("Mouse Y") * Time.deltaTime * sensY;

    // Accumulate rotation values
    yRotation += mouseX;  // Horizontal rotation (left/right)
    xRotation -= mouseY;  // Vertical rotation (up/down)

    // Clamp vertical rotation to prevent flipping
    xRotation = Mathf.Clamp(xRotation, -90f, 90f);

    // Apply rotation to camera
    transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
    // Update player body orientation (only horizontal)
    orientation.rotation = Quaternion.Euler(0, yRotation, 0);
  }
}
