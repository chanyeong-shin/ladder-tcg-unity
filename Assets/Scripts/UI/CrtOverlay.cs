using UnityEngine;
using UnityEngine.UI;
namespace Breach.UI {
 public sealed class CrtOverlay:MonoBehaviour {
  public Shader shader;
  [Range(0,1)]public float strength=.45f;
  Material runtimeMaterial;
  void Awake(){var image=GetComponent<RawImage>();image.raycastTarget=false;runtimeMaterial=new Material(shader);runtimeMaterial.SetFloat("_Strength",strength);image.material=runtimeMaterial;}
  void OnDestroy(){if(runtimeMaterial)Destroy(runtimeMaterial);}
 }
}
