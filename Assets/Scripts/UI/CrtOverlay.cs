using UnityEngine;
using UnityEngine.UI;
namespace Breach.UI {
 public sealed class CrtOverlay:MonoBehaviour {
  public Shader shader;
  [Range(0,1)]public float strength=.45f;
  public float Burst {get;private set;}
  Material runtimeMaterial;
  void Awake(){var image=GetComponent<RawImage>();image.raycastTarget=false;runtimeMaterial=new Material(shader);runtimeMaterial.SetFloat("_Strength",strength);image.material=runtimeMaterial;}
  public void SetBurst(float value){Burst=Mathf.Clamp01(value);if(runtimeMaterial)runtimeMaterial.SetFloat("_Burst",Burst);}
  void OnDisable(){SetBurst(0);}
  void OnDestroy(){if(runtimeMaterial)Destroy(runtimeMaterial);}
 }
}
