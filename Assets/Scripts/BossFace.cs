using UnityEngine;

public class BossFace : MonoBehaviour {
    public Renderer faceRenderer;

    public void SetFace(Texture2D faceTexture) {
        faceRenderer.material.mainTexture = faceTexture;
    }
}