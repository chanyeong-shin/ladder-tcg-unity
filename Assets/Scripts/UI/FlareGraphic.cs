using UnityEngine;
using UnityEngine.UI;

namespace Breach.UI {
 // Horizontal optical bloom. Vertex alpha fades to zero; no rectangular sprite background.
 [RequireComponent(typeof(CanvasRenderer))]
 public sealed class FlareGraphic : MaskableGraphic {
  protected override void OnPopulateMesh(VertexHelper vh) {
   vh.Clear();Rect r=rectTransform.rect;Vector2 c=r.center;
   Disc(vh,c,r.width*.5f,r.height*.3f,new Color(color.r,color.g,color.b,color.a*.22f));
   Disc(vh,c,r.width*.34f,r.height*.45f,new Color(color.r,color.g,color.b,color.a*.25f));
   Vector2[] star={new Vector2(-r.width*.5f,0),new Vector2(-r.width*.045f,r.height*.06f),new Vector2(0,r.height*.5f),new Vector2(r.width*.045f,r.height*.06f),new Vector2(r.width*.5f,0),new Vector2(r.width*.045f,-r.height*.06f),new Vector2(0,-r.height*.5f),new Vector2(-r.width*.045f,-r.height*.06f)};
   Color core=Color.Lerp(color,Color.white,.8f);core.a=color.a;
   int start=vh.currentVertCount;vh.AddVert(c,core,Vector2.zero);
   for(int i=0;i<star.Length;i++)vh.AddVert(c+star[i],new Color(color.r,color.g,color.b,i%2==0?0:color.a),Vector2.zero);
   for(int i=0;i<star.Length;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%star.Length);
   Disc(vh,c,9,12,new Color(1,1,1,color.a));
  }
  static void Disc(VertexHelper vh,Vector2 c,float x,float y,Color tint){
   int start=vh.currentVertCount;vh.AddVert(c,tint,Vector2.zero);
   for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;vh.AddVert(c+new Vector2(Mathf.Cos(a)*x,Mathf.Sin(a)*y),new Color(tint.r,tint.g,tint.b,0),Vector2.zero);}
   for(int i=0;i<48;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%48);
  }
 }
}
