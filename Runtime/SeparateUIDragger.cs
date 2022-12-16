using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;

namespace Game.Views
{
    public class SeparateUIDragger : MonoBehaviour
    {
        [SerializeField] private Camera _UICamera;
        [SerializeField] private RawImage _imageToSet;

        [NonSerialized] private RenderTexture Texture;

        private void OnEnable()
        {
            Texture = new RenderTexture(Screen.width,
                Screen.height,
                GraphicsFormat.R8G8B8A8_UNorm,
                GraphicsFormat.D32_SFloat_S8_UInt);

            _UICamera.targetTexture = Texture;
            _UICamera.enabled = true;
            _imageToSet.texture = Texture;
            _imageToSet.enabled = true;
        }

        private void OnDisable()
        {
            if (_imageToSet != null)
            {
                _imageToSet.enabled = false;
                _imageToSet.texture = Texture2D.whiteTexture;
            }

            if (_UICamera != null)
            {
                _UICamera.enabled = false;
                _UICamera.targetTexture = null;
            }

            if (Texture != null)
            {
                Texture.DiscardContents(true, true);
                Texture.Release();
                Destroy(Texture);
            }

            Texture = null;
        }
    }
}