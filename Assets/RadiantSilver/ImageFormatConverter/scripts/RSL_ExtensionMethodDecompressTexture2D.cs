using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class RSL_ExtensionMethodDecompressTexture2D
{   
        public static Texture2D decompressTexture(this Texture2D source)
        {
            Debug.Log("decompressing texture ("+source.width+" x "+source.height+")");
                       
            RenderTexture renderTex = RenderTexture.GetTemporary(
                        source.width,
                        source.height,
                        0,
                        RenderTextureFormat.Default,
                        RenderTextureReadWrite.Linear);

            Graphics.Blit(source, renderTex);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;

            Texture2D readableText = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);

            readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableText.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);
            return readableText;
        }
    }

