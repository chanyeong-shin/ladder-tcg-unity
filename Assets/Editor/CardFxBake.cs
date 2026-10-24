using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace Breach.EditorTools {
 public static class CardFxBake {
  public static Sprite Bake(GameObject prefab,string name){
   Directory.CreateDirectory("Assets/Art/Generated");
   const int width=504,height=806;
   var texture=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
   var cameraObject=new GameObject("임시 카드 촬영 카메라",typeof(Camera));var camera=cameraObject.GetComponent<Camera>();camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.targetTexture=texture;camera.orthographic=true;camera.orthographicSize=height/2f;
   var canvasObject=new GameObject("임시 카드 촬영",typeof(RectTransform),typeof(Canvas));canvasObject.layer=30;var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
   var card=Object.Instantiate(prefab,canvasObject.transform);card.SetActive(true);SetLayer(card.transform);var r=(RectTransform)card.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=Vector2.zero;r.localScale=Vector3.one*Mathf.Min(width/r.sizeDelta.x,height/r.sizeDelta.y);
   Canvas.ForceUpdateCanvases();foreach(var rect in card.GetComponentsInChildren<RectTransform>())rect.ForceUpdateRectTransforms();foreach(var graphic in card.GetComponentsInChildren<Graphic>()){graphic.SetAllDirty();graphic.Rebuild(CanvasUpdate.PreRender);graphic.canvasRenderer.cull=false;}foreach(var text in card.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate(true,true);Canvas.ForceUpdateCanvases();camera.Render();
   var old=RenderTexture.active;RenderTexture.active=texture;var image=new Texture2D(width,height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();string path="Assets/Art/Generated/"+name+".png";File.WriteAllBytes(path,image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;
   Object.DestroyImmediate(image);Object.DestroyImmediate(canvasObject);Object.DestroyImmediate(cameraObject);texture.Release();Object.DestroyImmediate(texture);
   AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
  }
  static void SetLayer(Transform node){node.gameObject.layer=30;foreach(Transform child in node)SetLayer(child);}
 }
}
