using UnityEngine;
using UnityEngine.UI;
namespace Breach.UI {
 // Editable native geometry for UI borders, server shells and circuit traces.
 [RequireComponent(typeof(CanvasRenderer))]
 public sealed class Shape:MaskableGraphic {
  public int sides=4;public float thickness=1;public bool filled=true;
  protected override void OnPopulateMesh(VertexHelper vh){vh.Clear();Rect r=rectTransform.rect;int count=sides==4?4:Mathf.Max(6,sides);Vector2[] outer=new Vector2[count];
   if(sides==4){outer[0]=new Vector2(r.xMin,r.yMin);outer[1]=new Vector2(r.xMin,r.yMax);outer[2]=new Vector2(r.xMax,r.yMax);outer[3]=new Vector2(r.xMax,r.yMin);}
   else for(int i=0;i<count;i++){float a=(90+i*360f/count)*Mathf.Deg2Rad;outer[i]=r.center+new Vector2(Mathf.Cos(a)*r.width/2,Mathf.Sin(a)*r.height/2);}
   if(filled){vh.AddVert(r.center,color,Vector2.zero);for(int i=0;i<count;i++)vh.AddVert(outer[i],color,Vector2.zero);for(int i=0;i<count;i++)vh.AddTriangle(0,i+1,(i+1)%count+1);}
   else {for(int i=0;i<count;i++){vh.AddVert(outer[i],color,Vector2.zero);Vector2 v=outer[i]-r.center;v.x*=Mathf.Max(0,1-2*thickness/Mathf.Max(1,r.width));v.y*=Mathf.Max(0,1-2*thickness/Mathf.Max(1,r.height));vh.AddVert(r.center+v,color,Vector2.zero);}for(int i=0;i<count;i++){int j=(i+1)%count;vh.AddTriangle(i*2,j*2,i*2+1);vh.AddTriangle(i*2+1,j*2,j*2+1);}}
  }
 }
 public static class Ui {
  public static Color Cyan=new Color(.22f,.85f,1),Red=new Color(1,.23f,.34f),Gold=new Color(1,.77f,.22f);
  public static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h){var go=new GameObject(name,typeof(RectTransform));var r=(RectTransform)go.transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
  public static Shape Box(string name,Transform parent,float x,float y,float w,float h,Color color,bool fill=true,float weight=1,int sides=4){var r=Rect(name,parent,x,y,w,h);var s=r.gameObject.AddComponent<Shape>();s.color=color;s.filled=fill;s.thickness=weight;s.sides=sides;s.raycastTarget=false;return s;}
  public static Shape Line(string name,Transform parent,Vector2 a,Vector2 b,Color color,float width=2){Vector2 d=b-a;var s=Box(name,parent,a.x,a.y,d.magnitude,width,color);s.rectTransform.pivot=new Vector2(0,.5f);s.rectTransform.localRotation=Quaternion.Euler(0,0,-Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);return s;}
  public static TMPro.TextMeshProUGUI Text(string name,Transform parent,float x,float y,float w,float h,string text,TMPro.TMP_FontAsset font,float size,Color color){var t=Rect(name,parent,x,y,w,h).gameObject.AddComponent<TMPro.TextMeshProUGUI>();t.font=font;t.fontSize=size;t.text=text;t.color=color;t.raycastTarget=false;t.overflowMode=TMPro.TextOverflowModes.Overflow;t.textWrappingMode=TMPro.TextWrappingModes.Normal;return t;}
 }
}
