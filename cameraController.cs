using UnityEngine;
using UnityEngine.InputSystem;

public class cameraController : MonoBehaviour
{
    public GameObject mainCamera;
    public cameraStats selected;

    void Update()
    {
        if (Mouse.current.leftButton.isPressed && selected == null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity))
            {
                selected = hitInfo.collider.GetComponent<cameraStats>();
                if (selected != null)
                {
                    selected.camera.SetActive(true);
                    selected.controlledByPlayer = true;

                    mainCamera.SetActive(false);
                }
            }
        }
        else if (Keyboard.current.escapeKey.isPressed)
        {
            returnToMainCamera();
        }
    }

    public void returnToMainCamera()
    {
        if (selected == null) return; 

        mainCamera.SetActive(true);
        selected.camera.SetActive(false);
        selected.controlledByPlayer = false;
        selected = null;
    }
}